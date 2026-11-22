Imports System.IO
Imports System.Text
Imports Newtonsoft.Json

Friend NotInheritable Class MegaDriveQueueItem
    Friend ReadOnly Handle As ULong
    Friend ReadOnly LocalDirectory As String
    Friend ReadOnly FileName As String
    Friend ReadOnly Bytes As Long
    Friend ReadOnly Completed As Boolean

    Friend Sub New(handle As ULong, localDirectory As String, fileName As String, bytes As Long, completed As Boolean)
        Me.Handle = handle
        Me.LocalDirectory = localDirectory
        Me.FileName = fileName
        Me.Bytes = bytes
        Me.Completed = completed
    End Sub
End Class

Friend NotInheritable Class MegaDriveChanges
    Friend ReadOnly Plan As MegaDriveDownload
    Friend ReadOnly Pending As HashSet(Of ULong)
    Friend ReadOnly Updated As HashSet(Of ULong)

    Friend Sub New(plan As MegaDriveDownload, pending As HashSet(Of ULong), updated As HashSet(Of ULong))
        Me.Plan = plan
        Me.Pending = pending
        Me.Updated = updated
    End Sub
End Class

Friend NotInheritable Class MegaDriveHistory
    Private NotInheritable Class Delivery
        Public Property Handle As ULong
        Public Property CloudName As String
        Public Property LocalPath As String
        Public Property Bytes As Long
        Public Property WriteUtcTicks As Long
    End Class

    Private Shared ReadOnly gate As New Object()
    Private ReadOnly folder As String

    Friend Sub New(folder As String)
        Me.folder = folder
    End Sub

    Private Function AccountFile(email As String) As String
        Dim key = Utils.SecretFingerprint(email.ToUpperInvariant()).Replace("+"c, "-"c).Replace("/"c, "_"c).TrimEnd("="c)
        Return Path.Combine(folder, key & ".jsonl")
    End Function

    Friend Sub Record(email As String, handle As ULong, cloudName As String, output As String)
        Try
            Dim info As New FileInfo(output)
            Dim entry As New Delivery With {.Handle = handle, .CloudName = cloudName, .LocalPath = info.FullName, .Bytes = info.Length,
                                           .WriteUtcTicks = info.LastWriteTimeUtc.Ticks}
            Dim line = JsonConvert.SerializeObject(entry)
            SyncLock gate
                Directory.CreateDirectory(folder)
                ' Start a fresh line even if the prior append was interrupted.
                File.AppendAllText(AccountFile(email), vbLf & line, New UTF8Encoding(False))
            End SyncLock
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Log.WriteWarning("Could not record MEGA cloud drive delivery: " & Log.SafeException(ex))
        End Try
    End Sub

    Friend Sub RemoveAccount(email As String)
        SyncLock gate
            Try
                File.Delete(AccountFile(email))
            Catch ex As DirectoryNotFoundException
                ' No history has been recorded yet.
            End Try
        End SyncLock
    End Sub

    Private Function ReadDeliveries(email As String) As ILookup(Of ULong, Delivery)
        Dim delivered As New Dictionary(Of String, Delivery)(StringComparer.OrdinalIgnoreCase)
        Dim history = AccountFile(email)
        SyncLock gate
            If File.Exists(history) Then
                For Each line In File.ReadLines(history, Encoding.UTF8)
                    If line.Length = 0 Then Continue For
                    Try
                        Dim entry = JsonConvert.DeserializeObject(Of Delivery)(line)
                        delivered(entry.LocalPath) = entry
                    Catch ex As JsonException
                        ' An interrupted final write does not invalidate previous deliveries.
                    End Try
                Next
            End If
        End SyncLock
        Return delivered.Values.ToLookup(Function(item) item.Handle)
    End Function

    Private Shared Function LocalFileMatches(path As String, bytes As Long, Optional writeUtcTicks As Long? = Nothing) As Boolean
        Dim info As New FileInfo(path)
        Return info.Exists AndAlso info.Length = bytes AndAlso (Not writeUtcTicks.HasValue OrElse info.LastWriteTimeUtc.Ticks = writeUtcTicks.Value)
    End Function

    Private Shared Function MatchesLegacyDeliveryName(cloudName As String, localName As String) As Boolean
        Dim leaf = PathGuard.SanitizeFileName(cloudName, "file")
        If String.Equals(leaf, localName, StringComparison.Ordinal) Then Return True
        Dim suffix = System.Text.RegularExpressions.Regex.Match(localName, " \(([0-9]+)\)(?:\.[^.]*)?\z")
        Dim number As Integer
        Return suffix.Success AndAlso Integer.TryParse(suffix.Groups(1).Value, number) AndAlso number >= 2 AndAlso
            String.Equals(Fichero.MegaAccountCollisionLeaf(leaf, number), localName, StringComparison.Ordinal)
    End Function

    Private Shared Function IsInDirectory(candidate As String, canonicalDirectory As String) As Boolean
        Return Not String.IsNullOrEmpty(candidate) AndAlso
            String.Equals(PathGuard.GetSafePathUnderRoot(candidate, ""), canonicalDirectory, StringComparison.OrdinalIgnoreCase)
    End Function

    Friend Function FindBoundChanges(email As String, snapshot As MegaDriveSnapshot, folder As ULong, destination As String,
                                     queue As IEnumerable(Of MegaDriveQueueItem), paths As Dictionary(Of ULong, String)) As MegaDriveChanges
        Dim delivered = ReadDeliveries(email)
        Dim root = PathGuard.GetSafePathUnderRoot(destination, "").TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar
        For Each item In queue
            RecoverFolderPaths(snapshot, folder, root, item.Handle, item.LocalDirectory, paths)
        Next
        For Each group In delivered
            For Each item In group
                RecoverFolderPaths(snapshot, folder, root, item.Handle, Path.GetDirectoryName(item.LocalPath), paths)
            Next
        Next
        Dim fullPlan = snapshot.PlanBoundDownload(folder, paths)
        paths.Clear()
        For Each binding In fullPlan.FolderPaths
            paths.Add(binding.Key, binding.Value)
        Next
        Return ComparePlan(fullPlan, destination, queue, delivered)
    End Function

    Private Shared Sub RecoverFolderPaths(snapshot As MegaDriveSnapshot, boundFolder As ULong, root As String,
                                          fileHandle As ULong, directory As String, paths As Dictionary(Of ULong, String))
        Dim file = snapshot.Find(fileHandle)
        If file Is Nothing OrElse paths.ContainsKey(file.ParentId) OrElse String.IsNullOrEmpty(directory) OrElse
           Not directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) Then Return
        Dim relative = directory.Substring(root.Length)
        Dim folder = snapshot.Find(file.ParentId)
        Dim recovered As New List(Of KeyValuePair(Of ULong, String))()
        While folder IsNot Nothing AndAlso folder.Id <> boundFolder
            If String.IsNullOrEmpty(relative) OrElse Not MegaDriveSnapshot.MatchesFolderName(folder, Path.GetFileName(relative)) Then Return
            Dim known As String = Nothing
            If paths.TryGetValue(folder.Id, known) Then
                If Not String.Equals(known, relative, StringComparison.OrdinalIgnoreCase) Then Return
                Exit While
            End If
            recovered.Add(New KeyValuePair(Of ULong, String)(folder.Id, relative))
            relative = Path.GetDirectoryName(relative)
            folder = snapshot.Find(folder.ParentId)
        End While
        If folder Is Nothing OrElse (folder.Id = boundFolder AndAlso relative <> "") Then Return
        For Each binding In recovered
            paths.Add(binding.Key, binding.Value)
        Next
    End Sub

    Friend Function FindChanges(email As String, fullPlan As MegaDriveDownload, destination As String,
                                queue As IEnumerable(Of MegaDriveQueueItem)) As MegaDriveChanges
        Return ComparePlan(fullPlan, destination, queue, ReadDeliveries(email))
    End Function

    Private Shared Function ComparePlan(fullPlan As MegaDriveDownload, destination As String, queue As IEnumerable(Of MegaDriveQueueItem),
                                        delivered As ILookup(Of ULong, Delivery)) As MegaDriveChanges
        Dim queued = queue.ToLookup(Function(item) item.Handle)
        Dim pending As New HashSet(Of ULong)()
        Dim updated As New HashSet(Of ULong)()
        Dim destinations As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim compare As Func(Of KeyValuePair(Of MegaDriveNode, String), Boolean) =
            Function(file)
                Dim node = file.Key
                Dim parent As String = Nothing
                If Not destinations.TryGetValue(file.Value, parent) Then
                    parent = PathGuard.GetSafePathUnderRoot(destination, file.Value)
                    destinations.Add(file.Value, parent)
                End If
                Dim previous = delivered(node.Id).Where(Function(item) IsInDirectory(Path.GetDirectoryName(item.LocalPath), parent)).ToArray()
                If previous.Any(Function(item) item.Bytes = node.Bytes AndAlso
                    If(item.CloudName Is Nothing, MatchesLegacyDeliveryName(node.Name, Path.GetFileName(item.LocalPath)),
                       String.Equals(item.CloudName, node.Name, StringComparison.Ordinal)) AndAlso
                    LocalFileMatches(item.LocalPath, item.Bytes, item.WriteUtcTicks)) Then Return False
                Dim tasks = queued(node.Id).Where(Function(item) IsInDirectory(item.LocalDirectory, parent)).ToArray()
                ' Queue entries from before delivery history was introduced can still account for existing files.
                If previous.Length = 0 AndAlso tasks.Any(Function(item) item.Completed AndAlso item.Bytes = node.Bytes AndAlso
                    MatchesLegacyDeliveryName(node.Name, item.FileName) AndAlso LocalFileMatches(Path.Combine(parent, item.FileName), node.Bytes)) Then Return False
                If tasks.Any(Function(item) Not item.Completed) Then pending.Add(node.Id)
                If previous.Length > 0 OrElse IO.File.Exists(PathGuard.GetSafeFilePathUnderRoot(parent, node.Name)) Then updated.Add(node.Id)
                Return True
            End Function
        Return New MegaDriveChanges(fullPlan.KeepFiles(compare, preserveEmpty:=False), pending, updated)
    End Function
End Class
