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

    /// <summary>
    /// Cross-platform host for the download queue. The WinForms shell keeps its own
    /// UI-coupled scheduler; this service exposes the same engine primitives
    /// (<see cref="Paquete"/>, <see cref="Fichero"/>, <see cref="ThrottledStreamController"/>)
    /// through a small public surface so the Avalonia UI can load, run and edit the queue.
    /// </summary>
    public sealed class DownloadEngineService : IDisposable
    {
        private readonly object _stateGate = new object();
        private readonly object _scheduleGate = new object();
        private readonly List<Paquete> _packages = new List<Paquete>();
        private readonly DownloadScheduleController _scheduleController = new DownloadScheduleController();
        private readonly Timer _timer;
        private Configuracion _config;
        private DownloadEngineState _state = DownloadEngineState.Stopped;
        private int _disposed;
        private int _loaded;
        private int _tickRunning;
        private DateTime _lastSaveUtc = DateTime.MinValue;
        private volatile bool _saveRequested;

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
            RefreshAggregates();
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

            RefreshAggregates();

            // Only now is it safe to schedule ticks (and periodic saves).
            Volatile.Write(ref _loaded, 1);
            _lastSaveUtc = DateTime.UtcNow;
            if (Volatile.Read(ref _disposed) == 0)
                _timer.Change(500, 500);
        }

        public void StartAll()
        {
            _scheduleController.MarkManualStart();
            // WinForms parity (QuitarPausasIndividuales): "Start all" also resumes
            // files the user had paused one by one.
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete package in _packages)
                {
                    if (package.ListaFicheros is null)
                        continue;
                    foreach (Fichero file in package.ListaFicheros)
                        file.PausaIndividual = false;
                }
            }

            lock (_stateGate)
            {
                _state = DownloadEngineState.Running;
                ThrottledStreamController.GetController().Continuar();
            }
        }

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
                    file.PausaIndividual = false;
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
            lock (_stateGate)
                _state = DownloadEngineState.Running;

            lock (_scheduleGate)
            {
                lock (Mutex.ListaDescargas)
                {
                    file.PausaIndividual = false;
                    file.DescargaIndividual = true;
                }

                if (file.DescargaEstado() == Estado.Pausado)
                {
                    file.Resume();
                }
                else if (file.DescargaEstado() == Estado.EnCola && file.DescargaProcesada)
                {
                    file.Start(ref _config, Math.Max(1, _config.ConexionesPorFichero));
                }
                else if (file.DescargaEstado() == Estado.Erroneo && !file.EsErrorPermanente)
                {
                    file.ResetearDescarga(true);
                    file.SetDescargaEstado = Estado.EnCola;
                }
            }
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
            foreach (Paquete package in SnapshotPackages())
            {
                try
                {
                    package.ActualizarDatosDescarga();
                }
                catch (Exception ex)
                {
                    Log.WriteError("Updating package totals failed: " + Log.SafeException(ex));
                }
            }
        }

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

                RefreshAggregates();

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
                            if (quotaHold || file.PausaIndividual)
                                continue;
                            if (fileState == Estado.Pausado)
                            {
                                toResume ??= new List<Fichero>();
                                toResume.Add(file);
                            }
                            else if (fileState == Estado.EnCola && file.DescargaProcesada)
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
                            if (file.DescargaEstado() != Estado.EnCola || !file.DescargaProcesada ||
                                file.PausaIndividual)
                                continue;
                            file.Start(ref _config, connections);
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
                    file.PausaIndividual = false;
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
            try
            {
                Save();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Saving the queue on shutdown failed: " + Log.SafeException(ex));
            }
        }
    }
}
