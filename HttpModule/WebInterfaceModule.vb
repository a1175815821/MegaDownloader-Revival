Imports System
Imports HttpServer
Imports System.IO
Imports HttpServer.Sessions
Imports HttpServer.HttpModules
Imports System.IO.Compression


Public Class WebInterfaceModule
    Inherits HttpModule


    Public Downloader As Main


    Public Const PaginaLogin As String = "/login"
    Public Const PaginaLogout As String = "/logout"
    Public Const PaginaMain As String = "/main"
    Public Const PaginaCSS As String = "/style.css"
    Public Const PaginaFavIcon As String = "/favicon.ico"
    Public Const PaginaAjaxStatus As String = "/ajax_status"
    Public Const PaginaAjaxStop As String = "/ajax_stopdownload"
    Public Const PaginaAjaxPlay As String = "/ajax_resumedownload"
    Public Const PaginaAjaxAddLink As String = "/ajax_addlink"

    Public Const CONST_CONTENT As String = "%CONTENT%"
    Public Const CONST_HEAD As String = "%HEAD%"
    Public Const CONST_JAVASCRIPT As String = "%JAVASCRIPT%"
    Public Const CONST_TITLE As String = "%TITLE%"
    Public Const CONST_ERROR As String = "%ERROR%"


    Public XmlTemplateData As Xml.XmlDocument = Nothing
    Private _RespuestaAjax As String = ""

    Private _Password As String
    Private _TitlePersonalizado As String
    Private _TimeoutSesion As Integer
    Private _Language As String

    Public Sub New(ByRef Downloader As Main, _
                   ByVal TemplatePath As String, _
                   ByVal Password As String, _
                   ByVal TituloVentana As String, _
                   ByVal TimeOutSesion As Integer, _
                   ByVal LanguageCode As String)
        If String.IsNullOrEmpty(TemplatePath) OrElse Not File.Exists(TemplatePath) Then

            For Each resname As String In Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames
                If resname.EndsWith("XmlTemplateData.xml") Then
                    Dim stream As Stream = Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream(resname)
                    Dim reader As New StreamReader(stream)
                    XmlTemplateData = New Xml.XmlDocument
                    XmlTemplateData.LoadXml(reader.ReadToEnd)
                    Exit For
                End If
            Next
        Else
            XmlTemplateData = New Xml.XmlDocument
            XmlTemplateData.Load(TemplatePath)
        End If


        CheckTemplate()

        If String.IsNullOrEmpty(TituloVentana) Then
            Me._TitlePersonalizado = InternalConfiguration.ObtenerNombreApp & InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER")
        Else
            Me._TitlePersonalizado = TituloVentana
        End If
        Me.Downloader = Downloader
        Me._TimeoutSesion = TimeOutSesion
        Me._Language = LanguageCode
        Me._Password = MD5Utils.MD5CalcString(Password)
    End Sub

    Private Sub CheckTemplate()
        If XmlTemplateData Is Nothing Then Throw New ApplicationException("Error template not found")
        If XmlTemplateData.DocumentElement.SelectSingleNode("Template/HtmlTemplate") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Template/HtmlTemplate")
        End If
        If XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIcon") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Template/FavIcon")
        End If
        If XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIconMimeType") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Template/FavIconMimeType")
        End If
        If XmlTemplateData.DocumentElement.SelectSingleNode("Content/HeadMain") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Content/HeadMain")
        End If
        If XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyMain") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Content/BodyMain")
        End If
        If XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyJavaScript") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Content/BodyJavaScript")
        End If
        If XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyLogin") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Content/BodyLogin")
        End If
        If XmlTemplateData.DocumentElement.SelectSingleNode("Content/CSS") Is Nothing Then
            Throw New ApplicationException("Template is not correct, missing node Content/CSS")
        End If

    End Sub



    Public Overrides Function Process(request As HttpServer.IHttpRequest, response As HttpServer.IHttpResponse, session As HttpServer.Sessions.IHttpSession) As Boolean

        SetHeaders(response)

        ResetRequestVar(request)

        If Not ComprobarAcceso(request, response, session) Then Return True
        If Not ProcesarPagina(request, response, session) Then Return True
        PintarPagina(request, response, session)

        Return True
    End Function

