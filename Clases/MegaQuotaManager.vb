Imports System.Net

''' <summary>
''' v2.5 beta: MEGA 配额熔断器。匿名下载按 IP 限流(~5GB/6h),命中 509 / -17 后
''' 整个队列自动暂停,到期自动恢复。线程安全,可在任何工作线程调用。
''' </summary>
Public Class MegaQuotaExceededException
    Inherits ApplicationException

    Public Sub New()
        MyBase.New("MEGA transfer quota exceeded (HTTP 509 / EOVERQUOTA).")
    End Sub

    Public Sub New(message As String)
        MyBase.New(message)
    End Sub

    Public Sub New(message As String, inner As Exception)
        MyBase.New(message, inner)
    End Sub
End Class

Public NotInheritable Class MegaQuotaManager

    Private Shared ReadOnly _lock As New Object
    Private Shared _quotaUntilUtc As DateTime? = Nothing
    Private Shared _level As Integer = 0
    Private Shared _lastHitUtc As DateTime = DateTime.MinValue

    ' 档位自然衰减窗口:24 小时内无命中才回 0 档。到期/手动清除都不再归零档位——
    ' 否则熔断期内本就不发请求,60min→2h→6h 永远走不到;手动重试还会把档位打回 60min
    ' 又立刻发新请求,正好和"避免延长惩罚窗口"相反。

    ' 递进等待:60min -> 2h -> 6h 封顶
    Private Shared ReadOnly _durations As TimeSpan() = {
        TimeSpan.FromMinutes(60),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6)
    }
    Private Shared ReadOnly _levelDecay As TimeSpan = TimeSpan.FromHours(24)

    Public Shared Function IsQuarantined() As Boolean
        SyncLock _lock
            If Not _quotaUntilUtc.HasValue Then Return False
            If DateTime.UtcNow >= _quotaUntilUtc.Value Then
                _quotaUntilUtc = Nothing
                Return False
            End If
            Return True
        End SyncLock
    End Function

    Public Shared Function GetRemaining() As TimeSpan?
        SyncLock _lock
            If Not _quotaUntilUtc.HasValue Then Return Nothing
            Dim remaining As TimeSpan = _quotaUntilUtc.Value - DateTime.UtcNow
            If remaining.TotalSeconds <= 0 Then
                _quotaUntilUtc = Nothing
                Return Nothing
            End If
            Return remaining
        End SyncLock
    End Function

    ''' <summary>命中配额:首次 60min,24h 内重复命中升级到 2h/6h 封顶。</summary>
    Public Shared Sub ReportQuota(Optional hintSeconds As Long = 0)
        SyncLock _lock
            Dim nowUtc As DateTime = DateTime.UtcNow
            If _lastHitUtc = DateTime.MinValue OrElse nowUtc - _lastHitUtc > _levelDecay Then
                _level = 0
            Else
                ' 24h 内任何再次命中都升级:熔断期内重复、自然到期后、手动清除后——
                ' 不给"到期即洗白"也不给"手动即洗白",否则递进形同虚设。
                _level = Math.Min(_durations.Length - 1, _level + 1)
            End If
            _lastHitUtc = nowUtc
            Dim duration As TimeSpan = _durations(_level)
            If hintSeconds > 0 Then
                Dim hinted As TimeSpan = TimeSpan.FromSeconds(Math.Min(hintSeconds, CLng(_durations(_durations.Length - 1).TotalSeconds)))
                If hinted > duration Then duration = hinted
            End If
            _quotaUntilUtc = nowUtc.Add(duration)
            Log.WriteWarning(String.Format("MEGA quota hit: pausing queue for {0} (level {1}).", duration.ToString(), _level))
        End SyncLock
    End Sub

    Public Shared Sub ClearQuota()
        SyncLock _lock
            _quotaUntilUtc = Nothing
        End SyncLock
        Log.WriteWarning("MEGA quota cleared manually by user; queue will resume.")
    End Sub

    ''' <summary>仅用状态码判定,不做 body 文本匹配(避免误判)。</summary>
    Public Shared Function IsQuotaStatusCode(status As HttpStatusCode) As Boolean
        Return CInt(status) = 509
    End Function

    Public Shared Function IsQuotaWebException(ex As WebException) As Boolean
        If ex Is Nothing OrElse ex.Response Is Nothing Then Return False
        Try
            Dim resp As HttpWebResponse = TryCast(ex.Response, HttpWebResponse)
            If resp IsNot Nothing Then Return CInt(resp.StatusCode) = 509
        Catch
        End Try
        Return False
    End Function

    ''' <summary>从 WebException 响应中尝试读取 Retry-After:秒数或 HTTP-date(大概率没有,返回 Nothing)。</summary>
    Public Shared Function TryGetRetryAfterSeconds(ex As WebException) As Long?
        Try
            If ex Is Nothing OrElse ex.Response Is Nothing Then Return Nothing
            Dim resp As HttpWebResponse = TryCast(ex.Response, HttpWebResponse)
            If resp Is Nothing Then Return Nothing
            Dim v As String = resp.Headers.Item("Retry-After")
            If String.IsNullOrEmpty(v) Then Return Nothing
            Dim secs As Long
            If Long.TryParse(v.Trim(), secs) AndAlso secs > 0 Then Return secs
            ' RFC 7231 允许 HTTP-date 格式,此前只认秒数直接丢弃。先按 invariant "r" 精确匹配,
            ' 再回退宽松解析(非英文 locale 下英文日期名可能失败,失败即无 hint,安全回退)。
            Dim dto As DateTimeOffset
            If DateTimeOffset.TryParseExact(v.Trim(), "r", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, dto) _
                OrElse DateTimeOffset.TryParse(v.Trim(), dto) Then
                Dim wait As Long = CLng(Math.Ceiling((dto.UtcDateTime - DateTime.UtcNow).TotalSeconds))
                If wait > 0 Then Return wait
            End If
        Catch
        End Try
        Return Nothing
    End Function

End Class
