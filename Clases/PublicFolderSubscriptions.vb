Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Xml
Imports Newtonsoft.Json

Friend NotInheritable Class PublicFolderVersion
    Public Property VersionKey As String
    Public Property RelativeName As String
End Class

Friend NotInheritable Class PublicFolderSubscriptions
    Private Shared ReadOnly dataFolder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader", "Public subscriptions")
    Friend Shared ReadOnly Current As New PublicFolderSubscriptions(Path.Combine(dataFolder, "subscriptions.xml"))
    Friend Shared ReadOnly History As New MegaDriveHistory(dataFolder)
    Friend Const DeliveryScope As String = "Public folder downloads"

    Private ReadOnly gate As New Object()
    Private ReadOnly filePath As String
    Private subscriptions As Dictionary(Of ULong, MegaDriveSubscription)

    Friend Sub New(filePath As String)
        Me.filePath = filePath
    End Sub

    Friend Function ReadAll() As List(Of MegaDriveSubscription)
        SyncLock gate
            Load()
            Return subscriptions.Values.Select(Function(item) item.Copy()).ToList()
        End SyncLock
    End Function

    Friend Function Contains(id As ULong) As Boolean
        SyncLock gate
            Load()
            Return subscriptions.ContainsKey(id)
        End SyncLock
    End Function

    Friend Function Due(now As Date) As List(Of MegaDriveSubscription)
        SyncLock gate
            Load()
            Return subscriptions.Values.Where(Function(item)
                                                  Dim nextCheck = item.NextCheckLocal()
                                                  Return nextCheck.HasValue AndAlso nextCheck.Value <= now
                                              End Function).Select(Function(item) item.Copy()).ToList()
        End SyncLock
    End Function

    Friend Function Find(id As ULong) As MegaDriveSubscription
        SyncLock gate
            Load()
            Dim item As MegaDriveSubscription = Nothing
            Return If(subscriptions.TryGetValue(id, item), item.Copy(), Nothing)
        End SyncLock
    End Function

    Friend Function Create(item As MegaDriveSubscription) As Boolean
        SyncLock gate
            Load()
            If subscriptions.ContainsKey(item.Folder) Then Return False
            Dim nextItems As New Dictionary(Of ULong, MegaDriveSubscription)(subscriptions) From {{item.Folder, item.Copy()}}
            Write(nextItems)
            Return True
        End SyncLock
    End Function

    Friend Function SaveSettings(settings As MegaDriveSubscription) As Boolean
        SyncLock gate
            Load()
            Dim previous As MegaDriveSubscription = Nothing
            If Not subscriptions.TryGetValue(settings.Folder, previous) Then Return False
            Dim item = previous.Copy()
            item.Destination = settings.Destination
            item.Extensions = settings.Extensions
            item.AutoQueue = settings.AutoQueue
            item.DownloadEmail = settings.DownloadEmail
            If item.CheckMinutes <> settings.CheckMinutes OrElse item.CheckMinutes.HasValue <> settings.CheckMinutes.HasValue Then
                item.CheckMinutes = settings.CheckMinutes
                item.ScheduleFromUtc = Date.UtcNow
            End If
            ReplaceItem(item)
            Return True
        End SyncLock
    End Function

    Friend Function SaveCheck(previous As MegaDriveSubscription, scanned As MegaDriveSubscription) As Boolean
        SyncLock gate
            Load()
            Dim current As MegaDriveSubscription = Nothing
            If Not subscriptions.TryGetValue(scanned.Folder, current) OrElse Not current.MatchesSettings(previous) OrElse
               current.LastAttemptUtc <> previous.LastAttemptUtc OrElse current.LastAttemptUtc.HasValue <> previous.LastAttemptUtc.HasValue Then Return False
            ReplaceItem(scanned)
            Return True
        End SyncLock
    End Function

    Friend Sub Remove(id As ULong)
        SyncLock gate
            Load()
            Dim nextItems As New Dictionary(Of ULong, MegaDriveSubscription)(subscriptions)
            If nextItems.Remove(id) Then Write(nextItems)
        End SyncLock
    End Sub

    Private Sub ReplaceItem(item As MegaDriveSubscription)
        Dim nextItems As New Dictionary(Of ULong, MegaDriveSubscription)(subscriptions)
        nextItems(item.Folder) = item.Copy()
        Write(nextItems)
    End Sub

    Private Sub Load()
        If subscriptions IsNot Nothing Then Return
        Dim document As XmlDocument = Nothing
        Dim recovered As Boolean
        Dim blocked As Boolean
        If AtomicFile.TryLoadXml(filePath, document, recovered, blocked) Then
            Dim protectedBytes = Convert.FromBase64String(document.DocumentElement.InnerText)
            Dim bytes = ProtectedData.Unprotect(protectedBytes, Nothing, DataProtectionScope.CurrentUser)
            Try
                subscriptions = JsonConvert.DeserializeObject(Of List(Of MegaDriveSubscription))(Encoding.UTF8.GetString(bytes)).ToDictionary(Function(item) item.Folder)
            Finally
                Array.Clear(bytes, 0, bytes.Length)
            End Try
        ElseIf blocked OrElse File.Exists(filePath) OrElse File.Exists(filePath & ".bak") Then
            Throw New IOException("Could not read public subscriptions.")
        Else
            subscriptions = New Dictionary(Of ULong, MegaDriveSubscription)()
        End If
    End Sub

    Private Sub Write(nextItems As Dictionary(Of ULong, MegaDriveSubscription))
        Dim bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(nextItems.Values))
        Try
            Dim document As New XmlDocument()
            document.AppendChild(document.CreateElement("PublicSubscriptions")).InnerText =
                Convert.ToBase64String(ProtectedData.Protect(bytes, Nothing, DataProtectionScope.CurrentUser))
            AtomicFile.SaveXml(document, filePath)
            subscriptions = nextItems
        Finally
            Array.Clear(bytes, 0, bytes.Length)
        End Try
    End Sub

    Friend Shared Function IsStorageError(ex As Exception) As Boolean
        Return TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is CryptographicException OrElse
               TypeOf ex Is JsonException OrElse TypeOf ex Is FormatException OrElse TypeOf ex Is ArgumentException
    End Function

    Friend Shared Function MatchesFilter(item As MegaDriveSubscription, files As IEnumerable(Of MegaDriveSubscriptionFile)) As List(Of MegaDriveSubscriptionFile)
        Dim extensions As New HashSet(Of String)(item.Extensions.Split({", "}, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase)
        Return files.Where(Function(file) MegaDriveSubscription.MatchesExtension(file.Name, extensions)).ToList()
    End Function

    ''' <summary>Compare whole snapshots, but report only items matching this subscription's filter.</summary>
    Friend Shared Sub ApplySnapshot(item As MegaDriveSubscription, snapshot As List(Of MegaDriveSubscriptionFile), initial As Boolean)
        Dim known = If(item.KnownPublicFiles, New Dictionary(Of ULong, PublicFolderVersion)())
        Dim formerPaths As New HashSet(Of String)(known.Values.Select(Function(file) file.RelativeName), StringComparer.OrdinalIgnoreCase)
        Dim previousReport = item.Files.ToDictionary(Function(file) file.Handle)
        item.Files = New List(Of MegaDriveSubscriptionFile)()
        For Each file In MatchesFilter(item, snapshot)
            Dim previous As PublicFolderVersion = Nothing
            Dim alreadyKnown = known.TryGetValue(file.Handle, previous)
            If alreadyKnown AndAlso previous.VersionKey = file.VersionKey Then
                Dim report As MegaDriveSubscriptionFile = Nothing
                If previousReport.TryGetValue(file.Handle, report) Then
                    file.Updated = report.Updated
                    item.Files.Add(file)
                End If
            ElseIf Not initial OrElse item.IncludeExisting Then
                file.Updated = alreadyKnown OrElse formerPaths.Contains(RelativeName(file))
                item.Files.Add(file)
            End If
        Next
        item.KnownPublicFiles = snapshot.ToDictionary(Function(file) file.Handle,
            Function(file) New PublicFolderVersion With {.VersionKey = file.VersionKey, .RelativeName = RelativeName(file)})
    End Sub

    Private Shared Function RelativeName(file As MegaDriveSubscriptionFile) As String
        Return Path.Combine(file.RelativePath, file.Name)
    End Function
End Class
