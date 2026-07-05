Imports System
Imports HttpServer
Imports System.IO
Imports HttpServer.Sessions
Imports HttpServer.HttpModules
Imports System.IO.Compression


Public Class StreamingLibraryModule
    Inherits HttpModule


    Public Downloader As Main
    Public Config As Configuracion


    Public Const PaginaMain As String = "/library"
    Public Const PaginaManagement As String = "/manage"
    Public Const PaginaAjax As String = "/ajax"
    Public Const PaginaLogin As String = "/login"


    Public TemplateManagerData As String
    Public TemplateData As String
    Public TemplateLogin As String
    Private _RespuestaAjax As String = ""
    Private _TimeoutSesion As Integer

    Public Sub New(ByRef Downloader As Main, ByRef Config As Configuracion)

        For Each resname As String In Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames
            If resname.EndsWith("StreamingLibraryManagerTemplateData.htm") Then
                Dim stream As Stream = Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream(resname)
                Dim reader As New StreamReader(stream)
                TemplateManagerData = reader.ReadToEnd
            ElseIf resname.EndsWith("StreamingLibraryTemplateData.htm") Then
                Dim stream As Stream = Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream(resname)
                Dim reader As New StreamReader(stream)
                TemplateData = reader.ReadToEnd
            ElseIf resname.EndsWith("StreamingLibraryTemplateLogin.htm") Then
                Dim stream As Stream = Reflection.Assembly.GetExecutingAssembly.GetManifestResourceStream(resname)
                Dim reader As New StreamReader(stream)
                TemplateLogin = reader.ReadToEnd
            End If
        Next


        Me._TimeoutSesion = 3600 * 24 * 7
        Me.Downloader = Downloader
        Me.Config = Config
    End Sub

   

    Public Overrides Function Process(request As HttpServer.IHttpRequest, response As HttpServer.IHttpResponse, session As HttpServer.Sessions.IHttpSession) As Boolean

        SetHeaders(response)

        ResetRequestVar(request)

        If Not ComprobarAcceso(request, response, session) Then Return False
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
        If Not String.IsNullOrEmpty(Me.Config.ServidorStreamingPassword) And Not UsuarioLogueado(session) And request.Uri.LocalPath <> PaginaLogin Then
            response.Redirect(PaginaLogin)
            Return False
        End If

        Select Case request.Uri.LocalPath
            Case PaginaMain, PaginaManagement, PaginaAjax, PaginaLogin
                Return True
            Case Else
                response.Redirect(PaginaMain)
                Return False
        End Select

    End Function

    Private Function UsuarioLogueado(ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        Dim resultado As Boolean = (session("Logueado") IsNot Nothing AndAlso CStr(session("Logueado")) = "1")
        If resultado And Me._TimeoutSesion > 0 And session("LoginDate") IsNot Nothing AndAlso TypeOf (session("LoginDate")) Is Date Then
            resultado = CDate(session("LoginDate")).AddSeconds(Me._TimeoutSesion) > Now
        End If
        Return resultado
    End Function

    Private Function ProcesarPagina(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        Select Case request.Uri.LocalPath
            Case PaginaLogin
                Return ProcesoLogin(request, response, session)
            Case PaginaAjax
                Return ProcesoAjax(request, response, session)
        End Select
        Return True
    End Function

    Private Sub PintarPagina(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession)

        Dim responseBody As New System.Text.StringBuilder


        Select Case request.Uri.LocalPath
            Case PaginaManagement
                responseBody.Append(TemplateManagerData)
            Case PaginaMain
                responseBody.Append(TemplateData)
            Case PaginaAjax
                responseBody.Append(CargarAjax)
            Case PaginaLogin
                responseBody.Append(TemplateLogin.Replace("%ERROR%", _Error))
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

    Private Function CargarAjax() As String
        Return _RespuestaAjax
    End Function


    Private Function ProcesoLogin(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean
        If request.Param IsNot Nothing Then
            If request.Param.Item("Password") IsNot Nothing And IsPostBack(request) Then
                If request.Param.Item("Password").Value = Me.Config.ServidorStreamingPassword Then
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


    Private Sub SetError(ByVal msj As String)
        _Error = "<br/><div class='error'>" & msj & "</div><br/>"
    End Sub


    Private Function ProcesoAjax(ByRef request As HttpServer.IHttpRequest, ByRef response As HttpServer.IHttpResponse, ByRef session As HttpServer.Sessions.IHttpSession) As Boolean

        'If Not IsPostBack(request) Then Return False

        Dim url As String = request.Uri.Authority

        If request.Param IsNot Nothing Then
            If request.Param.Item("Action") Is Nothing OrElse String.IsNullOrEmpty(request.Param.Item("Action").Value) Then
                PrepareAjaxResponse("Action not specified")
                Return True
            End If
            Select Case request.Param.Item("Action").Value

                Case "Load"
                    If request.Param.Item("Element") Is Nothing OrElse String.IsNullOrEmpty(request.Param.Item("Element").Value) Then
                        PrepareAjaxResponse("Element not specified")
                        Return True
                    End If

                    Dim Ele As LibraryElement = StreamingLibraryManager.GetElementByID(request.Param.Item("Element").Value)
                    If Ele Is Nothing Then
                        PrepareAjaxResponse("Element not found")
                        Return True
                    Else
                        PrepareAjaxResponse(url, Ele)
                    End If

                Case "Delete"

                    If request.Param.Item("ID") Is Nothing OrElse Not IsNumeric(request.Param.Item("ID").Value) Then
                        PrepareAjaxResponse("ID not specified")
                    End If

                    StreamingLibraryManager.RemoveElement(request.Param.Item("ID").Value)
                    PrepareAjaxResponse("")

                Case "Save"

                    If request.Param.Item("ID") Is Nothing Then
                        PrepareAjaxResponse("ID not specified") : Return True
                    ElseIf request.Param.Item("IMDB") Is Nothing Then
                        PrepareAjaxResponse("IMDB not specified") : Return True
                    ElseIf request.Param.Item("Allocine") Is Nothing Then
                        PrepareAjaxResponse("Allocine not specified") : Return True
                    ElseIf request.Param.Item("Filmaffinity") Is Nothing Then
                        PrepareAjaxResponse("Filmaffinity not specified") : Return True
                    ElseIf request.Param.Item("Name") Is Nothing Then
                        PrepareAjaxResponse("Name not specified") : Return True
                    ElseIf request.Param.Item("Desc") Is Nothing Then
                        PrepareAjaxResponse("Desc not specified") : Return True
                    ElseIf request.Param.Item("Poster") Is Nothing Then
                        PrepareAjaxResponse("Poster not specified") : Return True
                    ElseIf request.Param.Item("Comments") Is Nothing Then
                        PrepareAjaxResponse("Comments not specified") : Return True
                    ElseIf request.Param.Item("Link") Is Nothing OrElse String.IsNullOrEmpty(request.Param.Item("Link").Value) Then
                        PrepareAjaxResponse("Link not specified") : Return True
                    End If

                    If request.Param.Item("Link").Value <> LibraryElement.HIDDEN_LINK_DESC Then
                        If String.IsNullOrEmpty(Fichero.ExtraerFileID(request.Param.Item("Link").Value)) Then
                            PrepareAjaxResponse("Invalid link") : Return True
                        End If
                    End If

                    Dim LinkVisible As Boolean = True
                    Dim ID As String = request.Param.Item("ID").Value
                    Dim Name As String = request.Param.Item("Name").Value
                    Dim Desc As String = request.Param.Item("Desc").Value
                    Dim Comments As String = request.Param.Item("Comments").Value
                    Dim Poster As String = request.Param.Item("Poster").Value
                    Dim IMDB As String = request.Param.Item("IMDB").Value
                    Dim Filmaffinity As String = request.Param.Item("Filmaffinity").Value
                    Dim Allocine As String = request.Param.Item("Allocine").Value
                    Dim Link As String = request.Param.Item("Link").Value

                    MegaDownloader.IMDB.FillMissingFields(IMDB, Name, Poster, Desc)
                    MegaDownloader.Allocine.FillMissingFields(Allocine, Name, Poster, Desc)
                    MegaDownloader.Filmaffinity.FillMissingFields(Filmaffinity, Name, Poster, Desc)

                    If IsNumeric(ID) Then
                        Dim Ele As LibraryElement = StreamingLibraryManager.ModifyElement( _
                                 ID, _
                                 Name, _
                                 Desc, _
                                 Comments, _
                                 Poster, _
                                 Link, _
                                 LinkVisible, _
                                 IMDB, _
                                 Allocine, _
                                 Filmaffinity)
                        If Ele Is Nothing Then
                            PrepareAjaxResponse("Element not found")
                            Return True
                        Else
                            PrepareAjaxResponse(url, Ele)
                        End If
                    Else
                        ' New element
                        Dim Ele As LibraryElement = StreamingLibraryManager.AddElement( _
                                 Name, _
                                 Desc, _
                                 Comments, _
                                 Poster, _
                                 Link, _
                                 LinkVisible, _
                                 IMDB, _
                                 Allocine, _
                                 Filmaffinity)
                        If Ele Is Nothing Then
                            PrepareAjaxResponse("Element not created")
                            Return True
                        Else
                            PrepareAjaxResponse(url, Ele)
                        End If
                    End If


                Case "LoadAll"

                    PrepareAjaxResponse(url, StreamingLibraryManager.GetElements)

                Case "OpenVLC"
                    If request.Param.Item("URL") Is Nothing OrElse String.IsNullOrEmpty(request.Param.Item("URL").Value) Then
                        PrepareAjaxResponse("URL not specified")
                        Return True
                    End If

                    If Not StreamingHelper.WatchOnline(Config.VLCPath, request.Param.Item("URL").Value) Then
                        PrepareAjaxResponse("VLC could not be loaded")
                    End If
                    Return True


                Case "ImportLinks"
                    If request.Param.Item("Links") Is Nothing Then
                        PrepareAjaxResponse("Links not specified")
                        Return True
                    End If

                    Dim links As String = request.Param.Item("Links").Value

                    If StreamingLibraryManager.IsImportedLibrary(links) Then
                        PrepareAjaxResponseImport(-1, StreamingLibraryManager.ImportLibrary(links))
                        Return True
                    Else
                        Dim URLs As Generic.List(Of String) = URLExtractor.ExtraerURLs(Uri.UnescapeDataString(links))
                        PrepareAjaxResponseImport(URLs.Count, StreamingLibraryManager.ImportLinks(Me.Config, URLs))
                        Return True
                    End If
                 

                Case "ExportLinks"
                    If request.Param.Item("IDs") Is Nothing Then
                        PrepareAjaxResponse("IDs not specified")
                        Return True
                    End If

                    Dim HideLinks As Boolean = False
                    Dim PlainText As Boolean = False
                    If request.Param.Item("HideLinks") IsNot Nothing Then
                        Boolean.TryParse(request.Param.Item("HideLinks").Value, HideLinks)
                    End If
                    If request.Param.Item("PlainText") IsNot Nothing Then
                        Boolean.TryParse(request.Param.Item("PlainText").Value, PlainText)
                    End If

                    PrepareAjaxResponseExport(StreamingLibraryManager.ExportElements(request.Param.Item("IDs").Value.Split(";"c).ToList, HideLinks, PlainText))
                    Return True

                Case Else
                    PrepareAjaxResponse("Action " & request.Param.Item("Action").Value & " not recognized")
            End Select


        End If
        Return True
    End Function

    Private Sub PrepareAjaxResponseExport(code As String)
        _RespuestaAjax = "{""error"": """", ""code"": """ & code & """}"
    End Sub

    Private Sub PrepareAjaxResponseImport(ByVal numReceived As Integer, ByVal numImported As Integer)
        _RespuestaAjax = "{""error"": """", ""numR"": """ & numReceived.ToString & """, ""numI"": """ & numImported.ToString & """}"
    End Sub
    Private Sub PrepareAjaxResponse(CurrentURL As String, ByVal ele As LibraryElement)
        _RespuestaAjax = "{""error"": """", ""Data"": [" & ele.ToJSON(CurrentURL, Me.Config) & "]}"
    End Sub

    Private Sub PrepareAjaxResponse(CurrentURL As String, ByVal ele As IEnumerable(Of LibraryElement))
        Dim str As New System.Text.StringBuilder
        str.Append("{""error"": """", ""Data"": [")
        Dim primero As Boolean = True
        For Each e As LibraryElement In ele
            If Not primero Then str.Append(",")
            str.Append(e.ToJSON(CurrentURL, Me.Config))
            primero = False
        Next
        str.Append("]}")
        _RespuestaAjax = str.ToString
    End Sub


   
    Private Sub PrepareAjaxResponse(ByVal ErrorMessage As String)
        _RespuestaAjax = "{""error"": """ & JSONEscape(ErrorMessage) & """}"
    End Sub

    Private Function JSONEscape(str As String) As String
        Return (str & "").Replace("""", "\""")
    End Function


#End Region




End Class
