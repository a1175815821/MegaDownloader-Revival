using System;
using System.Net;

namespace MegaDownloader
{

    /// <summary>
/// v2.5 beta: MEGA 配额熔断器。匿名下载按 IP 限流(~5GB/6h),命中 509 / -17 后
/// 整个队列自动暂停,到期自动恢复。线程安全,可在任何工作线程调用。
/// </summary>
    public class MegaQuotaExceededException : ApplicationException
    {

        public MegaQuotaExceededException() : base("MEGA transfer quota exceeded (HTTP 509 / EOVERQUOTA).")
        {
        }

        public MegaQuotaExceededException(string message) : base(message)
        {
        }

        public MegaQuotaExceededException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public sealed class MegaQuotaManager
    {

        private static readonly object _lock = new object();
        private static DateTime? _quotaUntilUtc = default;
        private static int _level = 0;
        private static DateTime _lastHitUtc = DateTime.MinValue;

        // 档位自然衰减窗口:24 小时内无命中才回 0 档。到期/手动清除都不再归零档位——
        // 否则熔断期内本就不发请求,60min→2h→6h 永远走不到;手动重试还会把档位打回 60min
        // 又立刻发新请求,正好和"避免延长惩罚窗口"相反。

        // 递进等待:60min -> 2h -> 6h 封顶
        private static readonly TimeSpan[] _durations = new[] { TimeSpan.FromMinutes(60d), TimeSpan.FromHours(2d), TimeSpan.FromHours(6d) };
        private static readonly TimeSpan _levelDecay = TimeSpan.FromHours(24d);

        /// <summary>配额突破模式(旧版行为,可选,默认关):命中配额只记日志,不熔断不升级。
    /// 此时配额失败走普通瞬时错误链路(chunk 重连 -&gt; 30 次停 -&gt; 失败自愈按
    /// ResetearErrores 周期复活),即 v1.8 的持续重试。由设置界面开关,Config 持久化。</summary>
        private static bool _breakthroughMode = false;

        public static void SetBreakthroughMode(bool enabled)
        {
            lock (_lock)
            {
                if (_breakthroughMode != enabled)
                {
                    _breakthroughMode = enabled;
                    Log.WriteWarning("MEGA quota breakthrough mode " + (enabled ? "enabled (no quarantine on 509/-17)." : "disabled (circuit-breaker active)."));
                }
            }
        }

        public static bool IsBreakthroughMode()
        {
            lock (_lock)
                return _breakthroughMode;
        }

        public static bool IsQuarantined()
        {
            lock (_lock)
            {
                if (_breakthroughMode)
                    return false;
                if (!_quotaUntilUtc.HasValue)
                    return false;
                if (DateTime.UtcNow >= _quotaUntilUtc.Value)
                {
                    _quotaUntilUtc = default;
                    return false;
                }
                return true;
            }
        }

        public static TimeSpan? GetRemaining()
        {
            lock (_lock)
            {
                if (_breakthroughMode)
                    return default;
                if (!_quotaUntilUtc.HasValue)
                    return default;
                var remaining = _quotaUntilUtc.Value - DateTime.UtcNow;
                if (remaining.TotalSeconds <= 0d)
                {
                    _quotaUntilUtc = default;
                    return default;
                }
                return remaining;
            }
        }

        /// <summary>命中配额:首次 60min,24h 内重复命中升级到 2h/6h 封顶。
    /// 熔断期内的重复上报视为同一事件(多连接并发命中、120s 看门狗合成的配额超时、
    /// chunk 失败汇总再次上报),直接忽略不升级不延长,避免一次真实配额瞬间跳到 6h、
    /// 也避免熔断期内自激延长导致"明明换 IP 能下却一直被中止"。</summary>
        public static void ReportQuota(long hintSeconds = 0L)
        {
            lock (_lock)
            {
                // 突破模式:只记不隔离,调用方走普通瞬时错误链(重连/自愈),不进熔断升级。
                if (_breakthroughMode)
                    return;
                var nowUtc = DateTime.UtcNow;
                // 同一配额事件去重:已在熔断期内,不升级档位、不刷新 _lastHitUtc、不延长等待
                // (hint 明确要求更久才延长)。_lastHitUtc 不刷新很关键,否则并发命中会把
                // "24h 衰减窗口"不断续上,档位永远下不来。
                if (_quotaUntilUtc.HasValue && nowUtc < _quotaUntilUtc.Value)
                {
                    if (hintSeconds > 0L)
                    {
                        long maxSecs = (long)Math.Round(_durations[_durations.Length - 1].TotalSeconds);
                        var hintedUntil = nowUtc.AddSeconds(Math.Min(hintSeconds, maxSecs));
                        if (hintedUntil > _quotaUntilUtc.Value)
                        {
                            _quotaUntilUtc = hintedUntil;
                            Log.WriteWarning(string.Format("MEGA quota re-hit during quarantine; extending wait to {0} (level {1} unchanged).", (_quotaUntilUtc.Value - nowUtc).ToString(), _level));
                        }
                    }
                    return;
                }
                if (_lastHitUtc == DateTime.MinValue || nowUtc - _lastHitUtc > _levelDecay)
                {
                    _level = 0;
                }
                else
                {
                    // 仅熔断期外(自然到期后、手动清除后)的再次命中才升级:
                    // 不给"到期即洗白"也不给"手动即洗白",否则递进形同虚设。
                    _level = Math.Min(_durations.Length - 1, _level + 1);
                }
                _lastHitUtc = nowUtc;
                var duration = _durations[_level];
                if (hintSeconds > 0L)
                {
                    var hinted = TimeSpan.FromSeconds(Math.Min(hintSeconds, (long)Math.Round(_durations[_durations.Length - 1].TotalSeconds)));
                    if (hinted > duration)
                        duration = hinted;
                }
                _quotaUntilUtc = nowUtc.Add(duration);
                Log.WriteWarning(string.Format("MEGA quota hit: pausing queue for {0} (level {1}).", duration.ToString(), _level));
            }
        }

        public static void ClearQuota()
        {
            lock (_lock)
                _quotaUntilUtc = default;
            Log.WriteWarning("MEGA quota cleared manually by user; queue will resume.");
        }

        /// <summary>仅用状态码判定,不做 body 文本匹配(避免误判)。</summary>
        public static bool IsQuotaStatusCode(HttpStatusCode status)
        {
            return (int)status == 509;
        }

        public static bool IsQuotaWebException(WebException ex)
        {
            if (ex is null || ex.Response is null)
                return false;
            try
            {
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                if (resp is not null)
                    return (int)resp.StatusCode == 509;
            }
            catch
            {
            }
            return false;
        }

        /// <summary>从 WebException 响应中尝试读取 Retry-After:秒数或 HTTP-date(大概率没有,返回 Nothing)。</summary>
        public static long? TryGetRetryAfterSeconds(WebException ex)
        {
            try
            {
                if (ex is null || ex.Response is null)
                    return default;
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                if (resp is null)
                    return default;
                string v = resp.Headers["Retry-After"];
                if (string.IsNullOrEmpty(v))
                    return default;
                long secs;
                if (long.TryParse(v.Trim(), out secs) && secs > 0L)
                    return secs;
                // RFC 7231 允许 HTTP-date 格式,此前只认秒数直接丢弃。先按 invariant "r" 精确匹配,
                // 再回退宽松解析(非英文 locale 下英文日期名可能失败,失败即无 hint,安全回退)。
                DateTimeOffset dto;
                if (DateTimeOffset.TryParseExact(v.Trim(), "r", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dto) || DateTimeOffset.TryParse(v.Trim(), out dto))
                {
                    long wait = (long)Math.Round(Math.Ceiling((dto.UtcDateTime - DateTime.UtcNow).TotalSeconds));
                    if (wait > 0L)
                        return wait;
                }
            }
            catch
            {
            }
            return default;
        }

    }
}