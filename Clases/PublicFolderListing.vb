Imports System.Text.RegularExpressions
Imports System.Threading

Friend NotInheritable Class PublicFolderDirectory
    Friend ReadOnly Handle As String
    Friend ReadOnly ParentHandle As String
    Friend ReadOnly Name As String

    Friend Sub New(handle As String, parentHandle As String, name As String)
        Me.Handle = handle
        Me.ParentHandle = parentHandle
        Me.Name = name
    End Sub
End Class

Friend NotInheritable Class PublicFolderUnavailableException
    Inherits ApplicationException

    Friend Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>A decoded public share with stable node handles, including empty directories.</summary>
Friend NotInheritable Class PublicFolderListing
    Private Shared ReadOnly linkPattern As New Regex(
        "\A(?:https?://)?(?:www\.)?mega\.(?:co\.)?nz/(?:(?:folder/(?<share>[A-Za-z0-9_-]{8})#(?<key>[A-Za-z0-9_-]{22})={0,2}(?:/folder/(?<folder>[A-Za-z0-9_-]{8}))?(?:/file/(?<file>[A-Za-z0-9_-]{8}))?)|(?:#f!(?<share>[A-Za-z0-9_-]{8})!(?<key>[A-Za-z0-9_-]{22})={0,2}))\z",
        RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)

    Friend ReadOnly ShareId As String
    Private ReadOnly ShareKey As String
    Friend RootHandle As String
    Friend ShareName As String
    Friend Files As List(Of URLProcessor.FileURL)
    Friend IsComplete As Boolean = True
    Friend ReadOnly Directories As New Dictionary(Of String, PublicFolderDirectory)(StringComparer.Ordinal)

    Friend Sub New(shareId As String, shareKey As String)
        Me.ShareId = shareId
        Me.ShareKey = shareKey
    End Sub

    Friend Shared Function IsFolderLink(url As String) As Boolean
        Dim parsed = linkPattern.Match(If(url, ""))
        Return parsed.Success AndAlso Not parsed.Groups("file").Success
    End Function

    Friend Shared Function IsFileLink(url As String) As Boolean
        Dim parsed = linkPattern.Match(If(url, ""))
        Return parsed.Success AndAlso parsed.Groups("file").Success
    End Function

    Friend Shared Function Read(url As String, progress As IProgress(Of Integer), token As CancellationToken) As PublicFolderListing
        Dim parsed = linkPattern.Match(If(url, ""))
        If Not parsed.Success OrElse parsed.Groups("file").Success Then Throw New ArgumentException("Not a public folder link.", NameOf(url))
        Dim result As New PublicFolderListing(parsed.Groups("share").Value, parsed.Groups("key").Value)
        result.Files = MegaFolderHelper.ReadFolder(result.ShareId, result.ShareKey, parsed.Groups("folder").Value, "", progress, token, result)
        Return result
    End Function

    Friend Function FileLink(handle As String) As String
        Return "https://mega.nz/folder/" & ShareId & "#" & ShareKey & "/file/" & handle
    End Function

    Friend Function FolderLink(handle As String) As String
        Return "https://mega.nz/folder/" & ShareId & "#" & ShareKey & "/folder/" & handle
    End Function

    Friend Function SubscriptionId(handle As String) As ULong
        Using sha = Security.Cryptography.SHA256.Create()
            Return BitConverter.ToUInt64(sha.ComputeHash(Text.Encoding.ASCII.GetBytes(ShareId & "/" & handle)), 0)
        End Using
    End Function

    Friend Function Breadcrumb(handle As String) As String
        Dim names As New Stack(Of String)()
        Dim node As PublicFolderDirectory = Nothing
        While Directories.TryGetValue(handle, node)
            names.Push(node.Name)
            handle = node.ParentHandle
        End While
        If names.Count > 0 AndAlso ShareName <> "" AndAlso Not String.Equals(ShareName, names.Peek(), StringComparison.Ordinal) Then names.Push(ShareName)
        Return String.Join(" / ", names)
    End Function

    Friend Shared Function NodeId(value As String) As ULong
        If value.StartsWith("N?", StringComparison.Ordinal) Then value = value.Substring(2)
        If value.Length <> 8 Then Throw New ArgumentException("Invalid MEGA node handle.", NameOf(value))
        Dim bytes = Convert.FromBase64String(value.Replace("-"c, "+"c).Replace("_"c, "/"c))
        Dim extended(7) As Byte
        Array.Copy(bytes, extended, 6)
        Return BitConverter.ToUInt64(extended, 0)
    End Function

    Friend Function Plan(folder As String, previousPaths As Dictionary(Of ULong, String)) As List(Of MegaDriveSubscriptionFile)
        Dim root = NodeId(folder)
        Dim nodes As New List(Of MegaDriveNode)(Directories.Count + Files.Count)
        For Each entry In Directories.Values
            nodes.Add(New MegaDriveNode(NodeId(entry.Handle), If(entry.ParentHandle = "", ULong.MaxValue, NodeId(entry.ParentHandle)), entry.Name, True, 0, 0))
        Next
        Dim sources As New Dictionary(Of ULong, URLProcessor.FileURL)()
        For Each file In Files
            If Not Directories.ContainsKey(file.PublicFolderParent) Then Continue For
            Dim handle = NodeId(Fichero.ExtraerFileID(file.URL))
            sources(handle) = file
            nodes.Add(New MegaDriveNode(handle, NodeId(file.PublicFolderParent), file.DisplayName, False, file.SizeBytes.GetValueOrDefault(), 0))
        Next
        Dim snapshot As New MegaDriveSnapshot(NodeId(RootHandle), nodes)
        Dim download = snapshot.PlanBoundDownload(root, previousPaths)
        previousPaths.Clear()
        For Each binding In download.FolderPaths
            previousPaths.Add(binding.Key, binding.Value)
        Next
        Return download.Files.Select(Function(entry)
                                     Dim file = sources(entry.Key.Id)
                                     Return New MegaDriveSubscriptionFile With {.Handle = entry.Key.Id, .Name = file.DisplayName,
                                         .RelativePath = entry.Value, .Bytes = entry.Key.Bytes, .PublicUrl = file.URL,
                                         .AccountLink = file.PublicFolderAccountLink,
                                         .VersionKey = VersionOf(BatchLinkPreflight.Identity(file), entry.Key.Bytes)}
                                 End Function).ToList()
    End Function

    Friend Shared Function VersionOf(identity As String, bytes As Long) As String
        Return If(identity Is Nothing, Nothing, Utils.SecretFingerprint(identity & vbLf & bytes.ToString(Globalization.CultureInfo.InvariantCulture)))
    End Function
End Class
