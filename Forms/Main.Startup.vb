Imports System.Threading
Imports System.Threading.Tasks

Partial Public Class Main
    Private _startupQueueLoading As Boolean
    Private _startupQueueStarted As Boolean
    Private _startupQueueCancellation As CancellationTokenSource
    Private _startupQueueCompletion As TaskCompletionSource(Of Boolean)
    Private _startupQueueTask As Task
    Private _startupQueueStatus As ToolStripStatusLabel
    Private ReadOnly _pendingStartupArguments As New Generic.List(Of String())()

    Private Sub PrepareStartupQueue()
        _startupQueueCancellation = New CancellationTokenSource()
        _startupQueueCompletion = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        ListaPaquetes = New Generic.List(Of Paquete)()
        Volatile.Write(_startupQueueLoading, True)
        UpdateStartupActionAvailability()
    End Sub

    Private Sub UpdateStartupActionAvailability()
        Dim ready As Boolean = Not Volatile.Read(_startupQueueLoading) AndAlso Config IsNot Nothing AndAlso Not Config.LoadTransientError AndAlso Not Cerrando
        btnPlay.Enabled = ready
        btnPause.Enabled = ready
        btnStop.Enabled = ready
    End Sub

    Private Sub BeginStartupQueueLoad()
        If _startupQueueStarted OrElse Cerrando OrElse IsDisposed Then Return
        _startupQueueStatus = New ToolStripStatusLabel(Language.GetText("Loading please wait")) With {.Name = "StartupQueueStatus"}
        StatusStrip1.Items.Add(_startupQueueStatus)
        ListaDescargas.EmptyListMsg = Language.GetText("Loading please wait")
        IniciarRecuperacionTransitoria()
        ' Flush the prepared window before any business startup/JIT work. The queue
        ' and file-lock retry waits run on the pool while the UI message loop runs.
        If Not IsSilent() Then Me.Refresh()
        _startupQueueTask = LoadStartupQueueAsync(AddressOf Paquete.CargarStartupQueue)
    End Sub

    Friend Async Function LoadStartupQueueAsync(loader As Func(Of CancellationToken, Generic.List(Of Paquete))) As Task
        If _startupQueueStarted OrElse Cerrando OrElse IsDisposed Then Return
        _startupQueueStarted = True
        Dim cancellation As CancellationTokenSource = _startupQueueCancellation
        Dim loaded As Generic.List(Of Paquete) = Nothing
        Dim published As Boolean = False
        Dim queueReady As Boolean = False
        Try
            loaded = Await Task.Run(Function() loader(cancellation.Token), cancellation.Token)
            cancellation.Token.ThrowIfCancellationRequested()
            If Cerrando OrElse IsDisposed Then
                _startupQueueCompletion.TrySetResult(False)
                Return
            End If
            SyncLock Mutex.ListaDescargas
                ' Imports that already reached the queue remain authoritative.
                If ListaPaquetes IsNot Nothing Then loaded.AddRange(ListaPaquetes)
                ListaPaquetes = loaded
                published = True
                Volatile.Write(_startupQueueLoading, False)
            End SyncLock
            LogStartupStage("queue loaded")
            ListaDescargas.EmptyListMsg = Language.GetText("OLV_EmptyList")
            RefreshListaDescargas(True)
            UpdateNavRailOverview()
            If _startupQueueStatus IsNot Nothing Then
                StatusStrip1.Items.Remove(_startupQueueStatus)
                _startupQueueStatus.Dispose()
                _startupQueueStatus = Nothing
            End If
            UpdateStartupActionAvailability()
            queueReady = True
        Catch ex As OperationCanceledException
            _startupQueueCompletion.TrySetResult(False)
        Catch ex As Exception
            Volatile.Write(_startupQueueLoading, True)
            _startupQueueCompletion.TrySetResult(False)
            Log.WriteError("Startup queue load failed; saving remains suspended: " & Log.SafeException(ex))
            If Not Cerrando AndAlso Not IsDisposed Then
                If _startupQueueStatus Is Nothing Then
                    _startupQueueStatus = New ToolStripStatusLabel() With {.Name = "StartupQueueStatus"}
                    StatusStrip1.Items.Add(_startupQueueStatus)
                End If
                _startupQueueStatus.Text = Language.GetText("Downloads could not be loaded. Restart the application to try again.")
            End If
        Finally
            If Not published Then Paquete.DisposeLoadedQueue(loaded)
            If Object.ReferenceEquals(_startupQueueCancellation, cancellation) Then _startupQueueCancellation = Nothing
            cancellation.Dispose()
        End Try
        If queueReady AndAlso Not Cerrando AndAlso Not IsDisposed Then
            Try
                ContinueStartupAfterQueueLoad()
            Catch ex As Exception
                _startupQueueCompletion.TrySetResult(False)
                Log.WriteError("Business startup failed after queue recovery: " & Log.SafeException(ex))
                ToastForm.ShowToast(Me, Language.GetText("Startup could not be completed. Restart the application to try again."))
            End Try
        End If
    End Function

    Private Sub ContinueStartupAfterQueueLoad()
        If Cerrando OrElse IsDisposed OrElse Volatile.Read(_startupQueueLoading) Then Return
        If IsSilent() AndAlso Not Config.LoadTransientError AndAlso
           (Not Config.CondicionesAceptadas OrElse Config.LoadErrorConfig <> Configuracion.ErrorConfigClass.SinErrores) Then
            _startupNeedsAttention = True
            Log.WriteWarning("Silent startup paused: terms or configuration require attention. Open the main window to continue.")
            IconoMinimizado.ShowBalloonTip(5000, Me.Text, Language.GetText("Startup requires attention. Open the main window to continue."), ToolTipIcon.Warning)
            Return
        End If
        CompleteStartup()
    End Sub

    Private Async Function WaitForStartupQueueAsync() As Task(Of Boolean)
        If _startupQueueCompletion IsNot Nothing AndAlso Not Await _startupQueueCompletion.Task Then Return False
        Return Not Cerrando AndAlso Not IsDisposed AndAlso Config IsNot Nothing AndAlso Not Config.LoadTransientError
    End Function

    Private Sub CancelStartupQueueLoad()
        If _startupQueueCancellation IsNot Nothing Then _startupQueueCancellation.Cancel()
        If _startupQueueCompletion IsNot Nothing Then _startupQueueCompletion.TrySetResult(False)
    End Sub

    Private Function DeferStartupArguments(args As String()) As Boolean
        If Not Volatile.Read(_startupQueueLoading) Then Return False
        SyncLock Mutex.ListaDescargas
            If Not _startupQueueLoading Then Return False
            _pendingStartupArguments.Add(CType(args.Clone(), String()))
            Return True
        End SyncLock
    End Function

    Private Sub ProcessDeferredStartupArguments()
        Dim pending As Generic.List(Of String())
        SyncLock Mutex.ListaDescargas
            pending = New Generic.List(Of String())(_pendingStartupArguments)
            _pendingStartupArguments.Clear()
        End SyncLock
        For Each args As String() In pending
            ProcessArgs(args)
        Next
    End Sub
End Class