#Region "Funciones varias privadas"
    Private _Error As String
    Private Function IsPostBack(ByRef request As HttpServer.IHttpRequest) As Boolean
        Return request.Method = "POST"
    End Function



    Private Sub ResetRequestVar(ByRef request As HttpServer.IHttpRequest)
        If Not IsPostBack(request) Then ' Pedimos datos; si es un POST, es que enviamos al servidor
            _Error = ""
        End If
    End Sub

    Private Sub SetHeaders(ByRef response As HttpServer.IHttpResponse)
        response.AddHeader("Server", "Internal")
    End Sub
    Private Sub SetCSSControlCache(ByRef response As HttpServer.IHttpResponse)
        response.AddHeader("Cache-Control", "public, max-age=604800") ' Una semana, 3600*24*7
    End Sub


    Private Function UsuarioLogueado(ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        Dim resultado As Boolean = (session("Logueado") IsNot Nothing AndAlso CStr(session("Logueado")) = "1")
        If resultado And Me._TimeoutSesion > 0 And session("LoginDate") IsNot Nothing AndAlso TypeOf (session("LoginDate")) Is Date Then
            resultado = CDate(session("LoginDate")).AddSeconds(Me._TimeoutSesion) > Now
        End If
        Return resultado
    End Function

#End Region


    ''' <summary>
    ''' Examina el usuario y la página solicitada para redirigir a donde haga falta.
    ''' </summary>
    ''' <param name="request"></param>
    ''' <param name="response"></param>
    ''' <param name="session"></param>
    ''' <returns>Devuelve true si se puede que continuar, false si hay que abortar</returns>
    ''' <remarks></remarks>
    Private Function ComprobarAcceso(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean


        ' Miramos si hace falta loguearse
        Dim PaginaRequiereLogueo As Boolean = False
        Dim PaginaAjax As Boolean = False
        Select Case request.Uri.LocalPath
            Case PaginaMain
                PaginaRequiereLogueo = True
            Case PaginaAjaxStatus, PaginaAjaxStop, PaginaAjaxPlay, PaginaAjaxAddLink
                PaginaRequiereLogueo = True
                PaginaAjax = True
        End Select


        If Not UsuarioLogueado(session) And PaginaRequiereLogueo Then
            If PaginaAjax Then
                Dim writer As New StreamWriter(response.Body)
                writer.Write(ErrorAjax(Language.GetText("Error, session expired")))
                writer.Flush()
                Return False
            Else
                response.Redirect(PaginaLogin)
                Return False
            End If

        Else

            ' Comprobamos que la página sea válida, sino lo enviamos a main
            Select Case request.Uri.LocalPath
                Case PaginaLogin, PaginaLogout, PaginaMain, PaginaCSS, PaginaFavIcon, _
                     PaginaAjaxStatus, PaginaAjaxPlay, PaginaAjaxStop, PaginaAjaxAddLink
                    Return True
                Case Else
                    response.Redirect(PaginaMain)
                    Return False
            End Select
        End If

    End Function

    Private Function ProcesarPagina(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        Select Case request.Uri.LocalPath
            Case PaginaLogin
                Return ProcesoLogin(request, response, session)
            Case PaginaLogout
                Return ProcesoLogout(request, response, session)
            Case PaginaAjaxAddLink
                Return ProcesoAddLinks(request, response, session)
            Case PaginaAjaxStop
                Return ProcesoStop(request, response, session)
            Case PaginaAjaxPlay
                Return ProcesoPlay(request, response, session)
            Case PaginaAjaxStatus
                Return ProcesoStatus(request, response, session)
        End Select
        Return True
    End Function

    Private Sub PintarPagina(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession)
        ' Icono: (no es texto así que lo tratamos de forma distinta)
        Select Case request.Uri.LocalPath
            Case PaginaFavIcon
                response.AddHeader("Content-Type", FavIconMimeType)
                Dim writer As New BinaryWriter(response.Body)
                writer.Write(Convert.FromBase64String(FavIconBase64))
                writer.Flush()
                SetCSSControlCache(response)
                Exit Sub
        End Select


        Dim responseBody As New System.Text.StringBuilder


        Select Case request.Uri.LocalPath
            Case PaginaAjaxStatus
                responseBody.Append(CargarAjax)
            Case PaginaAjaxPlay
                responseBody.Append(CargarAjax)
            Case PaginaAjaxStop
                responseBody.Append(CargarAjax)
            Case PaginaAjaxAddLink
                responseBody.Append(CargarAjax)
            Case PaginaCSS
                response.AddHeader("Content-Type", "text/css")
                responseBody.Append(CSS)
                SetCSSControlCache(response)
            Case PaginaLogin
                responseBody.Append(PintarPaginaLogin)
            Case PaginaMain
                responseBody.Append(PintarPaginaMain)

        End Select

        ComprimirRespuesta(request, response, responseBody.ToString)
    End Sub

    Private Sub ComprimirRespuesta(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef ResponseBody As String)

        Dim acceptEncoding As String = request.Headers("Accept-Encoding")

        If Not String.IsNullOrEmpty(acceptEncoding) AndAlso acceptEncoding.ToLower.Contains("gzip") Then
            response.AddHeader("Content-Encoding", "gzip")


            Dim bytes As Byte() = Text.UTF8Encoding.UTF8.GetBytes(ResponseBody)
            Using memWriter As New MemoryStream
                Using writer As New GZipStream(memWriter, CompressionMode.Compress)
                    writer.Write(bytes, 0, bytes.Length)
                    writer.Flush()
                End Using
                bytes = memWriter.ToArray
                response.Body.Write(bytes, 0, bytes.Length)
            End Using
        Else
            Dim writer As New StreamWriter(response.Body)
            writer.Write(ResponseBody)
            writer.Flush()
            ' No tenemos que cerrar el stream sino da error al ejecutar
        End If
    End Sub

#Region "Proceso páginas"

    Private Function ProcesoStatus(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        _RespuestaAjax = "<span class='StatusMuyImportante'><strong>" & Language.GetText("Status") & "</strong>: "
        Select Case Downloader.ControlRemotoObtenerEstado
            Case Main.TipoEstadoAplicacion.Descargando
                _RespuestaAjax &= "<span class='iconPlay'>" & Language.GetText("Downloading") & "</span>"
            Case Main.TipoEstadoAplicacion.Parado
                _RespuestaAjax &= "<span class='iconPause'>" & Language.GetText("Stopped") & "</span>"
            Case Main.TipoEstadoAplicacion.Pausa
                _RespuestaAjax &= "<span class='iconPause'>" & Language.GetText("Paused") & "</span>"

        End Select
        _RespuestaAjax &= "</span><br/>" & vbNewLine
        _RespuestaAjax &= "<span class='StatusImportante'><strong>" & Language.GetText("Speed") & "</strong>: "
        Dim v As Decimal? = Downloader.ControlRemotoObtenerVelocidad
        If v.HasValue Then
            _RespuestaAjax &= PintarVelocidadDescarga(v.Value)
        Else
            _RespuestaAjax &= "-"
        End If
        _RespuestaAjax &= "</span><br/>" & vbNewLine
        _RespuestaAjax &= "<span class='StatusImportante'><strong>" & Language.GetText("Active downloads") & "</strong>: "
        Dim da As Integer? = Downloader.ControlRemotoObtenerDescargasActivas
        If da.HasValue Then
            _RespuestaAjax &= da.Value.ToString
        Else
            _RespuestaAjax &= "-"
        End If
        _RespuestaAjax &= "</span><br/>" & vbNewLine
        _RespuestaAjax &= "<span><strong>" & Language.GetText("Queued, error, completed") & "</strong>: <span style='white-space:nowrap;'>"
        Dim dec As Integer? = Downloader.ControlRemotoObtenerDescargasEnCola
        Dim de As Integer? = Downloader.ControlRemotoObtenerDescargasErroneas
        Dim dc As Integer? = Downloader.ControlRemotoObtenerDescargasCompletadas
        If dec.HasValue Then
            _RespuestaAjax &= dec.Value.ToString & " / "
        Else
            _RespuestaAjax &= "- / "
        End If
        If de.HasValue Then
            _RespuestaAjax &= de.Value.ToString & " / "
        Else
            _RespuestaAjax &= "- / "
        End If
        If dc.HasValue Then
            _RespuestaAjax &= dc.Value.ToString
        Else
            _RespuestaAjax &= "-"
        End If
        _RespuestaAjax &= "</span></span><br/>" & vbNewLine
        _RespuestaAjax &= "<span><strong>" & Language.GetText("Hour") & "</strong>: "
        _RespuestaAjax &= Now.ToString("HH:mm:ss")
        _RespuestaAjax &= "</span>"

        Return True
    End Function

    Private Shared Function PintarVelocidadDescarga(vel As Decimal) As String
        Dim Dato As String = "KB/s"
        If vel > 1024 Then
            Dato = "MB/s"
            vel = vel / 1024
        End If
        Return vel.ToString("F2") & " " & Dato
    End Function

    Private Function ProcesoStop(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        Downloader.ControlRemotoParar()
        System.Threading.Thread.Sleep(400)
        _RespuestaAjax = Language.GetText("Download stopped")
        Return True
    End Function

    Private Function ProcesoPlay(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        Downloader.ControlRemotoDescargar()
        System.Threading.Thread.Sleep(400)
        _RespuestaAjax = Language.GetText("Download started")
        Return True
    End Function

    Private Function ProcesoLogin(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        If request.Param IsNot Nothing Then
            If request.Param.Item("Password") IsNot Nothing And IsPostBack(request) Then
                If MD5Utils.MD5CalcString(request.Param.Item("Password").Value) = Me._Password Then
                    session("Logueado") = "1"
                    session("LoginDate") = Now
                    response.Redirect(PaginaMain)
                    Return False
                Else
                    SetError(Language.GetText("Invalid password"))
                End If
            End If
        End If
        Return True
    End Function

    Private Function ProcesoLogout(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        session("Logueado") = "0"
        response.Redirect(PaginaLogin)
        Return False
    End Function

    Private Function ProcesoAddLinks(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        _RespuestaAjax = ErrorAjax(Language.GetText("Invalid links")) ' Por defecto mostramos un error, si va bien lo cambiamos
        If request.Param IsNot Nothing Then
            If request.Param.Item("links") IsNot Nothing And _
               request.Param.Item("pckname") IsNot Nothing And _
               request.Param.Item("createdirpck") IsNot Nothing And _
               IsPostBack(request) Then

                Dim links As String = request.Param.Item("links").Value
                Dim createdirpck As String = request.Param.Item("createdirpck").Value
                Dim pckname As String = request.Param.Item("pckname").Value

                System.Threading.Thread.Sleep(400)

                If String.IsNullOrEmpty(links) Then
                    _RespuestaAjax = ErrorAjax(Language.GetText("Invalid links"))
                ElseIf createdirpck = "true" And String.IsNullOrEmpty(pckname) Then
                    _RespuestaAjax = ErrorAjax(Language.GetText("Invalid package name"))
                Else
                    Dim respuesta As String = Downloader.ControlRemotoAgregarLinks(links, pckname, createdirpck = "true")
                    If String.IsNullOrEmpty(respuesta) Then
                        _RespuestaAjax = Language.GetText("Links added successfully")
                    Else
                        _RespuestaAjax = ErrorAjax(respuesta)
                    End If

                End If
            End If
        End If
        Return True
    End Function


#End Region

#Region "Pintado"

    Private Function PintarComun(ByRef str As String) As String
        Return str.Replace(CONST_CONTENT, "").Replace(CONST_ERROR, _Error).Replace(CONST_HEAD, "").Replace(CONST_JAVASCRIPT, "").Replace(CONST_TITLE, "")
    End Function

    Private Function PintarPaginaLogin() As String
        Return PintarComun(Template.Replace(CONST_CONTENT, BodyLogin).Replace(CONST_HEAD, HeadMain(False)).Replace(CONST_TITLE, "Login"))
    End Function
    Private Function PintarPaginaMain() As String
        Return PintarComun(Template.Replace( _
                            CONST_CONTENT, BodyMain).Replace( _
                            CONST_JAVASCRIPT, BodyJavaScript).Replace( _
                            CONST_HEAD, HeadMain(True)).Replace( _
                            CONST_TITLE, Me._TitlePersonalizado))
    End Function

#End Region

#Region "Carga templates y ficheros"

    Private Function FavIconMimeType() As String
        Return XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIconMimeType").InnerText
    End Function
    Private Function FavIconBase64() As String
        Return XmlTemplateData.DocumentElement.SelectSingleNode("Template/FavIcon").InnerText
    End Function

    Private Function CSS() As String
        Return XmlTemplateData.DocumentElement.SelectSingleNode("Content/CSS").InnerText
    End Function

    Private Function Template() As String
        Return XmlTemplateData.DocumentElement.SelectSingleNode("Template/HtmlTemplate").InnerText
    End Function


    Private Function HeadMain(ByVal Logueado As Boolean) As String
        Dim str As String = ""
        If Logueado Then
            str &= "<link rel=""shortcut icon"" href=""/favicon.ico"" type=""" & FavIconMimeType() & """ />" & vbNewLine
        End If
        str &= XmlTemplateData.DocumentElement.SelectSingleNode("Content/HeadMain").InnerText
        Return str
    End Function
    Private Function BodyJavaScript() As String
        Return XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyJavaScript").InnerText
    End Function
    Private Function BodyMain() As String
        Return XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyMain").InnerText
    End Function

    Private Function BodyLogin() As String
        Return XmlTemplateData.DocumentElement.SelectSingleNode("Content/BodyLogin").InnerText
    End Function




    Private Function CargarAjax() As String
        Return _RespuestaAjax
    End Function


    Private Sub SetError(ByVal msj As String)
        _Error = "<br/><div class='error'>" & msj & "</div><br/>"
    End Sub

    Private Function ErrorAjax(msj As String) As String
        Return "<span class='error'>" & msj & "</span>"
    End Function
#End Region




End Class
