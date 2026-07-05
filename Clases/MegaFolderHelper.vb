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

        ' 找到文件夹本身的内部 handle (root)
        ' root 节点的特征: t=1, 且 fileN.h 出现在自己的 k 字段的 handle 部分
        ' MEGA API 中 k 字段格式可能为 "handle1:key1/handle2:key2/..." (多 share key)
        Dim root As String = ""
        For Each fileN As FileNode In FileList.f
            If fileN.t = 1 AndAlso Not String.IsNullOrEmpty(fileN.k) AndAlso fileN.k.Contains(":"c) Then
                ' 检查 k 字段中是否有 handle 等于 fileN.h 的对 (即文件自己的 handle)
                Dim keyForSelf As String = ExtractKeyFromK(fileN.k, fileN.h)
                If Not String.IsNullOrEmpty(keyForSelf) Then
                    root = fileN.h
                    Exit For
                End If
            End If
        Next


        ' Get folder structure
        Dim htFolderEstructure As New Generic.Dictionary(Of String, KeyValuePair(Of String, String))
        For Each fileN As FileNode In FileList.f
            If fileN.t = 1 Then
                Dim FileID As String = fileN.h

                ' 从 k 字段提取与 root handle 匹配的 key (用于用 FolderKey 解密)
                ' 如果没有 root,则回退到第一个 key
                Dim FileKey As String = ExtractKeyFromK(fileN.k, root)
                If String.IsNullOrEmpty(FileKey) Then Continue For

                Try
                    FileKey = Criptografia.a32_to_base64(Criptografia.decrypt_key(Criptografia.base64_to_a32(FileKey), Criptografia.base64_to_a32(FolderKey)))
                Catch exCrypt As Exception
                    ' 解密失败,跳过此节点 (可能是 k 字段格式不兼容)
                    Continue For
                End Try

                Dim FolderName As String = PreSharedKeyManager.DecryptFileInfo(fileN.a, FileKey)

                Dim ex As New System.Text.RegularExpressions.Regex(Conexion.patternGetFileName)
                If Not String.IsNullOrEmpty(FolderName) AndAlso ex.IsMatch(FolderName) Then
                    Dim m As System.Text.RegularExpressions.Match = ex.Match(FolderName)
                    FolderName = m.Groups("FileName").Value
                Else
                    Continue For
                End If

                ' 父级 handle: 如果 fileN.h == root,说明这是根文件夹本身,没有父级
                Dim parent As String = If(fileN.h = root, "", fileN.p)
                htFolderEstructure.Add(FileID, New KeyValuePair(Of String, String)(FolderName, parent))

            End If
        Next
        Dim htFolders As New Generic.Dictionary(Of String, String)
        FillFolderStructure(root, htFolders, htFolderEstructure)


        ' Get files
        For Each fileN As FileNode In FileList.f

            If fileN.t = 0 Then
                ' 从 k 字段提取与 root handle 匹配的 key
                Dim FileKey As String = ExtractKeyFromK(fileN.k, root)
                If String.IsNullOrEmpty(FileKey) Then Continue For

                Dim path As String = String.Empty
                If htFolders.ContainsKey(fileN.p) Then
                    path = htFolders(fileN.p)
                End If

                Try
                    FileKey = Criptografia.a32_to_base64(Criptografia.decrypt_key(Criptografia.base64_to_a32(FileKey), Criptografia.base64_to_a32(FolderKey)))
                Catch exCrypt As Exception
                    ' 解密失败,跳过此文件 (可能无法用 FolderKey 解密)
                    Continue For
                End Try

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

    ' 从 MEGA API 的 k 字段中提取指定 handle 对应的 key
    ' k 字段格式: "handle1:key1" 或 "handle1:key1/handle2:key2/handle3:key3"
    ' 当文件被多个用户分享时,会出现多个 handle:key 对
    Private Shared Function ExtractKeyFromK(kField As String, handle As String) As String
        If String.IsNullOrEmpty(kField) OrElse Not kField.Contains(":"c) Then Return ""

        ' 如果 k 字段包含 / (多个 handle:key 对)
        If kField.Contains("/"c) Then
            Dim parts() As String = kField.Split("/"c)

            ' 优先匹配指定的 handle
            If Not String.IsNullOrEmpty(handle) Then
                For Each part As String In parts
                    Dim colonIdx As Integer = part.IndexOf(":"c)
                    If colonIdx > 0 Then
                        Dim h As String = part.Substring(0, colonIdx)
                        If h = handle Then
                            Return part.Substring(colonIdx + 1)
                        End If
                    End If
                Next
            End If

            ' 回退:返回第一个有效 key
            For Each part As String In parts
                Dim colonIdx As Integer = part.IndexOf(":"c)
                If colonIdx > 0 Then
                    Dim key As String = part.Substring(colonIdx + 1)
                    If Not String.IsNullOrEmpty(key) Then Return key
                End If
            Next
            Return ""
        Else
            ' 单个 handle:key 对
            Return kField.Substring(kField.IndexOf(":"c) + 1)
        End If
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
