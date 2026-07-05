Public Class MegaFolderHelper


    Public Class FileListResponse
        Public e As String
        Public ok As Object
        Public u As Object
        Public sn As String
        Public f As Generic.List(Of FileNode)
    End Class

    Public Class FileNode
        Public h As String
        Public p As String
        Public u As String
        Public t As Integer
        Public a As String
        Public k As String
        Public s As Long
        Public ts As Long
    End Class

    Public Shared Function RetrieveLinksFromFolder(ByVal FolderID As String, ByVal FolderKey As String) As Generic.List(Of URLProcessor.FileURL)
        Dim jsonRQ As String
        Dim res As Conexion.Respuesta

        Dim FromENCLink As Boolean = FolderID.StartsWith(URLExtractor.FOLDERENCODEDPREFIX) Or FolderID.StartsWith(URLExtractor.FOLDERENCODEDPREFIX2)

        URLExtractor.CheckFileIDAndFileKey(FolderID, FolderKey)

        jsonRQ = "[{""a"":""f"",""c"":1,""r"":1}]"
        res = Conexion.SendJSON(Conexion.Get_MEGA_API_Url("") & "&n=" & FolderID, jsonRQ)

        If res.Excepcion IsNot Nothing Then
            Throw New ApplicationException("Error getting file list from shared folder - " & res.Excepcion.ToString)
        End If

        If IsNumeric(res.Mensaje) Then
            Throw MEGA_ErrorHandler.GetErrorFromMegaResponse(res.Mensaje, "getting file list from shared folder")
        End If

        Dim FileList As FileListResponse
        FileList = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(res.Mensaje.Trim("["c, "]"c), _
                                                      GetType(FileListResponse)),  _
                                                      FileListResponse)
        FileList = FileList

        Dim Results As New Generic.List(Of URLProcessor.FileURL)


        ' Get folder structure
        Dim htFolderEstructure As New Generic.Dictionary(Of String, KeyValuePair(Of String, String))
        Dim root As String = ""
        For Each fileN As FileNode In FileList.f
            If fileN.t = 1 Then
                Dim FileID As String = fileN.h
                Dim FileKey As String = fileN.k.Substring(fileN.k.IndexOf(":"c) + 1)

                FileKey = Criptografia.a32_to_base64(Criptografia.decrypt_key(Criptografia.base64_to_a32(FileKey), Criptografia.base64_to_a32(FolderKey)))
                Dim FolderName As String = PreSharedKeyManager.DecryptFileInfo(fileN.a, FileKey)

                Dim ex As New System.Text.RegularExpressions.Regex(Conexion.patternGetFileName)
                If Not String.IsNullOrEmpty(FolderName) AndAlso ex.IsMatch(FolderName) Then
                    Dim m As System.Text.RegularExpressions.Match = ex.Match(FolderName)
                    FolderName = m.Groups("FileName").Value
                Else
                    Continue For
                End If

                htFolderEstructure.Add(FileID, New KeyValuePair(Of String, String)(FolderName, If(FileID = fileN.k.Substring(0, fileN.k.IndexOf(":"c)), "", fileN.p)))

                If FileID = fileN.k.Substring(0, fileN.k.IndexOf(":"c)) Then root = FileID
            End If
        Next
        Dim htFolders As New Generic.Dictionary(Of String, String)
        FillFolderStructure(root, htFolders, htFolderEstructure)


        ' Get files
        For Each fileN As FileNode In FileList.f

            If fileN.t = 0 Then
                Dim FileKey As String = fileN.k.Substring(fileN.k.IndexOf(":"c) + 1)

                Dim path As String = String.Empty
                If htFolders.ContainsKey(fileN.p) Then
                    path = htFolders(fileN.p)
                End If

                FileKey = Criptografia.a32_to_base64(Criptografia.decrypt_key(Criptografia.base64_to_a32(FileKey), Criptografia.base64_to_a32(FolderKey)))
                Dim FileInfoDec As String = PreSharedKeyManager.DecryptFileInfo(fileN.a, FileKey)
                Try
                    Dim ex As New System.Text.RegularExpressions.Regex(Conexion.patternGetFileName)
                    If Not String.IsNullOrEmpty(FileInfoDec) AndAlso ex.IsMatch(FileInfoDec) Then
                        Dim m As System.Text.RegularExpressions.Match = ex.Match(FileInfoDec)
                        FileInfoDec = m.Groups("FileName").Value


                        '' Ya tenemos el FileID y el FileKey
                        'Dim FileID As String = "megafolder?" & FolderID & "?" & fileN.h
                        'Dim NuevoLink As String = URLExtractor.GenerateEncodedURILink(FileID, FileKey, False, False)
                        'Results.Add(New URLProcessor.FileURL(NuevoLink, path))

                        ' 25/1/15 Formato #N!
                        If FromENCLink Then
                            Dim NuevoLink As String = URLExtractor.GenerateEncodedURILink("N?" & fileN.h, FileKey & "=###n=" & FolderID, False, False)
                            Results.Add(New URLProcessor.FileURL(NuevoLink, path))
                        Else

                            Dim NuevoLink As String = String.Format("http://mega.co.nz/#N!{0}!{1}=###n={2}", fileN.h, FileKey, FolderID)
                            Results.Add(New URLProcessor.FileURL(NuevoLink, path))
                        End If

                    Else
                        Continue For
                    End If

                Catch exc As Exception ' Detect error reading file from folder
                    Throw
                End Try

            End If

        Next

        Return Results
    End Function

    Private Shared Sub FillFolderStructure(id As String, final As Generic.Dictionary(Of String, String), unprocessed As Generic.Dictionary(Of String, KeyValuePair(Of String, String)))
    
        If unprocessed.ContainsKey(id) Then

            Dim parent As String = unprocessed(id).Value
            If Not String.IsNullOrEmpty(parent) Then
                Dim parentPath As String = final(parent)
                final.Add(id, System.IO.Path.Combine(parentPath, unprocessed(id).Key))
            Else
                final.Add(id, "") ' primer nivel descartado
            End If

            ' examinar los hijos
            For Each son In (From n In unprocessed.Keys Where unprocessed(n).Value = id)
                FillFolderStructure(son, final, unprocessed)
            Next
        End If
    End Sub


End Class
