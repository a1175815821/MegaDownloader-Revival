using System;
using Microsoft.VisualBasic;

namespace MegaDownloader
{
    /// <summary>
/// 失败原因人话翻译：把技术错误文本映射为用户可行动的建议。
/// 纯函数（只依赖公开字段与自有关键字表），可单元测试。
/// 注意：配额/永久失败的关键字表与 Fichero.IsQuotaErrorText / IsPermanentErrorText
/// 保持同步（Fichero 的是 Friend，本程序集外不可见，故此处自带一份最小判定）。
/// </summary>
    public sealed class DownloadAdvice
    {

        private DownloadAdvice()
        {
        }

        public enum FailureKind
        {
            Unknown,
            Quota,
            Permanent,
            Connection
        }

        /// <summary>按 Fichero 公开字段分类失败。</summary>
        public static FailureKind Classify(string rawError, bool failedByQuota, bool esPermanent)
        {
            if (failedByQuota || IsQuotaText(rawError))
                return FailureKind.Quota;
            if (esPermanent || IsPermanentText(rawError))
                return FailureKind.Permanent;
            if (!string.IsNullOrEmpty(rawError) && rawError.IndexOf("too many connection errors", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FailureKind.Connection;
            }
            return FailureKind.Unknown;
        }

        private static bool IsQuotaText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            if (s.IndexOf("EOVERQUOTA", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("MegaQuotaExceededException", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("transfer quota exceeded", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static bool IsPermanentText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            // 与 Fichero.IsPermanentErrorText 同规则：只认错误码语（-9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED），不做泛文本匹配
            if (s.IndexOf("-9", StringComparison.Ordinal) >= 0 && s.IndexOf("ENOENT", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("-11", StringComparison.Ordinal) >= 0 && s.IndexOf("EACCESS", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("-14", StringComparison.Ordinal) >= 0 && s.IndexOf("EKEY", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("-16", StringComparison.Ordinal) >= 0 && s.IndexOf("EBLOCKED", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        /// <summary>返回本地化建议正文，永不返回空（兜底为原文首行，再兜底为通用提示）。</summary>
        public static string Describe(string rawError, bool failedByQuota, bool esPermanent)
        {
            switch (Classify(rawError, failedByQuota, esPermanent))
            {
                case FailureKind.Quota:
                    return Language.GetText("Advice_Quota");
                case FailureKind.Permanent:
                    return Language.GetText("Advice_Permanent");
                case FailureKind.Connection:
                    return Language.GetText("Advice_Connection");
                default:
                    {
                        string first = FirstLine(rawError);
                        if (!string.IsNullOrEmpty(first))
                            return first;
                        return Language.GetText("Notify_UnknownError");
                    }
            }
        }

        public static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            int cut = s.IndexOf(Constants.vbLf);
            string line = cut >= 0 ? s.Substring(0, cut) : s;
            line = line.Trim().Replace(Constants.vbCr, " ").Replace(Constants.vbTab, " ");
            if (line.Length > 120)
                line = line.Substring(0, 120) + "...";
            return line;
        }

    }
}