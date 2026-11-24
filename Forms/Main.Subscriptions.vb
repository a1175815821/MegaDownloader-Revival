Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class Main
    Private ReadOnly subscriptionWork As New SemaphoreSlim(1)
    Private subscriptionMenu As ToolStripMenuItem
    Private openingDriveSubscriptions As Boolean
    Private subscriptionTickRunning As Boolean

    Private Shared ReadOnly Property SubscriptionService As MegaAccountService
        Get
            Return MegaAccountService.Current
        End Get
    End Property

    Private Sub EnsureDriveSubscriptionMenu()
        If subscriptionMenu Is Nothing OrElse subscriptionMenu.Owner IsNot MainMenuStrip Then
            subscriptionMenu = New ToolStripMenuItem With {.Name = "MegaDriveSubscriptionsMenu"}
            AddHandler subscriptionMenu.Click, AddressOf OpenDriveSubscriptions
            MainMenuStrip.Items.Add(subscriptionMenu)
        End If
        subscriptionMenu.Text = Language.GetText("DriveSubscription_Title")
    End Sub

    Private Async Sub OpenDriveSubscriptions(sender As Object, e As EventArgs)
        If openingDriveSubscriptions Then Return
        openingDriveSubscriptions = True
        Try
            If Not Await WaitForStartupQueueAsync() Then Return
            Using dialog As New MegaDriveSubscriptionsForm(Me, SubscriptionService)
                dialog.ShowDialog(Me)
            End Using
        Finally
            openingDriveSubscriptions = False
        End Try
    End Sub

    Private Sub InitDriveSubscriptionScheduler()
        Dim timer As New Windows.Forms.Timer(components) With {.Interval = 30000}
        AddHandler timer.Tick, AddressOf CheckScheduledSubscriptions
        timer.Start()
    End Sub

    Private Async Sub CheckScheduledSubscriptions(sender As Object, e As EventArgs)
        If subscriptionTickRunning OrElse Cerrando OrElse Volatile.Read(_startupQueueLoading) OrElse Config.SubscriptionChecksPaused Then Return
        subscriptionTickRunning = True
        Try
            Await CheckPublicSubscriptionsAsync(Nothing, scheduled:=True)
            If Cerrando OrElse SubscriptionService.IsRestoring Then Return
            For Each account In SubscriptionService.GetAccounts()
                If Cerrando OrElse Config.SubscriptionChecksPaused Then Exit For
                If account.SignedIn AndAlso Not account.Connecting Then Await CheckSubscriptionsAsync(account.Email, Nothing, scheduled:=True)
            Next
        Finally
            subscriptionTickRunning = False
        End Try
    End Sub

    Friend Function CheckDriveSubscriptionsAsync(email As String, folder As ULong?) As Task
        Return CheckSubscriptionsAsync(email, folder, scheduled:=False)
    End Function

    Private Async Function CheckSubscriptionsAsync(email As String, folder As ULong?, scheduled As Boolean) As Task
        Await subscriptionWork.WaitAsync()
        Try
            If Cerrando OrElse String.IsNullOrEmpty(email) OrElse (scheduled AndAlso Config.SubscriptionChecksPaused) Then Return
            Dim available = If(scheduled, SubscriptionService.GetDueDriveSubscriptions(email, Date.Now),
                               SubscriptionService.GetDriveSubscriptions(email))
            Dim subscriptions = available.Where(Function(item) Not folder.HasValue OrElse item.Folder = folder.Value).ToArray()
            If subscriptions.Length = 0 Then Return
            Dim files = SnapshotCloudQueue(email)
            Await Task.Run(Sub() ScanSubscriptions(email, subscriptions, files))
            If Cerrando Then Return
            For Each subscription In subscriptions
                If subscription.AutoQueue AndAlso subscription.LastError = 0 Then
                    Dim count = Await EnqueueSubscriptionFilesAsync(email, subscription.Folder, Nothing)
                    If count < 0 Then Log.WriteWarning("Could not enqueue cloud directory subscription: " & count.ToString())
                End If
            Next
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is ArgumentException
            Log.WriteWarning("Could not check cloud directory subscriptions: " & Log.SafeException(ex))
        Finally
            subscriptionWork.Release()
        End Try
    End Function

    Private Shared Sub ScanSubscriptions(email As String, subscriptions As MegaDriveSubscription(), queue As List(Of MegaDriveQueueItem))
        Dim service = SubscriptionService
        Dim sources As New Dictionary(Of Boolean, KeyValuePair(Of Integer, MegaDriveSnapshot))()
        Dim compare As Func(Of MegaDriveSnapshot, ULong, String, Dictionary(Of ULong, String), MegaDriveChanges) = Nothing
        For Each subscription In subscriptions
            Dim started = Date.UtcNow
            Dim source = ReadSubscriptionSource(email, subscription.Incoming.GetValueOrDefault(), sources)
            If Not subscription.Incoming.HasValue AndAlso (source.Key <> 0 OrElse Not source.Value.IsFolder(subscription.Folder)) Then
                Dim shares = ReadSubscriptionSource(email, True, sources)
                If shares.Key = 0 AndAlso shares.Value.IsFolder(subscription.Folder) Then
                    source = shares
                    subscription.Incoming = True
                ElseIf source.Key = 0 AndAlso shares.Key <> 0 Then
                    source = shares
                End If
            End If
            subscription.LastError = source.Key
            If source.Key = 0 Then
                Dim folder = source.Value.Find(subscription.Folder)
                If folder Is Nothing OrElse Not folder.IsFolder Then
                    subscription.LastError = -9
                Else
                    subscription.Incoming = subscription.Incoming.GetValueOrDefault()
                    subscription.Name = folder.Name
                    Try
                        If compare Is Nothing Then compare = service.DriveHistory.CreateBoundComparer(email, queue)
                        Dim changes = compare(source.Value, folder.Id, subscription.Destination, subscription.FolderPaths)
                        subscription.ApplyCheck(source.Value, changes)
                    Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is ArgumentException
                        subscription.LastError = MegaAccountService.LocalError
                        Log.WriteWarning("Could not compare cloud directory subscription: " & Log.SafeException(ex))
                    End Try
                End If
            End If
            subscription.LastCheckedUtc = started
            Dim result = service.SaveDriveSubscriptionCheck(email, subscription)
            If result <> 0 Then subscription.LastError = result
        Next
    End Sub

    Private Shared Function ReadSubscriptionSource(email As String, incoming As Boolean,
                                                   sources As Dictionary(Of Boolean, KeyValuePair(Of Integer, MegaDriveSnapshot))) As KeyValuePair(Of Integer, MegaDriveSnapshot)
        Dim result As KeyValuePair(Of Integer, MegaDriveSnapshot) = Nothing
        If sources.TryGetValue(incoming, result) Then Return result
        Dim snapshot As MegaDriveSnapshot = Nothing
        Dim code = SubscriptionService.ReadDrive(email, CancellationToken.None, snapshot, incomingShares:=incoming)
        result = New KeyValuePair(Of Integer, MegaDriveSnapshot)(code, snapshot)
        sources.Add(incoming, result)
        Return result
    End Function

    Friend Function ReadDriveSubscriptionReportsAsync(email As String) As Task(Of List(Of MegaDriveSubscription))
        Dim subscriptions = SubscriptionService.GetDriveSubscriptions(email)
        Dim queue = SnapshotCloudQueue(email)
        Return Task.Run(Function()
                            Try
                                SubscriptionService.DriveHistory.UpdateSubscriptionFileStates(email, subscriptions, queue)
                            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is ArgumentException
                                Log.WriteWarning("Could not refresh cloud directory subscription results: " & Log.SafeException(ex))
                            End Try
                            Return subscriptions
                        End Function)
    End Function

    Friend Async Function QueueDriveSubscriptionFilesAsync(email As String, selection As Dictionary(Of ULong, HashSet(Of ULong))) As Task(Of Integer)
        Await subscriptionWork.WaitAsync()
        Try
            Dim added As Integer
            Dim lastError As Integer
            For Each item In selection
                Dim count = Await EnqueueSubscriptionFilesAsync(email, item.Key, item.Value)
                If count < 0 Then
                    lastError = count
                Else
                    added += count
                End If
            Next
            Return If(added > 0, added, lastError)
        Finally
            subscriptionWork.Release()
        End Try
    End Function

    Private Async Function EnqueueSubscriptionFilesAsync(email As String, folder As ULong, selectedHandles As HashSet(Of ULong)) As Task(Of Integer)
        If Cerrando Then Return 0
        Dim access = SubscriptionService.GetAccessError(email)
        If access <> 0 Then Return access
        Dim subscription = SubscriptionService.GetDriveSubscriptions(email).FirstOrDefault(Function(item) item.Folder = folder)
        If subscription Is Nothing OrElse subscription.Files.Count = 0 Then Return 0
        Dim queue = SnapshotCloudQueue(email)
        Dim extractArchives = Config.ExtraerAutomaticamente
        Dim package As Paquete = Nothing
        Try
            package = Await Task.Run(Function()
                                         SubscriptionService.DriveHistory.UpdateSubscriptionFileStates(email, {subscription}, queue)
                                         Dim plan = subscription.PlanSelected(subscription.Files.Where(
                                             Function(file) file.Status = MegaDriveSubscriptionFileState.PendingReview AndAlso
                                             (selectedHandles Is Nothing OrElse selectedHandles.Contains(file.Handle))))
                                         If plan.Files.Count = 0 Then Return Nothing
                                         Return plan.Build(email, subscription.Destination, subscription.Caption, extractArchives, CancellationToken.None)
                                     End Function)
            If package Is Nothing Then Return 0
            Dim latest = SubscriptionService.GetDriveSubscriptions(email).FirstOrDefault(Function(item) item.Folder = folder)
            If Cerrando OrElse latest Is Nothing OrElse Not latest.MatchesSettings(subscription) Then Return 0
            Dim activeQueue = SnapshotCloudQueue(email).ToLookup(Function(file) file.Handle)
            For i As Integer = package.ListaFicheros.Count - 1 To 0 Step -1
                Dim file = package.ListaFicheros(i)
                If activeQueue(file.MegaDriveNodeHandle.Value).Any(Function(item) Not item.Completed AndAlso
                    String.Equals(item.LocalDirectory, file.RutaLocal, StringComparison.OrdinalIgnoreCase)) Then
                    package.ListaFicheros.RemoveAt(i)
                    file.Dispose()
                End If
            Next
            Dim count = package.ListaFicheros.Count
            If count > 0 Then AgregarPaquete(package, False, saveInBackground:=True)
            Return count
        Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is ArgumentException
            Log.WriteWarning("Could not enqueue cloud directory subscription: " & Log.SafeException(ex))
            Return MegaAccountService.LocalError
        Finally
            If package IsNot Nothing AndAlso Not IsPackageQueued(package) Then
                For Each file In package.ListaFicheros
                    file.Dispose()
                Next
            End If
        End Try
    End Function
End Class
