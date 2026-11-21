Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Threading

Friend NotInheritable Class MegaDrivePreferences
    Public Property LastFolder As ULong?
    Public Property LastSharedFolder As ULong?
    Public Property Favorites As New List(Of ULong)()
    Public Property FolderDestinations As New Dictionary(Of ULong, String)()

    Friend Function Copy() As MegaDrivePreferences
        Return New MegaDrivePreferences With {.LastFolder = LastFolder, .LastSharedFolder = LastSharedFolder,
            .Favorites = New List(Of ULong)(Favorites), .FolderDestinations = New Dictionary(Of ULong, String)(FolderDestinations)}
    End Function
End Class

Friend NotInheritable Class MegaDriveNode
    Friend ReadOnly Id As ULong
    Friend ReadOnly ParentId As ULong
    Friend ReadOnly Name As String
    Friend ReadOnly IsFolder As Boolean
    Friend ReadOnly Bytes As Long
    Friend ReadOnly Modified As Long
    Friend ReadOnly Kind As FileKind

    Friend Enum FileKind
        All
        Video
        Archive
        Document
        Image
        Audio
        Other
    End Enum

    Friend Sub New(id As ULong, parent As ULong, name As String, folder As Boolean, bytes As Long, modified As Long)
        Me.Id = id
        ParentId = parent
        Me.Name = If(name, "")
        IsFolder = folder
        Me.Bytes = bytes
        Me.Modified = modified
        Kind = If(folder, FileKind.All, Classify(Me.Name))
    End Sub

    Private Shared Function Classify(name As String) As FileKind
        Dim dot = name.LastIndexOf("."c)
        If dot < 0 Then Return FileKind.Other
        Select Case name.Substring(dot + 1).ToLowerInvariant()
            Case "mp4", "mkv", "webm", "mov", "avi", "wmv", "m4v", "mpeg", "mpg", "flv", "ts", "rmvb", "3gp"
                Return FileKind.Video
            Case "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "tgz", "zst", "cab", "iso"
                Return FileKind.Archive
            Case "pdf", "doc", "docx", "odt", "txt", "md", "rtf", "xls", "xlsx", "ods", "csv", "ppt", "pptx", "odp", "epub"
                Return FileKind.Document
            Case "jpg", "jpeg", "png", "gif", "webp", "avif", "heic", "bmp", "tif", "tiff", "svg", "ico"
                Return FileKind.Image
            Case "mp3", "flac", "wav", "ogg", "opus", "aac", "m4a", "wma", "aiff"
                Return FileKind.Audio
            Case Else
                Return FileKind.Other
        End Select
    End Function
End Class

