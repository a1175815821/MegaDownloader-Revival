Imports System.Net

''' <summary>
''' issue #6: shared classification of transport-level network failures.
''' TCP connect timeout, DNS pollution/hijack (e.g. the MEGA API host resolved
''' to a non-MEGA IP), firewall/VPN/proxy and TLS trust failures all happen
''' before any API logic runs, so the raw exception text ("Unable to connect
''' to the remote server", SocketException 10060, "The remote server returned
''' an error") tells the user nothing actionable. Surfaces that talk to the
''' network use this helper to show a friendly message while the technical
''' detail stays in the log.
''' </summary>
Public NotInheritable Class NetworkErrors

    ''' <summary>True when ex (or any inner exception, max depth 6) is a
    ''' transport failure rather than an application/API-level error.</summary>
    Public Shared Function IsTransportError(ByVal ex As Exception) As Boolean
        Dim current As Exception = ex
        Dim guard As Integer = 0
        While current IsNot Nothing AndAlso guard < 6
            If TypeOf current Is System.Net.Sockets.SocketException Then Return True
            If TypeOf current Is System.IO.IOException Then Return True
            If TypeOf current Is TimeoutException Then Return True
            Dim wex As WebException = TryCast(current, WebException)
            If wex IsNot Nothing Then
                Select Case wex.Status
                    Case WebExceptionStatus.ConnectFailure, WebExceptionStatus.Timeout, _
                         WebExceptionStatus.NameResolutionFailure, WebExceptionStatus.ReceiveFailure, _
                         WebExceptionStatus.SendFailure, WebExceptionStatus.KeepAliveFailure, _
                         WebExceptionStatus.PipelineFailure, WebExceptionStatus.SecureChannelFailure, _
                         WebExceptionStatus.TrustFailure
                        Return True
                End Select
            End If
            current = current.InnerException
            guard += 1
        End While
        Return False
    End Function

    ''' <summary>Actionable user message for a transport failure; %DETAIL% keeps
    ''' the technical detail (the inner SocketException carries the failed
    ''' IP:port, e.g. a polluted non-MEGA IP).</summary>
    Public Shared Function DescribeTransportError(ByVal ex As Exception) As String
        Dim template As String = Language.GetText("Network_Error")
        If String.IsNullOrEmpty(template) OrElse template = "Network_Error" Then
            template = "Cannot connect to the server (network error)." & vbCrLf & _
                "Check your internet connection, firewall/VPN/proxy, and try again." & _
                " If the service is restricted in your region, configure a proxy in Configuration or use a VPN." & _
                vbCrLf & "Technical detail: %DETAIL%"
        End If
        ' The outer WebException message alone ("unable to connect") hides the
        ' useful part, so append the inner exception when present.
        Dim detail As String = Log.SafeException(ex)
        If ex IsNot Nothing AndAlso ex.InnerException IsNot Nothing Then
            detail &= " (" & Log.SafeException(ex.InnerException) & ")"
        End If
        Return template.Replace("%DETAIL%", detail)
    End Function

    ''' <summary>Same friendly message when only a technical detail string is
    ''' available (e.g. a short-link resolver that swallowed its exception).</summary>
    Public Shared Function DescribeTransportError(ByVal detail As String) As String
        Return DescribeTransportError(New ApplicationException(detail))
    End Function

End Class
