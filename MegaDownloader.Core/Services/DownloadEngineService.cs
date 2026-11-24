using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MegaDownloader
{
    public enum DownloadEngineState
    {
        Stopped,
        Running,
        Paused
    }

    /// <summary>How far a queue row moves when the user reorders it.</summary>
    public enum QueueMove
    {
        Top,
        Up,
        Down
    }

    /// <summary>
    /// Queue totals produced by the engine's own 500 ms sweep, so the workbench reads
    /// counts and speed without walking every task again on the UI thread.
    /// </summary>
    public readonly record struct QueueSummary(
        int Active,
        int Queued,
        int Paused,
        int Completed,
        int Failed,
        decimal SpeedKBs)
    {
        public static QueueSummary Empty => new(0, 0, 0, 0, 0, 0m);

        public int Total => Active + Queued + Paused + Completed + Failed;
    }

    /// <summary>
    /// Cross-platform host for the download queue. The WinForms shell keeps its own
    /// UI-coupled scheduler; this service exposes the same engine primitives
    /// (<see cref="Paquete"/>, <see cref="Fichero"/>, <see cref="ThrottledStreamController"/>)
    /// through a small public surface so the Avalonia UI can load, run and edit the queue.
    /// </summary>
    public sealed class DownloadEngineService : IDisposable, IDownloaderService
    {
        private static readonly object ExtractionWorkerGate = new object();
        private static bool _extractionWorkerRunning;

        private readonly object _stateGate = new object();
        private readonly object _scheduleGate = new object();
        private readonly List<Paquete> _packages = new List<Paquete>();
        private readonly DownloadScheduleController _scheduleController = new DownloadScheduleController();
        private readonly Timer _timer;
        private readonly System.ComponentModel.BackgroundWorker _extractionWorker;
        private Configuracion _config;
        private DownloadEngineState _state = DownloadEngineState.Stopped;
        private int _disposed;
        private int _loaded;
        private int _tickRunning;
        private DateTime _lastSaveUtc = DateTime.MinValue;
        private volatile bool _saveRequested;
        private sealed class SummaryBox
        {
            public readonly QueueSummary Value;
            public SummaryBox(QueueSummary value) => Value = value;
        }

        // Published as an immutable reference: the UI reads it from another thread and a
        // 36-byte struct read is not atomic.
        private SummaryBox _summary = new SummaryBox(QueueSummary.Empty);
        private int _summaryVersion;
        private int _queueVersion;

        public DownloadEngineService()
        {
            _config = new Configuracion();
            try
            {
                Language.InitLanguage(_config.Idioma);
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Language initialization failed: " + Log.SafeException(ex));
            }

            // Do not schedule before the queue is loaded: the first tick would
            // otherwise save an empty list over DownloadList.xml.
            _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);

            // Automatic archive extraction is driven by a worker, exactly like the
            // WinForms shell does it. Without it, a completed archive stays in
            // "Descomprimiendo" forever because nothing drains the extraction queue.
            _extractionWorker = new System.ComponentModel.BackgroundWorker
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true
            };
            _extractionWorker.DoWork += DescompresorController.DescompresorController_DoWork;
        }

        public Configuracion Config => _config;

        /// <summary>True once <see cref="LoadAsync"/> has read the saved queue.</summary>
        public bool IsLoaded => Volatile.Read(ref _loaded) != 0;

        public DownloadEngineState State
        {
            get
            {
                lock (_stateGate)
                    return _state;
            }
        }

        /// <summary>Snapshot of the live package list. The package/file instances are the
        /// engine's own objects, so callers must treat them as read-only.</summary>
        public IReadOnlyList<Paquete> SnapshotPackages()
        {
            lock (Mutex.ListaDescargas)
                return _packages.ToArray();
        }

        /// <summary>Identity → display metadata for every file already queued, used by
        /// link preflight to flag duplicates (WinForms Main.GetQueuedLinkSnapshot).</summary>
        /// <summary>Cloud-drive tasks in the queue for one account. Subscription checks use
        /// this to tell an already queued file from a missing one.</summary>
        public List<MegaDriveQueueItem> SnapshotCloudQueue(string email)
        {
            var files = new List<MegaDriveQueueItem>();
            lock (Mutex.ListaDescargas)
            {
                lock (Mutex.FicheroDownloader)
                {
                    foreach (Paquete package in _packages)
                    {
                        if (package.ListaFicheros is null)
                            continue;
                        foreach (Fichero file in package.ListaFicheros)
                        {
                            if (file.MegaDriveNodeHandle.HasValue &&
                                string.Equals(file.MegaAccountEmail, email, StringComparison.OrdinalIgnoreCase))
                            {
                                files.Add(new MegaDriveQueueItem(file.MegaDriveNodeHandle.Value, file.RutaLocal,
                                    file.NombreFichero, file.TamanoBytes,
                                    file.DescargaEstado() == Estado.Completado));
                            }
                        }
                    }
                }
            }
            return files;
        }

        /// <summary>
        /// Assigns one download identity to the selected pending tasks. Returns an empty
        /// string when the change was applied, otherwise the reason key for the caller.
        /// </summary>
        public string ChangeDownloadIdentity(IEnumerable<Fichero> files, string email)
        {
            lock (Mutex.FicheroDownloader)
            {
                foreach (Fichero file in files)
                {
                    if (file.MegaDriveNodeHandle.HasValue)
                        return "MegaDrive_AccountBound";
                    if (!file.CanChangeDownloadIdentity())
                        return "MegaAccount_QueueAccountInUse";
                    if (!string.IsNullOrEmpty(email) && Fichero.GetMegaAccountLink(file.URL) is null)
                        return "MegaAccount_QueueOnlyFiles";
                }
                foreach (Fichero file in files)
                    file.ChangeDownloadIdentity(email);
            }
            _saveRequested = true;
            Interlocked.Increment(ref _queueVersion);
            RefreshAggregatesCore();
            return string.Empty;
        }

        public Dictionary<string, URLProcessor.FileURL> GetQueuedLinkSnapshot()
        {
            var result = new Dictionary<string, URLProcessor.FileURL>(StringComparer.Ordinal);
            foreach (Paquete package in SnapshotPackages())
            {
                foreach (Fichero file in package.SnapshotFicheros())
                {
                    try
                    {
                        string key = BatchLinkPreflight.Identity(file.FileID, file.FileKey);
                        if (key is null)
                            continue;
                        result[key] = new URLProcessor.FileURL("", "")
                        {
                            DisplayName = file.DescargaProcesada ? file.NombreFichero : null,
                            SizeBytes = file.DescargaProcesada ? file.TamanoBytes : default(long?)
                        };
                    }
                    catch (ObjectDisposedException)
                    {
                        // A task can be removed after the reference snapshot.
                    }
                }
            }
            return result;
        }

        /// <summary>Append a package built by <see cref="DownloadPackageBuilder"/> to the
        /// queue. The caller owns the package until this returns true.</summary>
        public bool AddPackage(Paquete package)
        {
            if (package is null || !IsLoaded)
                return false;

            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                {
                    _packages.Add(package);
                    RenumberPrioritiesLocked();
                }
            }

            _saveRequested = true;
            Interlocked.Increment(ref _queueVersion);
            RefreshAggregatesCore();
            return true;
        }

        // Commit the duplicate check and package insertion together. Clipboard, manual
        // imports and second-instance activations can finish resolving the same link concurrently.
        public int AddUniquePackage(Paquete package)
        {
            if (package is null || !IsLoaded || Volatile.Read(ref _disposed) != 0)
                return -1;
            lock (_scheduleGate)
            {
                var known = new HashSet<string>(GetQueuedLinkSnapshot().Keys, StringComparer.Ordinal);
                package.ListaFicheros.RemoveAll(file =>
                {
                    string id = BatchLinkPreflight.Identity(file.FileID, file.FileKey);
                    if (id is null || known.Add(id))
                        return false;
                    file.Dispose();
                    return true;
                });
                if (package.ListaFicheros.Count == 0)
                    return 0;
                return AddPackage(package) ? package.ListaFicheros.Count : -1;
            }
        }

        /// <summary>
        /// Moves a package or one of its files in the queue. The physical list order is
        /// the scheduling order — the scheduler walks the packages in list order — so a
        /// move is a real swap, renumbered onto the stored priorities and saved with the
        /// queue. Restarting reproduces the order the user left.
        /// </summary>
        public bool MoveTo(object item, QueueMove move)
        {
            if (item is null || Volatile.Read(ref _disposed) != 0)
                return false;

            bool moved = false;
            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                {
                    moved = item switch
                    {
                        Paquete package => MovePackageLocked(package, move),
                        Fichero file => MoveFileLocked(file, move),
                        _ => false
                    };
                    if (moved)
                        RenumberPrioritiesLocked();
                }
            }

            if (!moved)
                return false;
            _saveRequested = true;
            Interlocked.Increment(ref _queueVersion);
            RefreshAggregatesCore();
            return true;
        }

        /// <summary>
        /// Places <paramref name="item"/> next to <paramref name="target"/> (before it,
        /// or after it). Drag-and-drop uses this; it takes the same list and priority
        /// path as the move commands. Packages move among packages; a file moves within
        /// its own package.
        /// </summary>
        /// <summary>True when <see cref="MoveBeside"/> could move the two rows together.</summary>
        public bool CanMoveBeside(object item, object target)
        {
            if (item is null || target is null || ReferenceEquals(item, target))
                return false;
            if (item is Paquete && target is Paquete)
                return true;
            if (item is not Fichero file || target is not Fichero anchor)
                return false;
            lock (Mutex.ListaDescargas)
            {
                Paquete owner = FindPackageLocked(file, out _);
                return owner is not null && ReferenceEquals(owner, FindPackageLocked(anchor, out _));
            }
        }

        public bool MoveBeside(object item, object target, bool after)
        {
            if (item is null || target is null || ReferenceEquals(item, target) ||
                Volatile.Read(ref _disposed) != 0)
                return false;

            bool moved = false;
            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                {
                    moved = (item, target) switch
                    {
                        (Paquete package, Paquete anchor) => MovePackageBesideLocked(package, anchor, after),
                        (Fichero file, Fichero anchor) => MoveFileBesideLocked(file, anchor, after),
                        _ => false
                    };
                    if (moved)
                        RenumberPrioritiesLocked();
                }
            }

            if (!moved)
                return false;
            _saveRequested = true;
            Interlocked.Increment(ref _queueVersion);
            RefreshAggregatesCore();
            return true;
        }

        private bool MovePackageBesideLocked(Paquete package, Paquete target, bool after)
        {
            int from = _packages.IndexOf(package);
            int to = _packages.IndexOf(target);
            if (from < 0 || to < 0 || from == to)
                return false;
            _packages.RemoveAt(from);
            if (from < to)
                to--;
            _packages.Insert(Math.Clamp(after ? to + 1 : to, 0, _packages.Count), package);
            return true;
        }

        private bool MoveFileBesideLocked(Fichero file, Fichero target, bool after)
        {
            Paquete owner = FindPackageLocked(file, out _);
            if (owner is null || !ReferenceEquals(owner, FindPackageLocked(target, out _)) || owner.ListaFicheros is null)
                return false;
            int from = owner.ListaFicheros.IndexOf(file);
            int to = owner.ListaFicheros.IndexOf(target);
            if (from < 0 || to < 0 || from == to)
                return false;
            owner.ListaFicheros.RemoveAt(from);
            if (from < to)
                to--;
            owner.ListaFicheros.Insert(Math.Clamp(after ? to + 1 : to, 0, owner.ListaFicheros.Count), file);
            return true;
        }

        private bool MovePackageLocked(Paquete package, QueueMove move)
        {
            int from = _packages.IndexOf(package);
            if (from < 0)
                return false;
            int to = move switch
            {
                QueueMove.Top => 0,
                QueueMove.Up => from - 1,
                _ => from + 1
            };
            if (to < 0 || to >= _packages.Count || to == from)
                return false;
            _packages.RemoveAt(from);
            _packages.Insert(to, package);
            return true;
        }

        private bool MoveFileLocked(Fichero file, QueueMove move)
        {
            Paquete owner = FindPackageLocked(file, out _);
            if (owner?.ListaFicheros is null)
                return false;
            int from = owner.ListaFicheros.IndexOf(file);
            if (from < 0)
                return false;
            int to = move switch
            {
                QueueMove.Top => 0,
                QueueMove.Up => from - 1,
                _ => from + 1
            };
            if (to < 0 || to >= owner.ListaFicheros.Count || to == from)
                return false;
            owner.ListaFicheros.RemoveAt(from);
            owner.ListaFicheros.Insert(to, file);
            return true;
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _loaded) != 0)
                return;

            var loaded = await Task.Run(() => Paquete.CargarStartupQueue(cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            lock (Mutex.ListaDescargas)
            {
                Paquete.DisposeLoadedQueue(_packages);
                _packages.Clear();
                _packages.AddRange(loaded);
                RenumberPrioritiesLocked();
            }

            if (_config.ComenzarDescargando && _config.DownloadSchedule.AllowsDownload(DateTime.Now))
            {
                lock (_stateGate)
                    _state = DownloadEngineState.Running;
            }
            else
            {
                lock (_stateGate)
                    _state = DownloadEngineState.Stopped;
            }

            RefreshAggregatesCore();
            Interlocked.Increment(ref _queueVersion);

            // Only now is it safe to schedule ticks (and periodic saves).
            Volatile.Write(ref _loaded, 1);
            _lastSaveUtc = DateTime.UtcNow;
            if (Volatile.Read(ref _disposed) == 0)
                _timer.Change(500, 500);

            StartExtractionWorker();
        }

        /// <summary>
        /// Starts the worker that drains the automatic-extraction queue. The WinForms
        /// shell does this with its own BackgroundWorker; without an equivalent the
        /// Avalonia shell leaves every completed archive in "Descomprimiendo".
        /// </summary>
        private void StartExtractionWorker()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            try
            {
                lock (ExtractionWorkerGate)
                {
                    // One worker per process: the controller is a process-wide
                    // singleton, so a second worker would race with the first one.
                    if (_extractionWorkerRunning)
                        return;
                    _extractionWorkerRunning = true;
                }
                if (!_extractionWorker.IsBusy)
                    _extractionWorker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                Log.WriteError("Starting the extraction worker failed: " + Log.SafeException(ex));
            }
        }

        public void StartAll()
        {
            TryStartAll(out _);
        }

        /// <summary>
        /// Starts the queue unless a guard forbids it: an active download schedule
        /// outside its window, or a MEGA quota quarantine. The WinForms shell refuses
        /// the same way and tells the user why; without this the queue flipped to
        /// "Running" and the next tick silently paused it again.
        /// </summary>
        /// <param name="reason">Empty when the queue was started; otherwise the reason.</param>
        /// <returns>True when the queue was set to running.</returns>
        public bool TryStartAll(out string reason) => TryStart(out reason, resumeIndividual: true);

        public bool TryStartPending(out string reason) => TryStart(out reason, resumeIndividual: false);

        private bool TryStart(out string reason, bool resumeIndividual)
        {
            reason = StartBlockedReason(_config, MegaQuotaManager.IsQuarantined());
            if (reason.Length > 0)
            {
                UserNotification.ShowInfo(reason, "Download");
                return false;
            }

            _scheduleController.MarkManualStart();
            // WinForms parity (QuitarPausasIndividuales): "Start all" also resumes
            // files the user had paused one by one.
            if (resumeIndividual)
            {
                lock (Mutex.ListaDescargas)
                    foreach (var file in SnapshotFiles())
                        file.PausaIndividual = false;
            }

            lock (_stateGate)
            {
                _state = DownloadEngineState.Running;
                ThrottledStreamController.GetController().Continuar();
            }
            return true;
        }

        /// <summary>
        /// Why a manual start must be refused, or an empty string when it may go ahead.
        /// </summary>
        /// <param name="config">The active configuration.</param>
        /// <param name="quotaQuarantined">True while a MEGA quota cooldown is active.</param>
        internal static string StartBlockedReason(Configuracion config, bool quotaQuarantined)
        {
            if (quotaQuarantined)
                return "The MEGA quota is in a cooling-off period. The queue stays paused until it expires.";

            if (config is null || !config.DownloadSchedule.AllowsDownload(DateTime.Now))
                return "The download schedule does not allow downloads right now.";

            return string.Empty;
        }

        /// <summary>
        /// Re-reads the download schedule after the settings changed. The controller
        /// caches the last window it saw, so an edited schedule would not take effect
        /// until the window changed by itself.
        /// </summary>
        public void ApplyScheduleSettings()
        {
            _scheduleController.Reset();
            ApplySchedule();
        }

        /// <summary>True when the download schedule allows transfers right now.</summary>
        public bool ScheduleAllowsDownloadsNow => _config.DownloadSchedule.AllowsDownload(DateTime.Now);

        public void PauseAll()
        {
            _scheduleController.MarkManualPause(_config.DownloadSchedule, DateTime.Now);
            lock (_stateGate)
                _state = DownloadEngineState.Paused;

            ThrottledStreamController.GetController().Abortar();
            foreach (Fichero file in SnapshotFiles())
            {
                // WinForms parity (QuitarDescargasIndividuales + PonerFicherosEnPausa).
                file.DescargaIndividual = false;
                if (file.DescargaEstado() == Estado.Descargando || file.DescargaEstado() == Estado.CreandoLocal)
                {
                    // WinForms PonerFicherosEnPausa pauses the files that are not
                    // individually paused and leaves PausaIndividual alone, so a file
                    // the user paused on its own stays paused after "Pause all".
                    if (file.PausaIndividual)
                        continue;
                    file.Pause();
                }
            }
        }

        public void StopAll()
        {
            _scheduleController.MarkManualPause(_config.DownloadSchedule, DateTime.Now);
            lock (_stateGate)
                _state = DownloadEngineState.Stopped;

            // The actual Stop() calls happen on the scheduler thread (see
            // StopActiveFiles): Stop can wait up to five seconds per file.
            foreach (Fichero file in SnapshotFiles())
                file.DescargaIndividual = false;
            ThrottledStreamController.GetController().Abortar();
        }

        public void StartFile(Fichero file)
        {
            if (file is null)
                return;

            // A single file obeys the same schedule window as the whole queue: the
            // WinForms shell refuses it with a toast instead of starting a transfer
            // that the next tick would pause again.
            string blocked = StartBlockedReason(_config, MegaQuotaManager.IsQuarantined());
            if (blocked.Length > 0)
            {
                UserNotification.ShowInfo(blocked, "Download");
                return;
            }
            if (CloudQuotaBlocks(file))
            {
                MegaAccountService.Status status = MegaAccountService.Current.GetStatus(file.MegaAccountEmail);
                string text = Language.GetText("MegaDrive_WaitingQuota");
                if (status.QuotaRetryUtc.HasValue)
                    text += " · " + FormatQuotaRemaining(status.QuotaRetryUtc.Value - DateTime.UtcNow);
                UserNotification.ShowInfo(text, "Download");
                return;
            }

            lock (_stateGate)
                _state = DownloadEngineState.Running;

            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                {
                    if (FindPackageLocked(file, out _) is null)
                        return;
                    file.PausaIndividual = false;
                    file.DescargaIndividual = true;
                }

                if (file.DescargaEstado() == Estado.Pausado)
                {
                    file.Resume();
                }
                else if (file.DescargaEstado() == Estado.EnCola)
                {
                    StartQueuedFile(file, Math.Max(1, _config.ConexionesPorFichero));
                }
                else if (file.DescargaEstado() == Estado.Erroneo && !file.EsErrorPermanente)
                {
                    file.ResetearDescarga(true);
                    file.SetDescargaEstado = Estado.EnCola;
                }
            }
        }

        private void StartQueuedFile(Fichero file, int connections)
        {
            Paquete owner;
            lock (Mutex.ListaDescargas)
                owner = FindPackageLocked(file, out _);
            if (owner is not null)
                file.Start(ref _config, connections, owner);
        }

        /// <summary>
        /// Puts every retryable failed task back in the queue. Permanent failures stay
        /// put: the link is gone and retrying only burns quota. Quota failures keep
        /// their partial progress so the retry resumes instead of starting over.
        /// </summary>
        public int RetryFailed()
        {
            var pending = new List<Fichero>();
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete package in _packages)
                {
                    if (package.ListaFicheros is null)
                        continue;
                    foreach (Fichero file in package.ListaFicheros)
                    {
                        if (file.DescargaEstado() == Estado.Erroneo && !file.EsErrorPermanente)
                            pending.Add(file);
                    }
                }
            }

            foreach (Fichero file in pending)
                ResetForRetry(file);

            if (pending.Count > 0)
            {
                _saveRequested = true;
                Log.WriteWarning("Retrying " + pending.Count + " failed task(s).");
            }
            return pending.Count;
        }

        /// <summary>Retries one failed task; returns false when it is a permanent failure.</summary>
        public bool RetryFile(Fichero file)
        {
            if (file is null || file.DescargaEstado() != Estado.Erroneo || file.EsErrorPermanente)
                return false;
            ResetForRetry(file);
            _saveRequested = true;
            return true;
        }

        private static void ResetForRetry(Fichero file)
        {
            // A private node's checkpoint is the only resume source; keep it on retry.
            file.ResetearDescarga(preserveProgress: file.FailedByQuota || file.MegaDriveNodeHandle.HasValue);
            file.PausaIndividual = false;
            file.SetDescargaEstado = Estado.EnCola;
        }

        public void PauseFile(Fichero file)
        {
            if (file is null)
                return;
            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                    file.PausaIndividual = true;
                if (file.DescargaEstado() == Estado.Descargando || file.DescargaEstado() == Estado.CreandoLocal)
                {
                    file.Pause();
                    ThrottledStreamController.GetController().Abortar(file.FileID);
                }
            }
        }

        public bool Remove(object item, bool deleteFiles)
        {
            if (item is null)
                return false;

            Paquete package;
            Fichero file = null;
            // Serialize with the scheduler's snapshot/start loop: once this block
            // finishes, no tick can still start the removed file.
            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                {
                    package = FindPackageLocked(item, out file);
                    if (package is null)
                        return false;

                    if (file is not null)
                    {
                        package.ListaFicheros.Remove(file);
                        ThrottledStreamController.GetController().RemoveId(file.FileID);
                        if (package.ListaFicheros.Count == 0)
                            _packages.Remove(package);
                    }
                    else
                    {
                        _packages.Remove(package);
                        if (package.ListaFicheros is not null)
                        {
                            foreach (Fichero child in package.ListaFicheros)
                                ThrottledStreamController.GetController().RemoveId(child.FileID);
                        }
                    }

                    RenumberPrioritiesLocked();
                }
            }

            if (file is not null)
            {
                RemoveFile(file, deleteFiles, package);
            }
            else if (package.ListaFicheros is not null)
            {
                foreach (Fichero child in package.ListaFicheros.ToArray())
                    RemoveFile(child, deleteFiles, package);
            }

            _saveRequested = true;
            Interlocked.Increment(ref _queueVersion);
            return true;
        }

        public void Save()
        {
            // Never write before LoadAsync finished: the in-memory list is still
            // empty and would replace the saved queue with an empty one.
            if (Volatile.Read(ref _loaded) == 0)
                return;

            List<Paquete> snapshot;
            lock (Mutex.ListaDescargas)
                snapshot = new List<Paquete>(_packages);

            // GuardarXML snapshots each package's file list under Mutex.ListaDescargas
            // itself, so the slow serialization/disk write stays off the queue lock.
            Paquete.GuardarEnFichero(snapshot);
            _saveRequested = false;
        }

        public void RefreshAggregates()
        {
            RefreshAggregatesCore();
        }

        /// <summary>
        /// The single queue sweep: refreshes each task's derived values and counts the
        /// states the workbench shows. The result is published for the UI to read, so a
        /// page refresh never has to walk the queue itself.
        /// </summary>
        private QueueSummary RefreshAggregatesCore()
        {
            int active = 0, queued = 0, paused = 0, completed = 0, failed = 0;
            decimal speed = 0m;
            int count = 0;

            foreach (Paquete package in SnapshotPackages())
            {
                try
                {
                    foreach (Fichero file in package.SnapshotFicheros())
                    {
                        file.ActualizarDatosDescarga();
                        count++;
                        switch (file.DescargaEstado())
                        {
                            case Estado.Descargando:
                            case Estado.CreandoLocal:
                            case Estado.Verificando:
                            case Estado.ComprobandoMD5:
                            case Estado.Descomprimiendo:
                            case Estado.EsperandoVolumenes:
                                active++;
                                break;
                            case Estado.EnCola:
                                queued++;
                                break;
                            case Estado.Pausado:
                                paused++;
                                break;
                            case Estado.Completado:
                                completed++;
                                break;
                            case Estado.Erroneo:
                                failed++;
                                break;
                        }

                        speed += file.DescargaVelocidadKBs();
                    }

                    package.ActualizarDatosDescarga();
                }
                catch (Exception ex)
                {
                    Log.WriteError("Updating package totals failed: " + Log.SafeException(ex));
                }
            }

            var summary = new QueueSummary(active, queued, paused, completed, failed, speed);
            Volatile.Write(ref _summary, new SummaryBox(summary));

            // The version moves whenever the totals or the task count change, so a page
            // can skip rebuilding rows on quiet ticks without diffing the queue.
            if (!summary.Equals(_lastSummary) || count != _lastFileCount)
            {
                _lastSummary = summary;
                _lastFileCount = count;
                Interlocked.Increment(ref _summaryVersion);
            }

            return summary;
        }

        private QueueSummary _lastSummary = QueueSummary.Empty;
        private int _lastFileCount;

        /// <summary>Latest totals from the engine sweep.</summary>
        public QueueSummary Summary => Volatile.Read(ref _summary).Value;

        /// <summary>Increments when the totals or the task count change.</summary>
        public int SummaryVersion => Volatile.Read(ref _summaryVersion);

        /// <summary>Increments whenever tasks are added or removed.</summary>
        public int QueueVersion => Volatile.Read(ref _queueVersion);

        private void Tick()
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _loaded) == 0)
                return;
            // A slow Stop()/Save() can outlive the 500 ms period; overlapping ticks
            // would double-start files and fight over the schedule gate.
            if (Interlocked.CompareExchange(ref _tickRunning, 1, 0) != 0)
                return;

            try
            {
                ApplySchedule();

                DownloadEngineState state = State;
                if (state == DownloadEngineState.Running)
                {
                    WakeAccountFailures();
                    RunScheduler();
                }
                else if (state == DownloadEngineState.Paused)
                {
                    PauseActiveFiles();
                }
                else
                {
                    StopActiveFiles();
                }

                RefreshAggregatesCore();

                if (_saveRequested || (DateTime.UtcNow - _lastSaveUtc).TotalSeconds >= 5)
                {
                    _lastSaveUtc = DateTime.UtcNow;
                    Save();
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Download engine tick failed: " + Log.SafeException(ex));
            }
            finally
            {
                Volatile.Write(ref _tickRunning, 0);
            }
        }

        /// <summary>
        /// WinForms parity (PonerFicherosADescargar): resume paused files first, then
        /// start queued ones, within the simultaneous-download limit. Resuming here is
        /// what makes "Pause all" followed by "Start all" work: FileDownloader keeps its
        /// own IsPaused flag, so releasing the throttled streams alone is not enough.
        /// </summary>
        private void RunScheduler()
        {
            bool quotaHold = MegaQuotaManager.IsQuarantined();
            List<Fichero> toResume = null;
            List<Fichero> toStart = null;
            int active = 0;

            // Hold the schedule gate across the snapshot and the start loop: Remove()
            // takes the same gate around its list mutation, so a file cannot be removed
            // and then started from a stale candidate list.
            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                {
                    foreach (Paquete package in _packages)
                    {
                        if (package.ListaFicheros is null)
                            continue;
                        foreach (Fichero file in package.ListaFicheros)
                        {
                            Estado fileState = file.DescargaEstado();
                            if (OccupiesDownloadSlot(fileState))
                            {
                                active++;
                                continue;
                            }
                            if (quotaHold || file.PausaIndividual || !AccountBackendAllowsStart(file))
                                continue;
                            if (fileState == Estado.Pausado)
                            {
                                toResume ??= new List<Fichero>();
                                toResume.Add(file);
                            }
                            else if (fileState == Estado.EnCola)
                            {
                                toStart ??= new List<Fichero>();
                                toStart.Add(file);
                            }
                        }
                    }
                }

                int slots = Math.Max(1, _config.DescargasSimultaneas) - active;
                if (slots <= 0)
                    return;

                // NumConexionesDisponibles is a per-file connection budget, not a count
                // of free download slots; FileDownloader takes min(ConexionesPorFichero, value).
                int connections = Math.Max(1, _config.ConexionesPorFichero);

                if (toResume is not null)
                {
                    foreach (Fichero file in toResume)
                    {
                        if (slots <= 0)
                            break;
                        try
                        {
                            if (file.DescargaEstado() != Estado.Pausado || file.PausaIndividual)
                                continue;
                            file.Resume();
                            slots--;
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("Resuming a download failed: " + Log.SafeException(ex));
                        }
                    }
                }

                if (toStart is not null)
                {
                    foreach (Fichero file in toStart)
                    {
                        if (slots <= 0)
                            break;
                        try
                        {
                            if (file.DescargaEstado() != Estado.EnCola ||
                                file.PausaIndividual)
                                continue;
                            StartQueuedFile(file, connections);
                            slots--;
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("Starting download failed: " + Log.SafeException(ex));
                        }
                    }
                }
            }
        }

        /// <summary>Account tasks wait for a connected, non-quota-blocked account; the
        /// anonymous engine's cooldown does not apply to them.</summary>
        private static bool AccountBackendAllowsStart(Fichero file)
        {
            if (!file.UsesMegaAccount || file.HasRecordedMegaAccountOutput)
                return true;
            if (file.IsMegaAccountConnecting)
                return false;
            return !CloudQuotaBlocks(file);
        }

        private static bool CloudQuotaBlocks(Fichero file)
        {
            if (!file.MegaDriveNodeHandle.HasValue || file.HasRecordedMegaAccountOutput)
                return false;
            MegaAccountService.Status account = MegaAccountService.Current.GetStatus(file.MegaAccountEmail);
            return account.SignedIn && account.QuotaReached;
        }

        private static string FormatQuotaRemaining(TimeSpan span)
        {
            int totalSec = Math.Max(1, (int)Math.Ceiling(span.TotalSeconds));
            if (totalSec < 120)
            {
                if (totalSec < 60)
                    return totalSec + " s";
                return "1 min " + (totalSec - 60) + " s";
            }
            int totalMin = totalSec / 60;
            int h = totalMin / 60;
            int m = totalMin % 60;
            return h > 0 ? h + " h " + m + " min" : m + " min";
        }

        /// <summary>
        /// Account retries follow the account state instead of the anonymous quota flag:
        /// a signed-in account with allowance retries session/transient failures, and a
        /// quota failure wakes when the reported deadline passes or another account can
        /// take the public transfer.
        /// </summary>
        private void WakeAccountFailures()
        {
            MegaAccountService service = MegaAccountService.Current;
            if (service.IsRestoring)
                return;

            var pending = new List<Fichero>();
            DateTime retryBefore = DateTime.Now.AddMinutes(-Math.Max(1, _config.ResetearErroresPeriodoMinutos));
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete package in _packages)
                {
                    if (package.ListaFicheros is null)
                        continue;
                    foreach (Fichero file in package.ListaFicheros)
                    {
                        if (!file.UsesMegaAccount || file.DescargaEstado() != Estado.Erroneo ||
                            file.EsErrorPermanente || file.PausaIndividual)
                        {
                            continue;
                        }

                        MegaAccountService.Status status = service.GetStatus(file.EffectiveMegaAccountEmail);
                        bool available = status.SignedIn && !status.Connecting && !status.QuotaReached;
                        bool retryDue = file.FechaUltimoError.GetValueOrDefault() < retryBefore;
                        if (file.FailedByQuota)
                        {
                            if (available && (!status.QuotaDelayUnknown || retryDue))
                            {
                                pending.Add(file);
                            }
                            else if (!file.MegaDriveNodeHandle.HasValue)
                            {
                                var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    file.EffectiveMegaAccountEmail
                                };
                                string next = service.NextTransferAccount(file.EffectiveMegaAccountEmail, tried, retryDue);
                                if (next.Length > 0)
                                {
                                    file.RecordMegaAccountTransfer(next);
                                    pending.Add(file);
                                }
                            }
                        }
                        else if (available)
                        {
                            int code = file.MegaAccountErrorCode.GetValueOrDefault();
                            if (code == MegaAccountService.SessionExpired || code == MegaAccountService.Busy ||
                                (_config.ResetearErrores && retryDue && Fichero.IsTransientMegaAccountError(code)))
                            {
                                pending.Add(file);
                            }
                        }
                    }
                }
            }

            foreach (Fichero file in pending)
            {
                file.MegaDriveWaitingForQuota = false;
                file.ResetearDescarga(preserveProgress: true);
                file.SetDescargaEstado = Estado.EnCola;
                _saveRequested = true;
                Log.WriteInfo("Retrying MEGA account task " + file.FileID + " automatically.");
            }
        }

        /// <summary>WinForms parity (PonerFicherosEnPausa), re-run every tick so files
        /// that were still starting when the pause was requested are caught too.</summary>
        private void PauseActiveFiles()
        {
            foreach (Fichero file in SnapshotFiles())
            {
                Estado state = file.DescargaEstado();
                if (state != Estado.Descargando && state != Estado.CreandoLocal)
                    continue;
                try
                {
                    // A file the user paused on its own keeps its marker, so it is not
                    // restarted by the scheduler after the global pause.
                    if (file.PausaIndividual)
                        continue;
                    file.Pause();
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Pausing a download failed: " + Log.SafeException(ex));
                }
            }
        }

        /// <summary>WinForms parity (PararDescargaFicheros). Stop() waits for the
        /// downloader to finish its current bookkeeping, so it runs here rather than on
        /// the UI thread.</summary>
        private void StopActiveFiles()
        {
            foreach (Fichero file in SnapshotFiles())
            {
                Estado state = file.DescargaEstado();
                if (state != Estado.Descargando && state != Estado.CreandoLocal && state != Estado.Pausado)
                    continue;
                try
                {
                    file.Stop();
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Stopping a download failed: " + Log.SafeException(ex));
                }
            }
        }

        private void ApplySchedule()
        {
            DownloadScheduleController.ScheduleAction action =
                _scheduleController.Evaluate(_config.DownloadSchedule, DateTime.Now,
                    State == DownloadEngineState.Running);

            if (action == DownloadScheduleController.ScheduleAction.Pause)
            {
                lock (_stateGate)
                    _state = DownloadEngineState.Paused;
                ThrottledStreamController.GetController().Abortar();
                Log.WriteInfo("Download schedule paused the queue.");
            }
            else if (action == DownloadScheduleController.ScheduleAction.Start)
            {
                lock (_stateGate)
                    _state = DownloadEngineState.Running;
                ThrottledStreamController.GetController().Continuar();
                Log.WriteInfo("Download schedule started the queue.");
            }
        }

        private List<Fichero> SnapshotFiles()
        {
            var files = new List<Fichero>();
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete package in _packages)
                {
                    if (package.ListaFicheros is not null)
                        files.AddRange(package.ListaFicheros);
                }
            }
            return files;
        }

        private Paquete FindPackageLocked(object item, out Fichero file)
        {
            file = null;
            foreach (Paquete package in _packages)
            {
                if (ReferenceEquals(package, item))
                    return package;
                if (package.ListaFicheros is null)
                    continue;
                foreach (Fichero child in package.ListaFicheros)
                {
                    if (ReferenceEquals(child, item))
                    {
                        file = child;
                        return package;
                    }
                }
            }
            return null;
        }

        private void RenumberPrioritiesLocked()
        {
            int priority = 0;
            foreach (Paquete package in _packages)
            {
                priority++;
                package.SetDescargaPrioridad = priority;
                if (package.ListaFicheros is null)
                    continue;
                foreach (Fichero file in package.ListaFicheros)
                {
                    priority++;
                    file.SetDescargaPrioridad = priority;
                }
            }
        }

        private static bool IsActive(Estado state)
        {
            return state == Estado.Descargando || state == Estado.CreandoLocal ||
                   state == Estado.Verificando || state == Estado.ComprobandoMD5 ||
                   state == Estado.Descomprimiendo || state == Estado.EsperandoVolumenes;
        }

        /// <summary>WinForms parity: only downloads in flight consume a
        /// DescargasSimultaneas slot; MD5 verification and extraction run after the
        /// transfer and must not block the next queued file.</summary>
        private static bool OccupiesDownloadSlot(Estado state)
        {
            return state == Estado.Descargando || state == Estado.CreandoLocal ||
                   state == Estado.Verificando;
        }

        private static void RemoveFile(Fichero file, bool deleteFiles, Paquete package)
        {
            if (deleteFiles)
                file.DiscardPrivateMegaCheckpoint();
            try
            {
                file.RemovePendingExtraction();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Removing pending extraction failed: " + Log.SafeException(ex));
            }

            if (deleteFiles)
                file.LimpiezaPaquete = package;

            if (IsActive(file.DescargaEstado()) || file.DescargaEstado() == Estado.Pausado)
            {
                EventHandler handler = null;
                handler = (sender, args) =>
                {
                    file.CancellationComplete -= handler;
                    // WinForms parity: a cancellation cycle may deliver the callback
                    // twice (e.g. Stop() notifying synchronously and the downloader
                    // later); delete and dispose only once.
                    if (Interlocked.Exchange(ref file.DisposalHandled, 1) != 0)
                        return;
                    try
                    {
                        if (file.MarcadoParaBorrarFicheroLocal)
                            file.BorrarFicheroLocal();
                        Paquete cleanupPackage = file.LimpiezaPaquete;
                        file.LimpiezaPaquete = null;
                        if (cleanupPackage is not null)
                            LimpiarDirectoriosVacios(cleanupPackage, new[] { file });
                    }
                    finally
                    {
                        file.Dispose();
                    }
                };
                file.CancellationComplete += handler;
                file.MarcadoParaBorrarFicheroLocal = deleteFiles;
                file.Stop();

                // Fichero.Stop() only reports the cancellation when the downloader was
                // still startable or stoppable. A downloader that already ended (its
                // Canceled event fired before this removal) never reports again, so the
                // partial file would stay on disk and the task would leak. Wait briefly
                // for the callback and clean up when it never arrives.
                if (Volatile.Read(ref file.DisposalHandled) == 0)
                {
                    for (int i = 0; i < 100 && Volatile.Read(ref file.DisposalHandled) == 0; i++)
                        Thread.Sleep(50);
                    if (Volatile.Read(ref file.DisposalHandled) == 0)
                    {
                        file.CancellationComplete -= handler;
                        handler(file, EventArgs.Empty);
                    }
                }
            }
            else
            {
                file.MarcadoParaBorrarFicheroLocal = deleteFiles;
                file.BorrarFicheroLocal();
                file.Dispose();
                if (deleteFiles)
                    LimpiarDirectoriosVacios(package, new[] { file });
            }
        }

        /// <summary>WinForms parity (Main.LimpiarDirectoriosVacios): after deleting a
        /// package's files, remove the directories that became empty, deepest first.</summary>
        private static void LimpiarDirectoriosVacios(Paquete package, IEnumerable<Fichero> files)
        {
            try
            {
                if (package is null || string.IsNullOrWhiteSpace(package.RutaLocal))
                    return;

                char separator = System.IO.Path.DirectorySeparatorChar;
                string packageRoot = System.IO.Path.GetFullPath(package.RutaLocal)
                    .TrimEnd(separator, System.IO.Path.AltDirectorySeparatorChar);

                var directories = new List<string>();
                foreach (Fichero file in files)
                {
                    try
                    {
                        if (file is null || string.IsNullOrWhiteSpace(file.RutaLocal))
                            continue;
                        string directory = System.IO.Path.GetFullPath(file.RutaLocal)
                            .TrimEnd(separator, System.IO.Path.AltDirectorySeparatorChar);
                        bool insidePackage = directory.Equals(packageRoot, StringComparison.OrdinalIgnoreCase) ||
                                             directory.StartsWith(packageRoot + separator, StringComparison.OrdinalIgnoreCase);
                        if (insidePackage && !directories.Exists(
                                existing => string.Equals(existing, directory, StringComparison.OrdinalIgnoreCase)))
                        {
                            directories.Add(directory);
                        }
                    }
                    catch
                    {
                    }
                }

                directories.Sort((x, y) => y.Length.CompareTo(x.Length));
                foreach (string directory in directories)
                {
                    try
                    {
                        if (!directory.Equals(packageRoot, StringComparison.OrdinalIgnoreCase) &&
                            System.IO.Directory.Exists(directory) &&
                            System.IO.Directory.GetFileSystemEntries(directory).Length == 0)
                        {
                            System.IO.Directory.Delete(directory);
                            Log.WriteInfo("Deleted empty directory: " + directory);
                        }
                    }
                    catch
                    {
                    }
                }

                try
                {
                    if (package.CrearSubdirectorio && System.IO.Directory.Exists(packageRoot) &&
                        System.IO.Directory.GetFileSystemEntries(packageRoot).Length == 0)
                    {
                        System.IO.Directory.Delete(packageRoot);
                        Log.WriteInfo("Deleted empty package directory: " + packageRoot);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Could not delete empty package directory " + packageRoot + ": " + Log.SafeException(ex));
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("LimpiarDirectoriosVacios failed: " + Log.SafeException(ex));
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            _timer.Dispose();

            // Shutdown order matters: stop dispatching new work, let the transfers in
            // flight commit what they have written, and only then persist the queue.
            // Saving first records progress the disk has not received yet, so a restart
            // could ask for bytes that were never written.
            try
            {
                lock (_stateGate)
                    _state = DownloadEngineState.Stopped;
                foreach (Fichero file in SnapshotFiles())
                {
                    Estado state = file.DescargaEstado();
                    if (state != Estado.Descargando && state != Estado.CreandoLocal && state != Estado.Pausado)
                        continue;
                    file.Stop();
                }
                RefreshAggregatesCore();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Stopping transfers on shutdown failed: " + Log.SafeException(ex));
            }

            try
            {
                // Stop the extraction worker. Only the worker is cancelled: the
                // controller's RequestCancel is a permanent, process-wide flag, so
                // calling it here would block extraction for any later engine in the
                // same process.
                if (_extractionWorker.IsBusy)
                    _extractionWorker.CancelAsync();
                lock (ExtractionWorkerGate)
                    _extractionWorkerRunning = false;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Stopping the extraction worker failed: " + Log.SafeException(ex));
            }
            try
            {
                Save();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Saving the queue on shutdown failed: " + Log.SafeException(ex));
            }
        }

        #region IDownloaderService (browser remote control)

        // Aggregate state for the web remote control. Counts come from the same package
        // snapshots the queue page uses, so both surfaces always agree.

        public decimal? ControlRemotoObtenerVelocidad()
        {
            decimal total = 0m;
            foreach (Paquete package in SnapshotPackages())
            {
                try
                {
                    total += package.DescargaVelocidadKBs();
                }
                catch (ObjectDisposedException)
                {
                }
            }
            return total;
        }

        public int? ControlRemotoObtenerDescargasActivas() => CountFiles(IsActive);

        public int? ControlRemotoObtenerDescargasCompletadas() => CountFiles(state => state == Estado.Completado);

        public int? ControlRemotoObtenerDescargasErroneas() => CountFiles(state => state == Estado.Erroneo);

        public int? ControlRemotoObtenerDescargasEnCola() => CountFiles(state => state == Estado.EnCola);

        public DownloaderEstado ControlRemotoObtenerEstado() => State switch
        {
            DownloadEngineState.Running => DownloaderEstado.Descargando,
            DownloadEngineState.Paused => DownloaderEstado.Pausa,
            _ => DownloaderEstado.Parado
        };

        public void ControlRemotoDescargar() => StartAll();

        public void ControlRemotoParar() => StopAll();

        public string ControlRemotoAgregarLinks(string Links, string NombrePaquete, bool CrearDirectorio)
        {
            if (!IsLoaded)
                return Language.GetText("Loading please wait");
            if (Paquete.HasTransientLoadError)
                return Language.GetText("Queue_LoadFailed");

            List<string> urls = URLExtractor.ExtraerURLs(Links);
            if (urls.Count == 0)
                return Language.GetText("No valid URLs have been inserted");

            // Same destination rule as the desktop intake: the configured folder, or the
            // system Downloads folder when none is set.
            string destination = LinkIntakeService.DownloadFolder(_config);
            System.IO.Directory.CreateDirectory(destination);

            List<URLProcessor.FileURL> expanded;
            try
            {
                expanded = URLProcessor.ProcessURLs(urls, ref _config);
            }
            catch (Exception ex)
            {
                Log.WriteError("Remote link intake failed: " + Log.SafeException(ex));
                return ex.Message;
            }

            if (expanded is null || expanded.Count == 0)
                return Language.GetText("No valid URLs have been inserted");

            Paquete package;
            try
            {
                package = DownloadPackageBuilder.Build(
                    expanded,
                    NombrePaquete ?? string.Empty,
                    destination,
                    CrearDirectorio,
                    _config.ExtraerAutomaticamente,
                    string.Empty,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.WriteError("Building a package from remote links failed: " + Log.SafeException(ex));
                return ex.Message;
            }

            // The same duplicate check as the desktop intake: a link that is already
            // queued is reported instead of being added a second time.
            int added = AddUniquePackage(package);
            if (added <= 0)
            {
                if (added == 0)
                    return Language.GetText("Preflight_AllQueued");
                foreach (Fichero file in package.SnapshotFicheros())
                    file.Dispose();
                return Language.GetText("Loading please wait");
            }

            // The remote control adds links with the same intent as the browser: the
            // transfer starts right away, unless the schedule or the quota stops it.
            if (!TryStartAll(out string reason))
                Log.WriteWarning("Remote links added but the queue did not start: " + reason);

            return string.Empty;
        }

        private int CountFiles(Func<Estado, bool> predicate)
        {
            int count = 0;
            foreach (Paquete package in SnapshotPackages())
            {
                foreach (Fichero file in package.SnapshotFicheros())
                {
                    try
                    {
                        if (predicate(file.DescargaEstado()))
                            count++;
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
            return count;
        }

        #endregion
    }
}
