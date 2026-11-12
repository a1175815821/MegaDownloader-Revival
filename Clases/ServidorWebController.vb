Public Class ServidorWebController

    Private Shared _WebServer As HttpServer.HttpServer = Nothing
    Private Shared _WebServerStreaming As HttpServer.HttpServer = Nothing


    Public Shared Function StartWebServer(ByVal Downloader As IDownloaderService, ByVal Config As Configuracion) As String
        If Config Is Nothing Then
            Return ""
        End If


        If Not Config.ServidorWebActivo Then
            StopWebServer_RemoteController()
        End If
        If Not Config.ServidorStreamingActivo Then
            StopWebServer_Streaming()
        End If

        ' 两个服务器彼此独立:Web 端口被占用不得连带让流媒体也起不来(此前早 Return
        ' 直接跳过了整个流媒体启动块)。分别启动,末尾汇总错误。
        Dim errores As New Generic.List(Of String)
        Dim errWeb As String = StartRemoteController(Downloader, Config)
        If Not String.IsNullOrEmpty(errWeb) Then errores.Add(errWeb)
        Dim errStreaming As String = StartStreamingServer(Downloader, Config)
        If Not String.IsNullOrEmpty(errStreaming) Then errores.Add(errStreaming)
        Return String.Join(" ", errores.ToArray())

    End Function

    Private Shared Function StartRemoteController(ByVal Downloader As IDownloaderService, ByVal Config As Configuracion) As String
        If _WebServer IsNot Nothing OrElse Not Config.ServidorWebActivo Then Return ""

        If IsBusy(Config.ServidorWebPuerto) Then
            Return "Port " & Config.ServidorWebPuerto & " is not valid or is in use."
        End If

        ' Default security posture: bind loopback only. Opt-in LAN access
        ' binds all interfaces so phones/other PCs can push downloads, but a
        ' password is MANDATORY — an open LAN listener would let anyone on
        ' the network control the downloader.
        Dim bindAddress As System.Net.IPAddress = System.Net.IPAddress.Loopback
        If Config.ServidorWebPermitirLAN Then
            If String.IsNullOrEmpty(Config.ServidorWebPassword) Then
                Return "A web server password is required to allow LAN access."
            End If
            If Not String.IsNullOrEmpty(Config.ServidorWebBindIP) Then
                Try
                    bindAddress = System.Net.IPAddress.Parse(Config.ServidorWebBindIP)
                Catch ex As FormatException
                    Return "Invalid web server bind IP: " & Config.ServidorWebBindIP
                End Try
                Log.WriteWarning("Web server: LAN access enabled — binding to " & bindAddress.ToString & " (password required).")
            Else
                bindAddress = System.Net.IPAddress.Any
                Log.WriteWarning("Web server: LAN access enabled — listening on all interfaces (password required).")
            End If
        End If

        Try
            _WebServer = New HttpServer.HttpServer()
            _WebServer.ServerName = "Internal"
            _WebServer.SessionCookieName = "Sd_session"
            _WebServer.Add(New WebInterfaceModule(Downloader, _
                                            Config.ServidorWebRutaPlantilla, _
                                            Config.ServidorWebPassword, _
                                            Config.ServidorWebNombre, _
                                            Config.ServidorWebTimeout * 60, _
                                            Language.GetCurrentLanguageCode))
            _WebServer.Start(bindAddress, Config.ServidorWebPuerto)
        Catch ex As Exception
            _WebServer = Nothing
            Log.WriteError("Error starting web server: " & ex.ToString)
            Return ex.Message
        End Try

        Return ""
    End Function

    Private Shared Function StartStreamingServer(ByVal Downloader As IDownloaderService, ByVal Config As Configuracion) As String
        If _WebServerStreaming IsNot Nothing OrElse Not Config.ServidorStreamingActivo Then Return ""

        If IsBusy(Config.ServidorStreamingPuerto) Then
            Return "Port " & Config.ServidorStreamingPuerto & " is not valid or is in use."
        End If

        Try
            _WebServerStreaming = New HttpServer.HttpServer()
            _WebServerStreaming.ServerName = "Streaming"
            ' Must differ from the remote controller's cookie name: both servers bind
            ' 127.0.0.1 and browsers scope cookies by host, not by port, so a shared
            ' name made the two sessions overwrite each other and forced re-login.
            _WebServerStreaming.SessionCookieName = "Sd_streaming"
            _WebServerStreaming.Add(New MegaDownloader.StreamingModule(Config))
            _WebServerStreaming.Add(New MegaDownloader.StreamingLibraryModule(Downloader, Config))
            _WebServerStreaming.Start(System.Net.IPAddress.Loopback, Config.ServidorStreamingPuerto)
        Catch ex As Exception
            _WebServerStreaming = Nothing
            Log.WriteError("Error starting streaming web server: " & ex.ToString)
            Return ex.Message
        End Try

        Return ""
    End Function

    Public Shared Sub StopWebServer()
        StopWebServer_RemoteController()
        StopWebServer_Streaming()
    End Sub

    Public Shared Sub StopWebServer_RemoteController()
        Try
            If _WebServer IsNot Nothing Then
                _WebServer.Stop()
                _WebServer = Nothing
            End If

        Catch ex As Exception
            Log.WriteError("Error stopping web server: " & ex.ToString)
        End Try

    End Sub

    Public Shared Sub StopWebServer_Streaming()
        Try

            If _WebServerStreaming IsNot Nothing Then
                _WebServerStreaming.Stop()
                _WebServerStreaming = Nothing
            End If
        Catch ex As Exception
            Log.WriteError("Error stopping streaming web server: " & ex.ToString)
        End Try

    End Sub


    Private Shared Function IsBusy(port As Integer) As Boolean
        Dim ipGP As Net.NetworkInformation.IPGlobalProperties = Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
        Dim endpoints As Net.IPEndPoint() = ipGP.GetActiveTcpListeners()
        If endpoints Is Nothing OrElse endpoints.Length = 0 Then
            Return False
        End If
        For i As Integer = 0 To endpoints.Length - 1
            If endpoints(i).Port = port Then
                Return True
            End If
        Next
        Return False
    End Function




End Class