Friend NotInheritable Class MegaDriveSnapshot
    Friend ReadOnly Root As ULong
    Private ReadOnly nodes As Dictionary(Of ULong, MegaDriveNode)
    Private ReadOnly children As ILookup(Of ULong, MegaDriveNode)

    Friend Sub New(root As ULong, items As IEnumerable(Of MegaDriveNode))
        Me.Root = root
        nodes = items.ToDictionary(Function(node) node.Id)
        children = nodes.Values.Where(Function(node) node.Id <> root).ToLookup(Function(node) node.ParentId)
    End Sub

    Friend Function Find(handle As ULong) As MegaDriveNode
        Dim node As MegaDriveNode = Nothing
        nodes.TryGetValue(handle, node)
        Return node
    End Function

    Friend Function IsFolder(handle As ULong) As Boolean
        Dim node = Find(handle)
        Return node IsNot Nothing AndAlso node.IsFolder
    End Function

    Friend Function InDirectory(folder As ULong) As IEnumerable(Of MegaDriveNode)
        Return children(folder)
    End Function

    Friend Function Breadcrumbs(folder As ULong) As List(Of MegaDriveNode)
        Dim trail As New List(Of MegaDriveNode)()
        Dim node = Find(folder)
        While node IsNot Nothing
            trail.Add(node)
            If node.Id = Root Then Exit While
            node = Find(node.ParentId)
        End While
        trail.Reverse()
        Return trail
    End Function

    Friend Function Browse(folder As ULong, query As String, wholeDrive As Boolean, kind As MegaDriveNode.FileKind,
                           sortByTime As Boolean, descending As Boolean) As MegaDriveNode()
        Dim candidates As IEnumerable(Of MegaDriveNode) = If(wholeDrive, nodes.Values.AsEnumerable(), InDirectory(folder))
        Dim matches = candidates.Where(Function(node) node.Id <> Root AndAlso
            (kind = MegaDriveNode.FileKind.All OrElse node.Kind = kind OrElse (node.IsFolder AndAlso Not wholeDrive)) AndAlso
            CultureInfo.CurrentCulture.CompareInfo.IndexOf(node.Name, query, CompareOptions.IgnoreCase) >= 0)
        Return Sort(matches, sortByTime, descending)
    End Function

    Friend Shared Function Sort(items As IEnumerable(Of MegaDriveNode), byTime As Boolean, descending As Boolean) As MegaDriveNode()
        Dim foldersFirst = items.OrderByDescending(Function(node) node.IsFolder)
        If byTime Then
            foldersFirst = If(descending, foldersFirst.ThenByDescending(Function(node) node.Modified),
                              foldersFirst.ThenBy(Function(node) node.Modified))
            Return foldersFirst.ThenBy(Function(node) node.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(Function(node) node.Id).ToArray()
        End If
        Dim byName = If(descending, foldersFirst.ThenByDescending(Function(node) node.Name, StringComparer.CurrentCultureIgnoreCase),
                        foldersFirst.ThenBy(Function(node) node.Name, StringComparer.CurrentCultureIgnoreCase))
        Return byName.ThenBy(Function(node) node.Id).ToArray()
    End Function

    Friend Function PlanDownload(selection As IEnumerable(Of ULong), Optional relativeToFolder As ULong? = Nothing) As MegaDriveDownload
        Dim selected As New HashSet(Of ULong)(selection)
        Dim download As New MegaDriveDownload()
        Dim paths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim pending As New Stack(Of KeyValuePair(Of MegaDriveNode, String))()
        For Each node In Sort(selected.Select(AddressOf Find).Where(Function(item) item IsNot Nothing), False, False).Reverse()
            If Breadcrumbs(node.ParentId).Any(Function(parent) selected.Contains(parent.Id)) Then Continue For
            pending.Push(New KeyValuePair(Of MegaDriveNode, String)(node, ""))
        Next
        While pending.Count > 0
            Dim item = pending.Pop()
            Dim node = item.Key
            If Not node.IsFolder Then
                download.Files.Add(item)
                download.TotalBytes += node.Bytes
                Continue While
            End If

            Dim path = item.Value
            If Not relativeToFolder.HasValue OrElse node.Id <> relativeToFolder.Value Then
                Dim name = PathGuard.SanitizeFileName(node.Name, "folder")
                path = PathGuard.CombineSafeRelativePath(item.Value, name)
                Dim number As Integer = 1
                While Not paths.Add(path)
                    number += 1
                    Dim suffix = " (" & number.ToString(CultureInfo.InvariantCulture) & ")"
                    Dim leaf = name.Substring(0, Math.Min(name.Length, 240 - suffix.Length)) & suffix
                    path = PathGuard.CombineSafeRelativePath(item.Value, leaf)
                End While
                download.Directories.Add(path)
            End If
            For Each child In InDirectory(node.Id).OrderByDescending(Function(value) value.Id)
                pending.Push(New KeyValuePair(Of MegaDriveNode, String)(child, path))
            Next
        End While
        Return download
    End Function
End Class

Friend NotInheritable Class MegaDriveDownload
    Friend ReadOnly Files As New List(Of KeyValuePair(Of MegaDriveNode, String))()
    Friend ReadOnly Directories As New List(Of String)()
    Friend TotalBytes As Decimal

    Friend Function KeepFiles(include As Func(Of KeyValuePair(Of MegaDriveNode, String), Boolean), preserveEmpty As Boolean) As MegaDriveDownload
        Dim filtered As New MegaDriveDownload()
        Dim used As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim original As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each file In Files
            If preserveEmpty Then AddParentDirectories(original, file.Value)
            If Not include(file) Then Continue For
            filtered.Files.Add(file)
            filtered.TotalBytes += file.Key.Bytes
            AddParentDirectories(used, file.Value)
        Next
        If preserveEmpty Then
            For Each relative In Directories
                If Not original.Contains(relative) Then AddParentDirectories(used, relative)
            Next
        End If
        filtered.Directories.AddRange(Directories.Where(AddressOf used.Contains))
        Return filtered
    End Function

    Private Shared Sub AddParentDirectories(directories As HashSet(Of String), relative As String)
        While relative <> "" AndAlso directories.Add(relative)
            relative = Path.GetDirectoryName(relative)
        End While
    End Sub

    Friend Function Build(email As String, destination As String, name As String, extractArchives As Boolean, ct As CancellationToken) As Paquete
        Dim root = PathGuard.GetSafePathUnderRoot(destination, "")
        Dim paths As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {{"", root}}
        For Each relative In Directories
            ct.ThrowIfCancellationRequested()
            paths.Add(relative, PathGuard.GetSafePathUnderRoot(root, relative, allowRoot:=False))
        Next
        For Each path In paths.Values
            ct.ThrowIfCancellationRequested()
            Directory.CreateDirectory(path)
        Next
        Dim package As New Paquete With {.Nombre = name, .RutaLocal = root, .ListaFicheros = New List(Of Fichero)()}
        package.SetDescargaExtraccionAutomatica("") = extractArchives
        Try
            For Each item In Files
                ct.ThrowIfCancellationRequested()
                Dim node = item.Key
                Dim file As New Fichero With {
                    .MegaAccountEmail = email, .MegaDriveNodeHandle = node.Id,
                    .NombreFichero = PathGuard.SanitizeFileName(node.Name), .TamanoBytes = node.Bytes,
                    .RutaLocal = paths(item.Value), .RutaRelativa = item.Value, .LinkVisible = False}
                file.SetDescargaExtraccionAutomatica("") = extractArchives
                package.ListaFicheros.Add(file)
            Next
            ct.ThrowIfCancellationRequested()
            Return package
        Catch
            For Each file In package.ListaFicheros
                file.Dispose()
            Next
            Throw
        End Try
    End Function
End Class
