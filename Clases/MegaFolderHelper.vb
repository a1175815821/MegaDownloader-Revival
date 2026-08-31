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

    ''' <summary>
    ''' Retrieves the download links of the files inside a shared MEGA folder.
    ''' </summary>
    ''' <param name="FolderID">Root folder handle (from the link).</param>
    ''' <param name="FolderKey">Root folder key (from the link).</param>
    ''' <param name="SubFolderID">Optional subfolder handle from a modern link
    ''' (mega.nz/folder/&lt;id&gt;#&lt;key&gt;/folder/&lt;subID&gt;). When set, only the files
    ''' inside that subfolder are returned, with paths relative to the subfolder.</param>
    ''' <param name="SubFileID">Optional file handle from a modern link
    ''' (mega.nz/folder/&lt;id&gt;#&lt;key&gt;/file/&lt;fileID&gt;). When set, only that file
    ''' is returned.</param>
    Public Shared Function RetrieveLinksFromFolder(ByVal FolderID As String, ByVal FolderKey As String, _
                                                    Optional ByVal SubFolderID As String = "", _
                                                    Optional ByVal SubFileID As String = "") As Generic.List(Of URLProcessor.FileURL)
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

        ' 原始父级关系表 (handle -> parent handle), 用于子文件夹过滤的祖先链判断。
        ' 与 htFolderEstructure 不同, 这里不经解密——中间某层文件夹即使解密失败,
        ' 祖先链依然完整, 其下文件的归属判断不受影响。
        Dim parentMap As New Generic.Dictionary(Of String, String)
        For Each fileN As FileNode In FileList.f
            If fileN.t = 1 AndAlso Not parentMap.ContainsKey(fileN.h) Then
                parentMap(fileN.h) = If(fileN.h = root, "", fileN.p)
            End If
        Next

        If Not String.IsNullOrEmpty(SubFolderID) Then
            If Not parentMap.ContainsKey(SubFolderID) Then
                Throw New ApplicationException("The subfolder specified in the link was not found in the shared folder (it may have been deleted).")
            End If
        End If
        If Not String.IsNullOrEmpty(SubFileID) Then
            Dim found As Boolean = False
            For Each fileN As FileNode In FileList.f
                If fileN.t = 0 AndAlso fileN.h = SubFileID Then
                    found = True
                    Exit For
                End If
            Next
            If Not found Then
                Throw New ApplicationException("The file specified in the link was not found in the shared folder (it may have been deleted).")
            End If
        End If


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

                Dim rx As New System.Text.RegularExpressions.Regex(Conexion.patternGetFileName)
                If Not String.IsNullOrEmpty(FolderName) AndAlso rx.IsMatch(FolderName) Then
                    Dim m As System.Text.RegularExpressions.Match = rx.Match(FolderName)
                    FolderName = m.Groups("FileName").Value
                Else
                    Continue For
                End If

                Try
                    FolderName = PathGuard.RequireSafePathSegment(FolderName, "remote folder name")
                Catch
                    FolderName = PathGuard.SanitizeFileName(FolderName, "folder")
                End Try

                ' 父级 handle: 如果 fileN.h == root,说明这是根文件夹本身,没有父级
                Dim parent As String = If(fileN.h = root, "", fileN.p)
                htFolderEstructure.Add(FileID, New KeyValuePair(Of String, String)(FolderName, parent))

            End If
        Next
        Dim htFolders As New Generic.Dictionary(Of String, String)
        FillFolderStructure(root, htFolders, htFolderEstructure)

        ' 子文件夹链接: 路径表重定基为以子文件夹为根 (handle 级 BFS, 不依赖字符串前缀去除,
        ' 避免不同层级同名文件夹造成的路径误判)。解密失败的文件夹不在 htFolderEstructure 中,
        ' 其下文件路径退化为 "" —— 与整文件夹下载时对同类节点的处理一致。
        Dim pathMap As Generic.Dictionary(Of String, String) = htFolders
        If Not String.IsNullOrEmpty(SubFolderID) Then
            pathMap = BuildSubfolderPaths(SubFolderID, htFolderEstructure)
        End If


        ' Get files
        For Each fileN As FileNode In FileList.f

            If fileN.t = 0 Then
                ' 单文件链接: 只保留链接指向的那个文件
                If Not String.IsNullOrEmpty(SubFileID) AndAlso Not fileN.h = SubFileID Then
                    Continue For
                End If

                ' 子文件夹链接: 只保留父级祖先链包含目标子文件夹的文件
                If Not String.IsNullOrEmpty(SubFolderID) AndAlso Not IsUnderFolder(fileN.p, SubFolderID, parentMap) Then
                    Continue For
                End If

                ' 从 k 字段提取与 root handle 匹配的 key
                Dim FileKey As String = ExtractKeyFromK(fileN.k, root)
                If String.IsNullOrEmpty(FileKey) Then Continue For

                Dim path As String = String.Empty
                If pathMap.ContainsKey(fileN.p) Then
                    path = pathMap(fileN.p)
                End If

                Try
                    FileKey = Criptografia.a32_to_base64(Criptografia.decrypt_key(Criptografia.base64_to_a32(FileKey), Criptografia.base64_to_a32(FolderKey)))
                Catch exCrypt As Exception
                    ' 解密失败,跳过此文件 (可能无法用 FolderKey 解密)
                    Continue For
                End Try

                Dim FileInfoDec As String = PreSharedKeyManager.DecryptFileInfo(fileN.a, FileKey)
                Try
                    Dim rx As New System.Text.RegularExpressions.Regex(Conexion.patternGetFileName)
                    If Not String.IsNullOrEmpty(FileInfoDec) AndAlso rx.IsMatch(FileInfoDec) Then
                        Dim m As System.Text.RegularExpressions.Match = rx.Match(FileInfoDec)
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

                            Dim NuevoLink As String = String.Format("https://mega.nz/#N!{0}!{1}=###n={2}", fileN.h, FileKey, FolderID)
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

    ' 判断 startFolder 的祖先链 (含自身) 是否包含 targetFolder。
    ' parentMap: handle -> parent handle (root 的父级为 "")。带环路防护。
    Private Shared Function IsUnderFolder(ByVal startFolder As String, ByVal targetFolder As String, _
                                          ByVal parentMap As Generic.Dictionary(Of String, String)) As Boolean
        If String.IsNullOrEmpty(startFolder) OrElse String.IsNullOrEmpty(targetFolder) Then Return False

        Dim current As String = startFolder
        Dim guard As Integer = 0
        While Not String.IsNullOrEmpty(current) AndAlso guard < 10000
            If String.Equals(current, targetFolder, StringComparison.Ordinal) Then Return True
            If Not parentMap.ContainsKey(current) Then Return False
            current = parentMap(current)
            guard += 1
        End While
        Return False
    End Function

    ' 构建以 subFolderID 为根的相对路径表 (handle -> 相对子文件夹的路径)。
    ' 只遍历子文件夹的后代; 子文件夹本身路径为 ""。
    Private Shared Function BuildSubfolderPaths(ByVal subFolderID As String, _
                                                ByVal folderEstructure As Generic.Dictionary(Of String, KeyValuePair(Of String, String))) As Generic.Dictionary(Of String, String)
        Dim childrenMap As New Generic.Dictionary(Of String, Generic.List(Of String))
        For Each entry As KeyValuePair(Of String, KeyValuePair(Of String, String)) In folderEstructure
            Dim parentHandle As String = entry.Value.Value
            If String.IsNullOrEmpty(parentHandle) Then Continue For
            Dim children As Generic.List(Of String) = Nothing
            If Not childrenMap.TryGetValue(parentHandle, children) Then
                children = New Generic.List(Of String)()
                childrenMap(parentHandle) = children
            End If
            children.Add(entry.Key)
        Next

        Dim paths As New Generic.Dictionary(Of String, String)
        Dim pending As New Generic.Queue(Of String)
        paths(subFolderID) = ""
        pending.Enqueue(subFolderID)

        While pending.Count > 0
            Dim current As String = pending.Dequeue()
            Dim children As Generic.List(Of String) = Nothing
            If Not childrenMap.TryGetValue(current, children) Then Continue While
            For Each child As String In children
                If paths.ContainsKey(child) Then Continue For
                paths(child) = PathGuard.CombineSafeRelativePath(paths(current), folderEstructure(child).Key)
                pending.Enqueue(child)
            Next
        End While

        Return paths
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
        ' Two-phase safe build: index first so parent order does not matter.
        If final.Count = 0 AndAlso unprocessed.Count > 0 Then
            Dim remaining As New Generic.HashSet(Of String)(unprocessed.Keys)
            Dim guard As Integer = 0
            While remaining.Count > 0 AndAlso guard < unprocessed.Count + 2
                guard += 1
                Dim progressed As Boolean = False
                For Each nodeId As String In remaining.ToList()
                    Dim parent As String = unprocessed(nodeId).Value
                    If String.IsNullOrEmpty(parent) OrElse parent = id Then
                        final(nodeId) = ""
                        remaining.Remove(nodeId)
                        progressed = True
                    ElseIf final.ContainsKey(parent) Then
                        final(nodeId) = PathGuard.CombineSafeRelativePath(final(parent), unprocessed(nodeId).Key)
                        remaining.Remove(nodeId)
                        progressed = True
                    End If
                Next
                If Not progressed Then
                    ' Orphans: place at relative root with sanitized name
                    For Each nodeId As String In remaining
                        final(nodeId) = PathGuard.SanitizeFileName(unprocessed(nodeId).Key, "folder")
                    Next
                    Exit While
                End If
            End While
            Return
        End If

        If unprocessed.ContainsKey(id) AndAlso Not final.ContainsKey(id) Then
            Dim parent As String = unprocessed(id).Value
            If Not String.IsNullOrEmpty(parent) AndAlso final.ContainsKey(parent) Then
                final.Add(id, PathGuard.CombineSafeRelativePath(final(parent), unprocessed(id).Key))
            Else
                final.Add(id, "")
            End If
            For Each son In (From n In unprocessed.Keys Where unprocessed(n).Value = id)
                FillFolderStructure(son, final, unprocessed)
            Next
        End If
    End Sub


End Class
