Imports System.Net
Imports System.IO
Imports System.Xml
Imports System.Net.Security
Imports System.ComponentModel

Public Class Conexion

    Private Shared useGlobalCDN As Boolean = True


    Public Enum TipoError
        SinErrores
        UsuarioInvalido
        ErrorConexion
        Otros
    End Enum

    Public Class InformacionFichero
        Public URL As String
        Public FileID As String
        Public FileKey As String
        Public Nombre As String
        Public MD5 As String
        Public Tamano As Long
        Public Err As TipoError = TipoError.SinErrores
        Public Errtxt As String
    End Class

    Friend Class Respuesta
        Public Status As HttpStatusCode
        Public Mensaje As String
        Public Excepcion As Exception
        Public Cookies As String
    End Class

    Private Shared _UsarProxy As Boolean = False
    Private Shared _ProxyIP As String
    Private Shared _ProxyUser As String
    Private Shared _ProxyPassword As String
    Private Shared _ProxyPort As Integer

    Public Shared Sub SetProxy(Config As Configuracion)
        _UsarProxy = Config.UsarProxy
        _ProxyIP = Config.ProxyIP
        _ProxyPort = Config.ProxyPort
        _ProxyUser = _ProxyUser
        _ProxyPassword = _ProxyPassword
    End Sub

    Public Shared Function CreateHttpWebRequest(ByVal Url As String) As HttpWebRequest
        System.Net.ServicePointManager.SecurityProtocol = Net.SecurityProtocolType.Tls12 Or Net.SecurityProtocolType.Tls11 Or Net.SecurityProtocolType.Tls
        System.Net.ServicePointManager.DefaultConnectionLimit = 10000

        Dim webrq As HttpWebRequest = CType(Net.WebRequest.Create(Url), HttpWebRequest)

        If _UsarProxy Then
            Dim proxy As New WebProxy(_ProxyIP, _ProxyPort)
            If Not String.IsNullOrEmpty(_ProxyUser) Then
                proxy.Credentials = New NetworkCredential(_ProxyUser, _ProxyPassword)
            End If
            webrq.Proxy = proxy
        End If

        'webrq.UserAgent = GetUserAgent()

        ServicePointManager.ServerCertificateValidationCallback = AddressOf CertificationAccept

        Return webrq
    End Function

    Public Shared Function CertificationAccept(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean

        ' Skip errors with linkcrypter :/
        If CType(sender, System.Net.HttpWebRequest).RequestUri.ToString.Contains("linkcrypter.net") Then
            Return True
        Else
            Return Security.SslPolicyErrors.None = sslPolicyErrors
        End If

    End Function

    Public Shared Function Get_MEGA_API_Url(ByVal Session As String) As String

        Dim Url As String
        If String.IsNullOrEmpty(Session) Then
            Url = InternalConfiguration.ObtenerValueFromInternalConfig(If(useGlobalCDN, "URL_MEGA_API_NOSESION_G", "URL_MEGA_API_NOSESION_EU"))
            If Not Url.ToUpper.StartsWith("HTTP") Then Url = Criptografia.AES_DecryptString(Url, keyUrl)
        Else
            Url = InternalConfiguration.ObtenerValueFromInternalConfig(If(useGlobalCDN, "URL_MEGA_API_SESION_G", "URL_MEGA_API_SESION_EU"))
            If Not Url.ToUpper.StartsWith("HTTP") Then Url = Criptografia.AES_DecryptString(Url, keyUrl)
            Url = Url.Replace("%SESSION%", Session)
        End If

        Url = Url.Replace("%SEQ%", TimeSpan.FromMilliseconds(DateTime.Now.Millisecond).Ticks.ToString)
        Return Url
    End Function

    Friend Shared Function SendJSON(ByVal URL As String, ByVal JSON As String, Optional ByVal ContentType As String = "", Optional ByVal SendAppId As Boolean = True) As Respuesta


        Dim webRQ As HttpWebRequest = CreateHttpWebRequest(URL)
        Dim webRS As HttpWebResponse = Nothing

        'Dim App_ID As String = GetAppID()
        'If SendAppId Then webRQ.Headers.Add("APPID", App_ID)
        webRQ.Method = "POST"

        Dim data As Byte() = System.Text.Encoding.UTF8.GetBytes(JSON)

        webRQ.ContentLength = data.Length
        If Not String.IsNullOrEmpty(ContentType) Then
            webRQ.ContentType = ContentType
        End If


        Dim inStream As StreamReader = Nothing

        Dim Resultado As New Respuesta
        Resultado.Status = HttpStatusCode.Unused
        Resultado.Excepcion = Nothing
        Resultado.Mensaje = ""

        Try
            Using newStream = webRQ.GetRequestStream()
                newStream.Write(data, 0, data.Length)
            End Using

            webRS = CType(webRQ.GetResponse, HttpWebResponse)

            inStream = New StreamReader(webRS.GetResponseStream())
            Resultado.Mensaje = inStream.ReadToEnd()
            Resultado.Status = webRS.StatusCode

            Resultado.Cookies = webRS.Headers("Set-Cookie")

        Catch ex As WebException
            Try
                If ex.Response IsNot Nothing Then
                    Resultado.Status = CType(ex.Response, HttpWebResponse).StatusCode
                    Using Stream = ex.Response.GetResponseStream()
                        Using reader = New StreamReader(Stream)
                            Resultado.Mensaje = reader.ReadToEnd()
                        End Using
                    End Using
                End If

            Catch exc As WebException
                ' Oh, well, we tried
            End Try
            Log.WriteError("Error accessing the URL: " & ex.ToString & " - Message received: " & Resultado.Mensaje)
            Resultado.Excepcion = ex
        Catch ex As Exception
            Log.WriteError("Error accessing the URL: " & ex.ToString)
            Resultado.Excepcion = ex
        Finally
            If webRS IsNot Nothing Then webRS.Close()
            If inStream IsNot Nothing Then inStream.Close()
        End Try

        Return Resultado
    End Function


    Friend Shared Function SendPOST(ByVal URL As String, ByVal PostParameters As Specialized.NameValueCollection, _
                                    Optional ByVal ContentType As String = "application/x-www-form-urlencoded", _
                                    Optional ByVal SendAppId As Boolean = True) As Respuesta
        Dim str As New System.Text.StringBuilder
        For Each param As String In PostParameters.Keys
            Dim value As String = PostParameters.Item(param)
            If str.Length > 0 Then str.Append("&")
            str.Append(Web.HttpUtility.UrlEncode(param))
            str.Append("=")
            str.Append(Web.HttpUtility.UrlEncode(value))
        Next
        If str.Length = 0 Then
            Throw New ArgumentException("No POST parameters found")
        End If
        Return SendJSON(URL, str.ToString, ContentType, SendAppId)
    End Function

    Friend Shared Function ObtenerUrlDesdeAcortador(ByVal URL As String) As String
        Dim webReq As HttpWebRequest = Nothing
        Dim webResp As HttpWebResponse = Nothing

        Try
            webReq = CreateHttpWebRequest(URL)
            webResp = CType(webReq.GetResponse, HttpWebResponse)

            Return webResp.ResponseUri.ToString
        Catch ex As Exception
            Return String.Empty
        Finally
            If webResp IsNot Nothing Then webResp.Close()
        End Try
    End Function


    Friend Shared Function LeerURL(ByVal URL As String, _
                                   Optional encoding As System.Text.Encoding = Nothing, _
                                   Optional userAgent As String = Nothing, _
                                   Optional referer As String = Nothing, _
                                   Optional Headers As Specialized.NameValueCollection = Nothing) As Respuesta
        Dim webReq As HttpWebRequest = Nothing
        Dim webResp As HttpWebResponse = Nothing
        Dim inStream As StreamReader = Nothing

        Dim Res As New Respuesta
        Res.Status = HttpStatusCode.Unused
        Res.Excepcion = Nothing
        Res.Mensaje = ""


        Try
            webReq = CreateHttpWebRequest(URL)

            If Headers IsNot Nothing Then
                For Each header In Headers.AllKeys
                    webReq.Headers.Add(header, Headers(header))
                Next

            End If


            If Not String.IsNullOrEmpty(referer) Then
                webReq.Referer = referer
            End If
            If Not String.IsNullOrEmpty(userAgent) Then
                webReq.UserAgent = userAgent
            End If
            webResp = CType(webReq.GetResponse, HttpWebResponse)

            Dim encode As System.Text.Encoding = encoding
            If encode Is Nothing Then encode = System.Text.Encoding.GetEncoding("utf-8")

            inStream = New StreamReader(webResp.GetResponseStream(), encode)
            Res.Mensaje = inStream.ReadToEnd()
            Res.Status = webResp.StatusCode
        Catch ex As WebException
            Try
                If ex.Response IsNot Nothing Then
                    Res.Status = CType(ex.Response, HttpWebResponse).StatusCode
                    Using Stream = ex.Response.GetResponseStream()
                        Dim encode As System.Text.Encoding = encoding
                        If encode Is Nothing Then encode = System.Text.Encoding.GetEncoding("utf-8")
                        Using reader = New StreamReader(Stream, encode)
                            Res.Mensaje = reader.ReadToEnd()
                        End Using
                    End Using
                End If

            Catch exc As WebException
                ' Oh, well, we tried
            End Try
            Log.WriteError("Error accessing the URL: " & ex.ToString & " - Message received: " & Res.Mensaje)
            Res.Excepcion = ex
        Catch ex As Exception
            Log.WriteError("Error accessing the URL: " & ex.ToString)
            Res.Excepcion = ex
        Finally
            If webResp IsNot Nothing Then webResp.Close()
            If inStream IsNot Nothing Then inStream.Close()
        End Try
        Return Res
    End Function



    Private Const keyUrl As String = "81379874BC2815E6825E98F986F98410EFC68D6DABC6EF8B54968C75387551F2"

    Public Shared Function GetUpdateCheckURL() As String
        Dim URL As String = InternalConfiguration.ObtenerValueFromInternalConfig("URL_UPDATE_CHECK")
        URL = Criptografia.AES_DecryptString(URL, keyUrl)

        'URL = URL.Replace("%VERSION%", InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_UPDATE"))
        URL = URL.Replace("%D%", Date.UtcNow.ToString("yyyyMMddHH"))
        Return URL
    End Function
    'Public Shared Function GetAppID() As String
    '    Dim Code As String = InternalConfiguration.ObtenerValueFromInternalConfig("APP_ID")
    '    Code = Criptografia.AES_DecryptString(Code, keyUrl)
    '    Code &= InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER")
    '    Return Code
    'End Function


    Private Shared Function TratarUsuario(ByVal Usuario As String) As String
        Dim str As String = System.Uri.EscapeUriString(Usuario & "")
        Return str
    End Function

    Friend Const patternGetFileName As String = "MEGA.*?""n""\s*:\s*""(?<FileName>.*?)"""


    Public Shared Function ObtenerInformacionFichero(ByVal Config As Configuracion, ByVal FileID As String, ByVal FileKey As String, ByVal ComprobacionAntesDescarga As Boolean) As InformacionFichero

        If Crypters.MegaCrypter.IsMegaCrypter(FileID) Then Return Crypters.MegaCrypter.ObtenerInformacionFichero(Config, FileID, FileKey, ComprobacionAntesDescarga)
        If Crypters.YouPaste.IsYouPaste(FileID) Then Return Crypters.YouPaste.ObtenerInformacionFichero(Config, FileID, FileKey, ComprobacionAntesDescarga)
        If Crypters.EncrypterMega.IsEncrypterMega(FileID) Then Return Crypters.EncrypterMega.ObtenerInformacionFichero(Config, FileID, FileKey, ComprobacionAntesDescarga)
        If Crypters.LinkCrypter.IsLinkCrypter(FileID) Then Return Crypters.LinkCrypter.ObtenerInformacionFichero(Config, FileID, FileKey, ComprobacionAntesDescarga)

        Dim Info As New InformacionFichero
        Try
            URLExtractor.CheckFileIDAndFileKey(FileID, FileKey)
        Catch ex As Exception
            Info.Err = TipoError.Otros
            If TypeOf (ex) Is System.ArgumentOutOfRangeException Then
                Info.Errtxt = "Error getting ID and File Key: the provided link seems to be incomplete"
            Else
                Info.Errtxt = "Error getting ID and File Key: " & ex.Message
            End If

            Log.WriteError("Error getting ID and File Key: " & ex.ToString)
            Return Info
        End Try


        Info.FileID = FileID
        Info.FileKey = FileKey

        Dim URL As String = InternalConfiguration.ObtenerValueFromInternalConfig(If(useGlobalCDN, "URL_MEGA_API_DOWN_GET_G", "URL_MEGA_API_DOWN_GET_EU"))
        If Not URL.ToUpper.StartsWith("HTTP") Then URL = Criptografia.AES_DecryptString(URL, keyUrl)


        URL = URL.Replace("%ID%", TimeSpan.FromMilliseconds(DateTime.Now.Millisecond).Ticks.ToString)

        Dim useSSL As String = "0"

        Dim json As String
        If FileID.StartsWith("megafolder?") AndAlso FileID.Split("?"c).Length = 3 Then ' Private file from folder: old system (just for compatibility)
            json = String.Format("[{{""a"":""g"",""g"":""1"",""ssl"":{0},""n"":""{1}""}}]", useSSL, FileID.Split("?"c)(2))
            URL &= "&n=" & FileID.Split("?"c)(1)
        ElseIf FileID.StartsWith("N?") AndAlso FileID.Split("?"c).Length = 2 Then ' Private file from folder: new system
            If Info.FileKey.Contains("=###n=") Then
                URL &= "&n=" & Info.FileKey.Substring(Info.FileKey.IndexOf("=###n=") + 6)
            End If
            json = String.Format("[{{""a"":""g"",""g"":""1"",""ssl"":{0},""n"":""{1}""}}]", useSSL, FileID.Split("?"c)(1))
        Else
            If Info.FileKey.Contains("=###n=") Then
                URL &= "&n=" & Info.FileKey.Substring(Info.FileKey.IndexOf("=###n=") + 6)
            End If
            json = String.Format("[{{""a"":""g"",""g"":""1"",""ssl"":{0},""p"":""{1}""}}]", useSSL, FileID)
        End If

        Dim data As Byte() = System.Text.Encoding.UTF8.GetBytes(json)

        Dim Resultado As Respuesta = SendJSON(URL, json)


        If Resultado.Excepcion Is Nothing Then

            Dim FileInfoRS As Generic.Dictionary(Of String, Object)
            Try
                Dim str As String = Resultado.Mensaje
                If str.StartsWith("[") Then
                    str = str.Substring(1)
                End If
                If str.EndsWith("]") Then
                    str = str.Trim("]"c)
                End If

                If IsNumeric(str) Then
                    Throw MEGA_ErrorHandler.GetErrorFromMegaResponse(str, "when retrieving file information")
                End If

                FileInfoRS = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(str, _
                                                                                 GetType(Generic.Dictionary(Of String, Object))),  _
                                                                                 Generic.Dictionary(Of String, Object))

                If FileInfoRS.ContainsKey("e") AndAlso FileInfoRS.Item("e") IsNot Nothing Then
                    Throw MEGA_ErrorHandler.GetErrorFromMegaResponse(FileInfoRS.Item("e").ToString, "when retrieving file information")
                End If

                If Not FileInfoRS.ContainsKey("s") Then Throw New Exception
                If Not FileInfoRS.ContainsKey("at") Then Throw New Exception
                If Not FileInfoRS.ContainsKey("g") Then Throw New Exception
            Catch excFileInfo As Exception
                Info.Err = TipoError.Otros
                If excFileInfo.Message.Contains("when retrieving file information") Then
                    Info.Errtxt = excFileInfo.Message
                Else
                    Info.Errtxt = "Response not expected: " & Resultado.Mensaje
                End If
                Log.WriteError("Error getting the info for file " & FileID & ": Response not expected - Message received: " & Resultado.Mensaje & " - Error: " & excFileInfo.ToString)
                Return Info
            End Try

            Dim FileInfo As String = CStr(FileInfoRS.Item("at"))
            Dim FileSize As String = CStr(FileInfoRS.Item("s"))
            Dim Download As String = CStr(FileInfoRS.Item("g"))

            Dim FileInfoDec As String = ""

            FileInfoDec = PreSharedKeyManager.DecryptFileInfo(FileInfo, Info.FileKey)
            If String.IsNullOrEmpty(FileInfoDec) Then
                For Each PreSharedKey As String In PreSharedKeyManager.GetFileKeyFromPreSharedKeys(Config)
                    FileInfoDec = PreSharedKeyManager.DecryptFileInfo(FileInfo, PreSharedKey)
                    If Not String.IsNullOrEmpty(FileInfoDec) Then
                        Info.FileKey = PreSharedKey
                        Exit For
                    End If
                Next
            End If

            If String.IsNullOrEmpty(FileInfoDec) Then
                Info.Err = TipoError.Otros
                Info.Errtxt = "File could not be decrypted. Check the file Key is present on the URL and it is correct; check that the link doesn't have spaces in the middle (some forums/webpages add spaces in the middle of long words). In case of using a pre-shared key, make sure you have configured it."
                Log.WriteError("Error getting the info for file " & FileID & ": Error decrypting fileinfo - Message received: " & Resultado.Mensaje)
                Return Info
            End If

            Dim ex As New System.Text.RegularExpressions.Regex(patternGetFileName)
            If ex.IsMatch(FileInfoDec) Then
                Dim m As System.Text.RegularExpressions.Match = ex.Match(FileInfoDec)
                Info.Nombre = m.Groups("FileName").Value
                Info.URL = Download
                Info.Tamano = 0
                Long.TryParse(FileSize, Info.Tamano)
            Else
                Info.Err = TipoError.Otros
                Info.Errtxt = "File name could not be retrieved"
                Log.WriteError("Error getting the info for file " & FileID & ": File name could not be retrieved - Message received: " & Resultado.Mensaje & " - Fileinfo: " & FileInfoDec)
            End If



        ElseIf TypeOf (Resultado.Excepcion) Is WebException Then
            Info.Err = TipoError.ErrorConexion
            Info.Errtxt = "Connection error: " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje
            Log.WriteError("Error getting the info for file " & FileID & ": " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje)
        Else
            Info.Err = TipoError.Otros
            Info.Errtxt = "Description: " & Resultado.Excepcion.ToString & " - Message received: " & Resultado.Mensaje
            Log.WriteError("Error getting the info for file " & FileID & ": " & Resultado.Excepcion.Message & " - Message received: " & Resultado.Mensaje)
        End If
        Return Info
    End Function

    Private Shared Function LeerNodo(ByRef DocumentoXML As XmlDocument, ByRef Path As String, ByVal ValorDefecto As String) As String
        Dim nodo As XmlNode = DocumentoXML.DocumentElement.SelectSingleNode(Path)
        If nodo Is Nothing Then
            Return ValorDefecto
        Else
            Return nodo.InnerText
        End If
    End Function


