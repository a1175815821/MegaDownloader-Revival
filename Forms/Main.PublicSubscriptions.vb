Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Main
    Friend Function HasPublicSubscription(listing As PublicFolderListing, folderHandle As String) As Boolean
        Try
            Return PublicFolderSubscriptions.Current.Contains(listing.SubscriptionId(folderHandle))
        Catch ex As Exception When PublicFolderSubscriptions.IsStorageError(ex)
            Return False
        End Try
    End Function

    Friend Async Function ManagePublicSubscriptionAsync(owner As IWin32Window, listing As PublicFolderListing,
                                                         folderHandle As String, destination As String, downloadEmail As String) As Task(Of Boolean)
        If Not Await WaitForStartupQueueAsync() Then Return False
        Dim store = PublicFolderSubscriptions.Current
        Dim id = listing.SubscriptionId(folderHandle)
        Try
            If store.Find(id) IsNot Nothing Then
                Using manager As New MegaDriveSubscriptionsForm(Me, SubscriptionService, initialEmail:="", initialFolder:=id)
                    manager.ShowDialog(owner)
                End Using
                Return store.Find(id) IsNot Nothing
            End If

            If Not listing.IsComplete Then
                MessageBox.Show(owner, Language.GetText("DriveSubscription_PublicCheckFailed"), Language.GetText("DriveSubscription_Title"))
                Return False
            End If
            Dim initial As New MegaDriveSubscription With {.Folder = id, .Name = listing.Breadcrumb(folderHandle),
                .PublicShareUrl = listing.FolderLink(folderHandle), .DownloadEmail = downloadEmail,
                .Destination = PathGuard.GetSafePathUnderRoot(destination, listing.Directories(folderHandle).Name, allowRoot:=False),
                .CheckMinutes = 9 * 60, .ScheduleFromUtc = Date.UtcNow, .AutoQueue = True}
            Config.ApplyNewSubscriptionDefaults(initial)
            Dim previewTask = Task.Run(Function()
                                           Return (Files:=listing.Plan(folderHandle, initial.FolderPaths), Queue:=SnapshotPublicQueue(),
                                               Delivered:=PublicFolderSubscriptions.History.CreatePublicDeliveryLookup(PublicFolderSubscriptions.DeliveryScope))
                                       End Function)
            Dim preview = Await previewTask
            Dim settings As MegaDriveSubscription
            Using dialog As New MegaDriveSubscriptionSettingsForm(initial,
                Function(value) CountPublicMatches(value, preview.Files, preview.Queue, preview.Delivered))
                If dialog.ShowDialog(owner) <> DialogResult.OK Then Return False
                settings = dialog.Settings
            End Using

            Await subscriptionWork.WaitAsync()
            Dim queued As Integer = 0
            Try
                If Cerrando Then Return False
                settings.ScheduleFromUtc = Date.UtcNow
                settings.LastCheckedUtc = settings.ScheduleFromUtc
                Await Task.Run(Sub()
                                   PublicFolderSubscriptions.ApplySnapshot(settings, preview.Files, initial:=True)
                                   store.Create(settings)
                               End Sub)
                If settings.IncludeExisting Then queued = Await EnqueuePublicSubscriptionAsync(id, Nothing)
            Finally
                subscriptionWork.Release()
            End Try
            Dim nextCheck = settings.NextCheckLocal()
            Dim notice = Language.GetText(If(nextCheck.HasValue AndAlso Not Config.SubscriptionChecksPaused,
                "DriveSubscription_PublicCreated", "DriveSubscription_PublicCreatedManual"))
            ToastForm.ShowToast(If(TryCast(owner, Form), Me), notice.Replace("%NAME%", settings.Caption).
                Replace("%TIME%", nextCheck.GetValueOrDefault().ToString("g")))
            If queued < 0 Then MessageBox.Show(owner, Language.GetText("DriveSubscription_QueueFailed"), Language.GetText("DriveSubscription_Title"))
            Return True
        Catch ex As Exception When PublicFolderSubscriptions.IsStorageError(ex)
            Log.WriteWarning("Could not save public folder subscription: " & Log.SafeException(ex))
            MessageBox.Show(owner, MegaAccountService.ErrorText(MegaAccountService.LocalError), Language.GetText("DriveSubscription_Title"))
            Return False
        End Try
    End Function

    Friend Function SavePublicSubscriptionSettings(settings As MegaDriveSubscription) As Integer
        Try
            Return If(PublicFolderSubscriptions.Current.SaveSettings(settings), 0, -9)
        Catch ex As Exception When PublicFolderSubscriptions.IsStorageError(ex)
            Log.WriteWarning("Could not edit public folder subscription: " & Log.SafeException(ex))
            Return MegaAccountService.LocalError
        End Try
    End Function

    Friend Function RemovePublicSubscription(folder As ULong) As Integer
        Try
            PublicFolderSubscriptions.Current.Remove(folder)
            Return 0
        Catch ex As Exception When PublicFolderSubscriptions.IsStorageError(ex)
            Log.WriteWarning("Could not remove public folder subscription: " & Log.SafeException(ex))
            Return MegaAccountService.LocalError
        End Try
    End Function

    Friend Async Function CheckPublicSubscriptionsAsync(folder As ULong?, Optional scheduled As Boolean = False) As Task
        Await subscriptionWork.WaitAsync()
        Try
            If Cerrando OrElse (scheduled AndAlso Config.SubscriptionChecksPaused) Then Return
            Dim store = PublicFolderSubscriptions.Current
            Dim now = Date.Now
            Dim all = Await Task.Run(Function() If(scheduled, store.Due(now), store.ReadAll()))
            For Each item In all
                If Cerrando OrElse (scheduled AndAlso Config.SubscriptionChecksPaused) Then Exit For
                If folder.HasValue AndAlso item.Folder <> folder.Value Then Continue For
                Dim updated = Await Task.Run(Function() ScanPublicSubscription(item))
                If Cerrando Then Exit For
                If updated IsNot Nothing AndAlso updated.LastError = 0 AndAlso updated.AutoQueue Then
                    Dim result = Await EnqueuePublicSubscriptionAsync(item.Folder, Nothing)
                    If result < 0 Then Log.WriteWarning("Could not enqueue public folder subscription: " & result.ToString())
                End If
            Next
        Catch ex As Exception When PublicFolderSubscriptions.IsStorageError(ex)
            Log.WriteWarning("Could not check public folder subscriptions: " & Log.SafeException(ex))
        Finally
            subscriptionWork.Release()
        End Try
    End Function

    Private Shared Function ScanPublicSubscription(previous As MegaDriveSubscription) As MegaDriveSubscription
        Dim item = previous.Copy()
        item.LastAttemptUtc = Date.UtcNow
        Try
            Dim listing = PublicFolderListing.Read(item.PublicShareUrl, Nothing, CancellationToken.None)
            If Not listing.IsComplete Then Throw New ApplicationException("The share listing was incomplete.")
            Dim files = listing.Plan(listing.RootHandle, item.FolderPaths)
            item.LastCheckedUtc = item.LastAttemptUtc
            item.LastError = 0
            PublicFolderSubscriptions.ApplySnapshot(item, files, initial:=False)
        Catch ex As PublicFolderUnavailableException
            item.LastError = -9
        Catch ex As Exception When TypeOf ex Is ApplicationException OrElse TypeOf ex Is MegaQuotaExceededException OrElse PublicFolderSubscriptions.IsStorageError(ex)
            Log.WriteWarning("Could not scan public folder: " & Log.SafeException(ex))
            item.LastError = -3
        End Try
        Return If(PublicFolderSubscriptions.Current.SaveCheck(previous, item), item, Nothing)
    End Function

    Friend Function ReadPublicSubscriptionReportsAsync() As Task(Of List(Of MegaDriveSubscription))
        Return Task.Run(Function()
                            Dim items = PublicFolderSubscriptions.Current.ReadAll()
                            If items.Count = 0 Then Return items
                            Dim active = SnapshotPublicQueue()
                            Dim delivered = PublicFolderSubscriptions.History.CreatePublicDeliveryLookup(PublicFolderSubscriptions.DeliveryScope)
                            For Each item In items
                                For Each file In item.Files
                                    file.Status = PublicFileStatus(item, file, active, delivered)
                                Next
                            Next
                            Return items
                        End Function)
    End Function

    Friend Async Function QueuePublicSubscriptionFilesAsync(selection As Dictionary(Of ULong, HashSet(Of ULong))) As Task(Of Integer)
        Await subscriptionWork.WaitAsync()
        Try
            Dim count As Integer
            For Each group In selection
                Dim result = Await EnqueuePublicSubscriptionAsync(group.Key, group.Value)
                If result < 0 Then Return If(count > 0, count, result)
                count += result
            Next
            Return count
        Finally
            subscriptionWork.Release()
        End Try
    End Function

    Private Async Function EnqueuePublicSubscriptionAsync(folder As ULong, selected As HashSet(Of ULong)) As Task(Of Integer)
        If Cerrando Then Return 0
        Dim package As Paquete = Nothing
        Try
            Dim item = PublicFolderSubscriptions.Current.Find(folder)
            If item Is Nothing OrElse item.Files.Count = 0 Then Return 0
            Dim extract = Config.ExtraerAutomaticamente
            package = Await Task.Run(Function()
                                         Dim active = SnapshotPublicQueue()
                                         Dim delivered = PublicFolderSubscriptions.History.CreatePublicDeliveryLookup(PublicFolderSubscriptions.DeliveryScope)
                                         Dim inputs As New List(Of URLProcessor.FileURL)()
                                         For Each file In PublicFolderSubscriptions.MatchesFilter(item, item.Files)
                                             If selected IsNot Nothing AndAlso Not selected.Contains(file.Handle) Then Continue For
                                             If PublicFileStatus(item, file, active, delivered) <> MegaDriveSubscriptionFileState.PendingReview Then Continue For
                                             inputs.Add(New URLProcessor.FileURL(file.PublicUrl, file.RelativePath) With {.DisplayName = file.Name,
                                                 .SizeBytes = file.Bytes, .PublicFolderAccountLink = file.AccountLink, .FixedDownloadIdentity = True})
                                         Next
                                         If inputs.Count = 0 Then Return Nothing
                                         Return DownloadPackageBuilder.Build(inputs, item.Caption, item.Destination, False, extract, "", CancellationToken.None,
                                                                             megaAccountEmail:=item.DownloadEmail)
                                     End Function)
            If package Is Nothing Then Return 0
            Dim latest = PublicFolderSubscriptions.Current.Find(folder)
            If Cerrando OrElse latest Is Nothing OrElse Not latest.MatchesSettings(item) Then Return 0
            ' Other imports can have reached the UI queue while this package was built off-thread.
            Dim currentQueue = SnapshotPublicQueue()
            For index = package.ListaFicheros.Count - 1 To 0 Step -1
                Dim file = package.ListaFicheros(index)
                Dim identity = BatchLinkPreflight.Identity(file.FileID, file.FileKey)
                If currentQueue(identity).Any(Function(task) String.Equals(task.RutaLocal, file.RutaLocal, StringComparison.OrdinalIgnoreCase) AndAlso
                    (task.DescargaEstado() <> Estado.Completado OrElse task.FindRecordedPublicOutput() IsNot Nothing)) Then
                    package.ListaFicheros.RemoveAt(index)
                    file.Dispose()
                End If
            Next
            Dim count = package.ListaFicheros.Count
            If count > 0 Then AgregarPaquete(package, AgregadoDesdeServidorWeb:=True, saveInBackground:=True)
            Return count
        Catch ex As Exception When PublicFolderSubscriptions.IsStorageError(ex)
            Log.WriteWarning("Could not enqueue public folder subscription: " & Log.SafeException(ex))
            Return MegaAccountService.LocalError
        Finally
            If package IsNot Nothing AndAlso Not IsPackageQueued(package) Then
                For Each file In package.ListaFicheros
                    file.Dispose()
                Next
            End If
        End Try
    End Function

    Private Function SnapshotPublicQueue() As ILookup(Of String, Fichero)
        Dim snapshot As New List(Of Fichero)()
        SyncLock Mutex.ListaDescargas
            For Each package In ListaPaquetes
                snapshot.AddRange(package.SnapshotFicheros())
            Next
        End SyncLock
        Dim sources As New List(Of KeyValuePair(Of String, Fichero))()
        For Each file In snapshot
            Try
                Dim fileId = file.FileID
                If Not fileId.StartsWith("N?", StringComparison.Ordinal) Then Continue For
                Dim identity = BatchLinkPreflight.Identity(fileId, file.FileKey)
                If identity IsNot Nothing Then sources.Add(New KeyValuePair(Of String, Fichero)(identity, file))
            Catch ex As ObjectDisposedException
                ' A removed task can outlive the snapshot of the queue.
            End Try
        Next
        Return sources.ToLookup(Function(item) item.Key, Function(item) item.Value, StringComparer.Ordinal)
    End Function

    Private Shared Function PublicFileStatus(item As MegaDriveSubscription, file As MegaDriveSubscriptionFile,
                                            active As ILookup(Of String, Fichero),
                                            delivered As Func(Of MegaDriveSubscriptionFile, String, String)) As MegaDriveSubscriptionFileState
        Dim target = PathGuard.GetSafePathUnderRoot(item.Destination, file.RelativePath)
        file.LocalPath = delivered(file, target)
        If file.LocalPath IsNot Nothing Then Return MegaDriveSubscriptionFileState.Downloaded
        Dim identity = BatchLinkPreflight.Identity(New URLProcessor.FileURL(file.PublicUrl, ""))
        For Each task In active(identity)
            If Not String.Equals(task.RutaLocal, target, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim existing = task.FindRecordedPublicOutput()
            If existing IsNot Nothing Then
                file.LocalPath = existing
                Return MegaDriveSubscriptionFileState.Downloaded
            End If
            If task.DescargaEstado() <> Estado.Completado Then Return MegaDriveSubscriptionFileState.Queued
        Next
        Return MegaDriveSubscriptionFileState.PendingReview
    End Function

    Private Shared Function CountPublicMatches(item As MegaDriveSubscription, allFiles As List(Of MegaDriveSubscriptionFile),
                                              active As ILookup(Of String, Fichero),
                                              delivered As Func(Of MegaDriveSubscriptionFile, String, String)) As (Matches As Integer, Queued As Integer, Downloaded As Integer, Adds As Integer)
        Dim result As (Matches As Integer, Queued As Integer, Downloaded As Integer, Adds As Integer)
        For Each file In PublicFolderSubscriptions.MatchesFilter(item, allFiles)
            result.Matches += 1
            Select Case PublicFileStatus(item, file, active, delivered)
                Case MegaDriveSubscriptionFileState.Queued : result.Queued += 1
                Case MegaDriveSubscriptionFileState.Downloaded : result.Downloaded += 1
                Case Else : If item.IncludeExisting Then result.Adds += 1
            End Select
        Next
        Return result
    End Function
End Class
