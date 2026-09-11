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

    ' 递进等待:60min -> 2h -> 6h 封顶
    Private Shared ReadOnly _durations As TimeSpan() = {
        TimeSpan.FromMinutes(60),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6)
    }

    Public Shared Function IsQuarantined() As Boolean
        SyncLock _lock
            If Not _quotaUntilUtc.HasValue Then Return False
            If DateTime.UtcNow >= _quotaUntilUtc.Value Then
                _quotaUntilUtc = Nothing
                _level = 0
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
                _level = 0
                Return Nothing
            End If
            Return remaining
        End SyncLock
    End Function

    ''' <summary>命中配额:首次 60min,配额期内重复命中升级到 2h/6h 封顶。</summary>
    Public Shared Sub ReportQuota(Optional hintSeconds As Long = 0)
        SyncLock _lock
            Dim nowUtc As DateTime = DateTime.UtcNow
            Dim stillQuarantined As Boolean = _quotaUntilUtc.HasValue AndAlso nowUtc < _quotaUntilUtc.Value
            If stillQuarantined Then
                _level = Math.Min(_durations.Length - 1, _level + 1)
            Else
                _level = 0
            End If
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
            _level = 0
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

    ''' <summary>从 WebException 响应中尝试读取 Retry-After 秒数(大概率没有,返回 Nothing)。</summary>
    Public Shared Function TryGetRetryAfterSeconds(ex As WebException) As Long?
        Try
            If ex Is Nothing OrElse ex.Response Is Nothing Then Return Nothing
            Dim resp As HttpWebResponse = TryCast(ex.Response, HttpWebResponse)
            If resp Is Nothing Then Return Nothing
            Dim v As String = resp.Headers.Item("Retry-After")
            If String.IsNullOrEmpty(v) Then Return Nothing
            Dim secs As Long
            If Long.TryParse(v.Trim(), secs) AndAlso secs > 0 Then Return secs
        Catch
        End Try
        Return Nothing
    End Function

End Class