#Region "Ping"
    ''' <summary>
    ''' Función para hacer un ping en background; la primera conexión al ser SSL es muy lenta, la hacemos al arrancar
    ''' en segundo plano así el usuario no nota ese tiempo de "arranque en frío"
    ''' </summary>
    ''' <remarks></remarks>
    Public Shared Sub PingMega()
        bgPing = New BackgroundWorker
        bgPing.WorkerReportsProgress = False
        bgPing.WorkerSupportsCancellation = False
        bgPing.RunWorkerAsync()
    End Sub

    Public Shared Sub PingNewUser()
        bgPingNewUser = New BackgroundWorker
        bgPingNewUser.WorkerReportsProgress = False
        bgPingNewUser.WorkerSupportsCancellation = False
        bgPingNewUser.RunWorkerAsync()
    End Sub

    Public Shared Sub PingNewVersion()
        bgPingNewVersion = New BackgroundWorker
        bgPingNewVersion.WorkerReportsProgress = False
        bgPingNewVersion.WorkerSupportsCancellation = False
        bgPingNewVersion.RunWorkerAsync()
    End Sub

    Private Shared WithEvents bgPing As BackgroundWorker
    Private Shared WithEvents bgPingNewVersion As BackgroundWorker
    Private Shared WithEvents bgPingNewUser As BackgroundWorker

    Private Shared Sub bgPing_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles bgPing.DoWork
        Try
            Log.WriteDebug("Starting ping...")
            Dim URL As String = InternalConfiguration.ObtenerValueFromInternalConfig("URL_PING")
            URL = Criptografia.AES_DecryptString(URL, keyUrl)

            Dim Resultado As Respuesta = LeerURL(URL)
            Log.WriteDebug("Ping finished")
        Catch ex As Exception
            ' No nos interesa lo que haga...
        End Try
    End Sub

    Private Shared Sub bgPingNewVersion_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles bgPingNewVersion.DoWork
        Try
            Log.WriteDebug("Starting ping new version...")
            Dim URL As String = InternalConfiguration.ObtenerValueFromInternalConfig("URL_NEW_VERSION")
            Dim Resultado As Respuesta = LeerURL(URL, Nothing, PingUserAgent, PingReferer)
            Log.WriteDebug("Ping finished")
        Catch ex As Exception
            ' No nos interesa lo que haga...
        End Try
    End Sub

    Private Shared Sub bgPingNewUser_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles bgPingNewUser.DoWork
        Try
            Log.WriteDebug("Starting ping new user...")
            Dim URL As String = InternalConfiguration.ObtenerValueFromInternalConfig("URL_NEW_USER")
            Dim Resultado As Respuesta = LeerURL(URL, Nothing, PingUserAgent, PingReferer)
            Log.WriteDebug("Ping finished")
        Catch ex As Exception
            ' No nos interesa lo que haga...
        End Try
    End Sub


    Private Shared Function PingUserAgent() As String
        Return "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)" ' IE 10
    End Function
    Private Shared Function PingReferer() As String
        Return "http://megadownloaderapp.blogspot.com"
    End Function
#End Region



End Class
