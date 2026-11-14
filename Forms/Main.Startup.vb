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
        Dim ready As Boolean = Not Volatile.Read(_startupQueueLoading) AndAlso Config IsNot Nothing AndAlso
                               Not Config.LoadTransientError AndAlso Not Paquete.HasTransientLoadError AndAlso Not Cerrando
        btnPlay.Enabled = ready
        btnPause.Enabled = ready
        btnStop.Enabled = ready
    End Sub

    ''' <summary>Removes the "Downloads could not be loaded" banner when the queue becomes
    ''' healthy again (initial load success or later recovery). Must run on the UI thread.</summary>
    Private Sub ClearStartupQueueBanner()
        If _startupQueueStatus Is Nothing Then Return
        Try
            StatusStrip1.Items.Remove(_startupQueueStatus)
            _startupQueueStatus.Dispose()
        Catch ex As Exception
            Log.WriteError("Clearing startup queue banner failed: " & Log.SafeException(ex))
        Finally
            _startupQueueStatus = Nothing
        End Try
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
            ClearStartupQueueBanner()
            UpdateStartupActionAvailability()
            queueReady = True
        Catch ex As OperationCanceledException
            _startupQueueCompletion.TrySetResult(False)
        Catch ex As Exception
            ' Do NOT leave _startupQueueLoading True here. A permanent True would keep
            ' Play/Pause/Stop disabled and gate the recovery timer out. The loader
            ' classifies transient locks vs corrupt nodes; this generic catch only
            ' supplies write protection when the loader did not classify the failure
            ' (e.g. UI continuation or injected test loader), and never marks it corrupt.
            Volatile.Write(_startupQueueLoading, False)
            If Not Paquete.HasTransientLoadError AndAlso Not Paquete.HasCorruptLoadError Then
                Try
                    Paquete.ArmTransientWriteProtection()
                Catch logEx As Exception
                    Log.WriteError("Failed to arm queue write protection: " & Log.SafeException(logEx))
                End Try
            End If
            _startupQueueCompletion.TrySetResult(False)
            Log.WriteError("Startup queue load failed; writes are suspended until recovery: " & Log.SafeException(ex))
            If Not Cerrando AndAlso Not IsDisposed Then
                ' 加载已经失败,不能继续显示"载入中,请稍候":那句话会让用户一直等一个永远不来的结果。
                Dim failedText As String = Language.GetText("Queue_LoadFailed")
                If _startupQueueStatus IsNot Nothing Then _startupQueueStatus.Text = failedText
                ListaDescargas.EmptyListMsg = failedText
                UpdateStartupActionAvailability()
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
        ' TCS 是一次性的：启动失败后它永远停在 False，恢复计时器后来修好队列并跑完
        ' CompleteStartup 也无法再置 True。因此 Await 只用于让出 UI 线程直到加载结束，
        ' 放行与否必须按当前状态判断，否则添加链接/剪贴板导入会整个会话静默失效。
        If _startupQueueCompletion IsNot Nothing Then Await _startupQueueCompletion.Task
        Return Not Cerrando AndAlso Not IsDisposed AndAlso Config IsNot Nothing AndAlso
               Not Config.LoadTransientError AndAlso Not Paquete.HasTransientLoadError AndAlso
               Not Volatile.Read(_startupQueueLoading)
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
