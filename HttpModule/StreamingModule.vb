
Imports System
Imports HttpServer
Imports System.IO
Imports HttpServer.Sessions
Imports HttpServer.HttpModules
Imports System.IO.Compression
Imports System.Net


Public Class StreamingModule
    Inherits HttpModule


    Private _Config As Configuracion
    Public Const PaginaStreaming As String = "/streaming"

    Public Sub New(ByRef Config As Configuracion)
        _Config = Config
    End Sub



    Private Shared Urls As New Generic.Dictionary(Of String, KeyValuePair(Of Date, Conexion.InformacionFichero))

    Public Overrides Function Process(request As HttpServer.IHttpRequest, _
        response As HttpServer.IHttpResponse, _
        session As HttpServer.Sessions.IHttpSession) As Boolean

        Try

            If Not IsStreaming(request) Then Return False


            If Not String.IsNullOrEmpty(Me._Config.ServidorStreamingPassword) Then
                If request.Param.Item("p") Is Nothing OrElse request.Param.Item("p").Value <> Me._Config.ServidorStreamingPassword Then
                    ComprimirRespuesta(request, response, "Error: Access denied")
                    Return True
                End If
            End If
        

            Dim FileKey As String = String.Empty
            Dim FileID As String = String.Empty
            If request.Param.Item("mega") IsNot Nothing AndAlso Not String.IsNullOrEmpty(request.Param.Item("mega").Value) Then
                ' http://localhost:5000/streaming?mega=!vNFx2D4Y!a1uUcgUF0OV6quLPf2UT83PvGKo_VvTgG4hl-yk0FdU
                FileKey = ExtraerStreamingFileKey(request.Uri.PathAndQuery)
                FileID = ExtraerStreamingFileID(request.Uri.PathAndQuery)
            ElseIf request.Param.Item("id") IsNot Nothing AndAlso Not String.IsNullOrEmpty(request.Param.Item("id").Value) Then
                Dim ele As LibraryElement = StreamingLibraryManager.GetElementByID(request.Param.Item("id").Value)
                If ele Is Nothing Then
                    ComprimirRespuesta(request, response, "Invalid ID")
                    Return True
                End If
                Dim link As String = Criptografia.ToInsecureString(ele.Link)
                FileKey = Fichero.ExtraerFileKey(link)
                FileID = Fichero.ExtraerFileID(link)
            ElseIf request.Param.Item("t") IsNot Nothing AndAlso Not String.IsNullOrEmpty(request.Param.Item("t").Value) Then
                If Not StreamingHelper.GetFileDataFromTempID(request.Param.Item("t").Value, FileID, FileKey) Then
                    ComprimirRespuesta(request, response, "Invalid ID")
                    Return True
                End If
            End If


            If String.IsNullOrEmpty(FileID) Then
                ComprimirRespuesta(request, response, "Missing FileID and/or FileKey")
                Return True
            End If

            Dim PathQuery As String = request.Uri.PathAndQuery

            Dim InfoFichero As Conexion.InformacionFichero
            If Not Urls.ContainsKey(PathQuery) OrElse Urls(PathQuery).Key < Now Then
                InfoFichero = Conexion.ObtenerInformacionFichero(Me._Config, FileID, FileKey, True)
                If InfoFichero.Err <> Conexion.TipoError.SinErrores Then
                    ComprimirRespuesta(request, response, "Error: " & InfoFichero.Errtxt)
                    Return True
                Else
                    FileKey = InfoFichero.FileKey
                    FileID = InfoFichero.FileID
                End If
                Urls(PathQuery) = New KeyValuePair(Of Date, Conexion.InformacionFichero)(Now.AddMinutes(3), InfoFichero)
            Else
                InfoFichero = CType(Urls(PathQuery).Value, Conexion.InformacionFichero)
                FileKey = InfoFichero.FileKey
                FileID = InfoFichero.FileID
            End If

            ' Folder links (new format)
            If FileKey.Contains("=###n=") Then
                FileKey = FileKey.Substring(0, FileKey.IndexOf("=###n="))
            End If


            ' Iniciamos la descarga!!
            Dim webReq As HttpWebRequest = Nothing
            Dim webResp As HttpWebResponse = Nothing
            Dim oCipher As Criptografia.SicSeekableBlockCipher = Criptografia.GetInstaceCipher(FileKey)

            Try

                ' Range adjustment
                Dim rangeStart As Long = 0
                Dim rangeEnd As Long = 0
                Dim requestRangeStart As Long = 0
                Dim requestRangeEnd As Long = 0
                Dim content As Long = 0
                RangeAdjust(rangeStart, rangeEnd, requestRangeStart, requestRangeEnd, content, request, response, InfoFichero.Tamano)


                'response.AddHeader("Content-Disposition", "attachment; filename=" & InfoFichero.Nombre)
                Dim contentDispositionHeader As New Mime.ContentDisposition()
                contentDispositionHeader.FileName = Utils.RemoveDiacritics(InfoFichero.Nombre)
                response.AddHeader("Content-Disposition", contentDispositionHeader.ToString)

                response.AddHeader("Cache-Control", "private")

                response.ContentLength = content 'InfoFichero.Tamano 
                response.ContentType = "application/octet-stream"
                response.Status = HttpStatusCode.PartialContent
                response.Connection = ConnectionType.Close



                webReq = Conexion.CreateHttpWebRequest(InfoFichero.URL)

                ' Pequeño hack para soportar ficheros de más de 2 GB:
                ' http://forums.codeguru.com/showthread.php?467570-WebRequest.AddRange-what-about-files-gt-2gb&p=1794639
                ' http://www.freesoft.org/CIE/RFC/2068/198.htm
                Dim key As String = "Range"
                Dim val As String = String.Format("bytes={0}-{1}", rangeStart, rangeEnd)

                Dim method As Reflection.MethodInfo = GetType(WebHeaderCollection).GetMethod("AddWithoutValidate", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
                method.Invoke(webReq.Headers, New Object() {key, val})

                webResp = CType(webReq.GetResponse, HttpWebResponse)


                oCipher.IncrementCounter(CInt(Math.Ceiling(rangeStart / oCipher.GetBlockSize)))



                Dim PackageSize As Integer = 1024 * 8 * 2
                Dim DownloadIndex As Long = 0
                Dim currentPackageSize As Integer = 0
                Dim readBytes(PackageSize - 1) As Byte

                Dim Stream As Stream = webResp.GetResponseStream()
                Try
                    response.SendHeaders()

                    'While DownloadIndex < InfoFichero.Tamano And ClientConnected(response)
                    While DownloadIndex < content And ClientConnected(response)

                        currentPackageSize = Stream.Read(readBytes, 0, PackageSize)

                        ' Forzamos a que haya bloques de 16 bytes
                        Dim diff As Integer = oCipher.GetBlockSize - currentPackageSize Mod oCipher.GetBlockSize
                        If diff <> oCipher.GetBlockSize Then
                            If DownloadIndex + currentPackageSize < content Then
                                currentPackageSize += Stream.Read(readBytes, currentPackageSize, diff)
                            Else
                                currentPackageSize += diff
                            End If
                        End If

                        Dim Buffer(currentPackageSize - 1) As Byte
                        For tempIndex As Integer = 0 To currentPackageSize - 1 Step oCipher.GetBlockSize
                            oCipher.ProcessBlock(readBytes, tempIndex, Buffer, tempIndex)
                        Next

                        If DownloadIndex = 0 Then
                            Buffer = Buffer.ToList().GetRange(CInt(requestRangeStart - rangeStart), Buffer.Length - CInt(requestRangeStart - rangeStart)).ToArray()
                        End If

                        response.SendBody(Buffer)

                        DownloadIndex += currentPackageSize

                    End While
                Catch ex As Exception
                    Dim ErrMsg As String = ex.ToString
                    Throw
                Finally
                    Stream.Close()
                End Try


            Catch ex As Exception
                Dim ErrMsg As String = ex.ToString
                Throw
            Finally

                If webResp IsNot Nothing Then webResp.Close()
            End Try
            Return True

        Catch ex As Exception
            Log.WriteError("Error in StreamingModule: " & ex.ToString)
            Throw
        End Try

    End Function


    Private Sub RangeAdjust(ByRef rangeStart As Long, ByRef rangeEnd As Long, _
        ByRef requestRangeStart As Long, ByRef requestRangeEnd As Long, _
        ByRef content As Long, _
        request As HttpServer.IHttpRequest, response As HttpServer.IHttpResponse, _
        FileSize As Long)
        rangeStart = 0
        rangeEnd = 0

        If Not String.IsNullOrEmpty(request.Headers("Range")) Then
            Dim mRange = System.Text.RegularExpressions.Regex.Match(request.Headers("Range"), "bytes=(\d*)-(\d*)")
            If mRange.Success Then
                'range = mRange.Groups[1].Value + "-" + mRange.Groups[2].Value;
                If Not String.IsNullOrEmpty(mRange.Groups(1).Value) Then
                    Long.TryParse(mRange.Groups(1).Value, rangeStart)
                End If
                If Not String.IsNullOrEmpty(mRange.Groups(2).Value) Then
                    Long.TryParse(mRange.Groups(2).Value, rangeEnd)
                End If
            End If
        End If

        requestRangeStart = rangeStart
        requestRangeEnd = rangeEnd

        If rangeStart Mod 16 <> 0 Then
            rangeStart = rangeStart - (rangeStart Mod 16)
        End If
        If rangeEnd Mod 16 <> 0 Then
            rangeEnd = (rangeEnd - (rangeEnd Mod 16)) + 16
        End If
        If rangeStart = rangeEnd AndAlso rangeStart <> 0 Then
            rangeEnd += 16
        End If


        rangeEnd = If(rangeEnd = 0, FileSize - 1, rangeEnd)
        requestRangeEnd = (If(requestRangeEnd > 0, requestRangeEnd, FileSize - 1))

        content = (requestRangeEnd - requestRangeStart) + 1

        response.AddHeader("Content-Length", content.ToString())
        response.ContentType = "application/octet-stream"

        If Not String.IsNullOrEmpty(request.Headers("Range")) Then
            response.AddHeader("Accept-Ranges", "bytes")
            response.AddHeader("Content-Range", "bytes " & requestRangeStart & "-" & requestRangeEnd.ToString() & "/" + (FileSize).ToString())
        End If
        ' / Range adjustment
    End Sub

    Private Function ClientConnected(response As HttpServer.IHttpResponse) As Boolean
        Dim fi As Reflection.FieldInfo = response.GetType.GetField("_context", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        Dim Contexto As Object = fi.GetValue(response)
        Dim pi As Reflection.PropertyInfo = Contexto.GetType.GetProperty("Stream", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        Dim St As System.IO.Stream = CType(pi.GetValue(Contexto, Nothing), System.IO.Stream)
        pi = St.GetType.GetProperty("Connected", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        Return CBool(pi.GetValue(St, Nothing))
    End Function

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


    Private Function IsStreaming(ByRef request As HttpServer.IHttpRequest) As Boolean
        Return request.Uri.LocalPath = PaginaStreaming
    End Function


    Public Shared Function ExtraerStreamingFileKey(ByVal URL As String) As String
        If String.IsNullOrEmpty(URL) Then Return ""
        If Not URL.ToLower.Contains("?mega=") Then Return ""
        URL = URL.Substring(URL.IndexOf("?") + 6)
        If URL.Split("!"c).Length <> 3 AndAlso Not String.IsNullOrEmpty(URL.Split("!"c)(0)) Then Return ""

        Return URL.Split("!"c)(2)
    End Function
    Public Shared Function ExtraerStreamingFileID(ByVal URL As String) As String
        If String.IsNullOrEmpty(URL) Then Return ""
        If Not URL.ToLower.Contains("?mega=") Then Return ""
        URL = URL.Substring(URL.IndexOf("?") + 6)
        If URL.Split("!"c).Length <> 3 AndAlso Not String.IsNullOrEmpty(URL.Split("!"c)(0)) Then Return ""

        Return URL.Split("!"c)(1)
    End Function


End Class