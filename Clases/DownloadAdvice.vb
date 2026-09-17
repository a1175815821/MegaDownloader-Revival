''' <summary>
''' 失败原因人话翻译：把技术错误文本映射为用户可行动的建议。
''' 纯函数（只依赖公开字段与自有关键字表），可单元测试。
''' 注意：配额/永久失败的关键字表与 Fichero.IsQuotaErrorText / IsPermanentErrorText
''' 保持同步（Fichero 的是 Friend，本程序集外不可见，故此处自带一份最小判定）。
''' </summary>
Public NotInheritable Class DownloadAdvice

    Private Sub New()
    End Sub

    Public Enum FailureKind
        Unknown
        Quota
        Permanent
        Connection
    End Enum

    ''' <summary>按 Fichero 公开字段分类失败。</summary>
    Public Shared Function Classify(ByVal rawError As String, ByVal failedByQuota As Boolean, ByVal esPermanent As Boolean) As FailureKind
        If failedByQuota OrElse IsQuotaText(rawError) Then Return FailureKind.Quota
        If esPermanent OrElse IsPermanentText(rawError) Then Return FailureKind.Permanent
        If Not String.IsNullOrEmpty(rawError) AndAlso rawError.IndexOf("too many connection errors", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return FailureKind.Connection
        End If
        Return FailureKind.Unknown
    End Function

    Private Shared Function IsQuotaText(ByVal s As String) As Boolean
        If String.IsNullOrEmpty(s) Then Return False
        If s.IndexOf("EOVERQUOTA", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        If s.IndexOf("MegaQuotaExceededException", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        If s.IndexOf("transfer quota exceeded", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        Return False
    End Function

    Private Shared Function IsPermanentText(ByVal s As String) As Boolean
        If String.IsNullOrEmpty(s) Then Return False
        ' 与 Fichero.IsPermanentErrorText 同规则：只认错误码语（-9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED），不做泛文本匹配
        If s.IndexOf("-9", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("ENOENT", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        If s.IndexOf("-11", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("EACCESS", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        If s.IndexOf("-14", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("EKEY", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        If s.IndexOf("-16", StringComparison.Ordinal) >= 0 AndAlso s.IndexOf("EBLOCKED", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        Return False
    End Function

    ''' <summary>返回本地化建议正文，永不返回空（兜底为原文首行，再兜底为通用提示）。</summary>
    Public Shared Function Describe(ByVal rawError As String, ByVal failedByQuota As Boolean, ByVal esPermanent As Boolean) As String
        Select Case Classify(rawError, failedByQuota, esPermanent)
            Case FailureKind.Quota
                Return Language.GetText("Advice_Quota")
            Case FailureKind.Permanent
                Return Language.GetText("Advice_Permanent")
            Case FailureKind.Connection
                Return Language.GetText("Advice_Connection")
            Case Else
                Dim first As String = FirstLine(rawError)
                If Not String.IsNullOrEmpty(first) Then Return first
                Return Language.GetText("Notify_UnknownError")
        End Select
    End Function

    Public Shared Function FirstLine(ByVal s As String) As String
        If String.IsNullOrEmpty(s) Then Return ""
        Dim cut As Integer = s.IndexOf(vbLf)
        Dim line As String = If(cut >= 0, s.Substring(0, cut), s)
        line = line.Trim().Replace(vbCr, " ").Replace(vbTab, " ")
        If line.Length > 120 Then line = line.Substring(0, 120) & "..."
        Return line
    End Function

End Class
