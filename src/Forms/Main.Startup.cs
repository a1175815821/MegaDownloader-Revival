using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MegaDownloader
{

    public partial class Main
    {
        private bool _startupQueueLoading;
        private bool _startupQueueStarted;
        private CancellationTokenSource _startupQueueCancellation;
        private TaskCompletionSource<bool> _startupQueueCompletion;
        private Task _startupQueueTask;
        private ToolStripStatusLabel _startupQueueStatus;
        private readonly List<string[]> _pendingStartupArguments = new List<string[]>();

        private void PrepareStartupQueue()
        {
            _startupQueueCancellation = new CancellationTokenSource();
            _startupQueueCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ListaPaquetes = new List<Paquete>();
            Volatile.Write(ref _startupQueueLoading, true);
            UpdateStartupActionAvailability();
        }

        private void UpdateStartupActionAvailability()
        {
            bool ready = !Volatile.Read(ref _startupQueueLoading) && Config is not null && !Config.LoadTransientError && !Paquete.HasTransientLoadError && !Cerrando;
            btnPlay.Enabled = ready;
            btnPause.Enabled = ready;
            btnStop.Enabled = ready;
        }

        /// <summary>Removes the "Downloads could not be loaded" banner when the queue becomes
    /// healthy again (initial load success or later recovery). Must run on the UI thread.</summary>
        private void ClearStartupQueueBanner()
        {
            if (_startupQueueStatus is null)
                return;
            try
            {
                StatusStrip1.Items.Remove(_startupQueueStatus);
                _startupQueueStatus.Dispose();
            }
            catch (Exception ex)
            {
                Log.WriteError("Clearing startup queue banner failed: " + Log.SafeException(ex));
            }
            finally
            {
                _startupQueueStatus = null;
            }
        }

        private void BeginStartupQueueLoad()
        {
            if (_startupQueueStarted || Cerrando || IsDisposed)
                return;
            _startupQueueStatus = new ToolStripStatusLabel(Language.GetText("Loading please wait")) { Name = "StartupQueueStatus" };
            StatusStrip1.Items.Add(_startupQueueStatus);
            ListaDescargas.EmptyListMsg = Language.GetText("Loading please wait");
            IniciarRecuperacionTransitoria();
            // Flush the prepared window before any business startup/JIT work. The queue
            // and file-lock retry waits run on the pool while the UI message loop runs.
            if (!IsSilent())
                Refresh();
            _startupQueueTask = LoadStartupQueueAsync(Paquete.CargarStartupQueue);
        }

        internal async Task LoadStartupQueueAsync(Func<CancellationToken, List<Paquete>> loader)
        {
            if (_startupQueueStarted || Cerrando || IsDisposed)
                return;
            _startupQueueStarted = true;
            var cancellation = _startupQueueCancellation;
            List<Paquete> loaded = null;
            bool published = false;
            bool queueReady = false;
            try
            {
                loaded = await Task.Run(() => loader(cancellation.Token), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (Paquete.HasTransientLoadError)
                    throw new System.IO.IOException("Download list needs recovery.");
                if (Cerrando || IsDisposed)
                {
                    _startupQueueCompletion.TrySetResult(false);
                    return;
                }
                lock (Mutex.ListaDescargas)
                {
                    // Imports that already reached the queue remain authoritative.
                    if (ListaPaquetes is not null)
                        loaded.AddRange(ListaPaquetes);
                    ListaPaquetes = loaded;
                    published = true;
                    Volatile.Write(ref _startupQueueLoading, false);
                }
                LogStartupStage("queue loaded");
                ListaDescargas.EmptyListMsg = Language.GetText("OLV_EmptyList");
                RefreshListaDescargas(true);
                UpdateNavRailOverview();
                ClearStartupQueueBanner();
                UpdateStartupActionAvailability();
                queueReady = true;
            }
            catch (OperationCanceledException ex)
            {
                _startupQueueCompletion.TrySetResult(false);
            }
            catch (Exception ex)
            {
                // Do NOT leave _startupQueueLoading True here. A permanent True would keep
                // Play/Pause/Stop disabled and gate the recovery timer out. The loader
                // classifies transient locks vs corrupt nodes; this generic catch only
                // supplies write protection when the loader did not classify the failure
                // (e.g. UI continuation or injected test loader), and never marks it corrupt.
                Volatile.Write(ref _startupQueueLoading, false);
                if (!Paquete.HasTransientLoadError && !Paquete.HasCorruptLoadError)
                {
                    try
                    {
                        Paquete.ArmTransientWriteProtection();
                    }
                    catch (Exception logEx)
                    {
                        Log.WriteError("Failed to arm queue write protection: " + Log.SafeException(logEx));
                    }
                }
                _startupQueueCompletion.TrySetResult(false);
                Log.WriteError("Startup queue load failed; writes are suspended until recovery: " + Log.SafeException(ex));
                if (!Cerrando && !IsDisposed)
                {
                    // 加载已经失败,不能继续显示"载入中,请稍候":那句话会让用户一直等一个永远不来的结果。
                    string failedText = Language.GetText("Queue_LoadFailed");
                    if (_startupQueueStatus is not null)
                        _startupQueueStatus.Text = failedText;
                    ListaDescargas.EmptyListMsg = failedText;
                    UpdateStartupActionAvailability();
                }
            }
            finally
            {
                if (!published)
                    Paquete.DisposeLoadedQueue(loaded);
                if (ReferenceEquals(_startupQueueCancellation, cancellation))
                    _startupQueueCancellation = null;
                cancellation.Dispose();
            }
            if (queueReady && !Cerrando && !IsDisposed)
            {
                try
                {
                    ContinueStartupAfterQueueLoad();
                }
                catch (Exception ex)
                {
                    _startupQueueCompletion.TrySetResult(false);
                    Log.WriteError("Business startup failed after queue recovery: " + Log.SafeException(ex));
                    ToastForm.ShowToast(this, Language.GetText("Startup could not be completed. Restart the application to try again."));
                }
            }
        }

        private void ContinueStartupAfterQueueLoad()
        {
            if (Cerrando || IsDisposed || Volatile.Read(ref _startupQueueLoading))
                return;
            if (IsSilent() && !Config.LoadTransientError && (!Config.CondicionesAceptadas || Config.LoadErrorConfig != Configuracion.ErrorConfigClass.SinErrores))
            {
                _startupNeedsAttention = true;
                Log.WriteWarning("Silent startup paused: terms or configuration require attention. Open the main window to continue.");
                IconoMinimizado.ShowBalloonTip(5000, Text, Language.GetText("Startup requires attention. Open the main window to continue."), ToolTipIcon.Warning);
                return;
            }
            CompleteStartup();
        }

        private async Task<bool> WaitForStartupQueueAsync()
        {
            // TCS 是一次性的：启动失败后它永远停在 False，恢复计时器后来修好队列并跑完
            // CompleteStartup 也无法再置 True。因此 Await 只用于让出 UI 线程直到加载结束，
            // 放行与否必须按当前状态判断，否则添加链接/剪贴板导入会整个会话静默失效。
            if (_startupQueueCompletion is not null)
                await _startupQueueCompletion.Task;
            return !Cerrando && !IsDisposed && Config is not null && !Config.LoadTransientError && !Paquete.HasTransientLoadError && !Volatile.Read(ref _startupQueueLoading);
        }

        private void CancelStartupQueueLoad()
        {
            if (_startupQueueCancellation is not null)
                _startupQueueCancellation.Cancel();
            if (_startupQueueCompletion is not null)
                _startupQueueCompletion.TrySetResult(false);
        }

        private bool DeferStartupArguments(string[] args)
        {
            if (!Volatile.Read(ref _startupQueueLoading) && !Paquete.HasTransientLoadError)
                return false;
            lock (Mutex.ListaDescargas)
            {
                if (!_startupQueueLoading && !Paquete.HasTransientLoadError)
                    return false;
                _pendingStartupArguments.Add((string[])args.Clone());
                return true;
            }
        }

        private void ProcessDeferredStartupArguments()
        {
            List<string[]> pending;
            lock (Mutex.ListaDescargas)
            {
                pending = new List<string[]>(_pendingStartupArguments);
                _pendingStartupArguments.Clear();
            }
            foreach (string[] args in pending)
                ProcessArgs(args);
        }
    }
}