using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MegaDownloader
{
    /// <summary>
    /// Checks public folder links while the application is open. The folder is listed
    /// with the anonymous engine — the share key in the link is all it needs — and the
    /// result is compared with what the queue and the delivery history already hold.
    /// </summary>
    public sealed class MegaPublicFolderService : IDisposable
    {
        /// <summary>The share could not be listed: deleted, private, or the key is wrong.</summary>
        public const int ReadFailed = -1004;

        private readonly DownloadEngineService _engine;
        private readonly MegaPublicFolderStore _store;
        private readonly MegaDriveHistory _history;
        private readonly Configuracion _config;
        private readonly SemaphoreSlim _work = new SemaphoreSlim(1, 1);
        private Timer _timer;
        private int _tickRunning;
        private int _disposed;

        public MegaPublicFolderService(DownloadEngineService engine, MegaPublicFolderStore store,
            MegaDriveHistory history, Configuracion config)
        {
            _engine = engine;
            _store = store;
            _history = history;
            _config = config;
        }

        /// <summary>Raised after a check changed a report.</summary>
        public event EventHandler Changed;

        public void Start()
        {
            if (_timer is not null || Volatile.Read(ref _disposed) != 0)
                return;
            _timer = new Timer(_ => _ = TickAsync(), null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30));
        }

        private async System.Threading.Tasks.Task TickAsync()
        {
            if (Volatile.Read(ref _disposed) != 0 || !_engine.IsLoaded)
                return;
            if (Interlocked.CompareExchange(ref _tickRunning, 1, 0) != 0)
                return;
            try
            {
                DateTime now = DateTime.Now;
                foreach (MegaPublicFolderSubscription subscription in _store.Snapshot())
                {
                    if (Volatile.Read(ref _disposed) != 0)
                        break;
                    if (subscription.Paused)
                        continue;
                    DateTime? due = subscription.NextCheckLocal();
                    if (!due.HasValue || due.Value > now)
                        continue;
                    await CheckAsync(subscription.FolderId, scheduled: true).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Scheduled public folder check failed: " + Log.SafeException(ex));
            }
            finally
            {
                Volatile.Write(ref _tickRunning, 0);
            }
        }

        /// <summary>Checks one subscription, or every one when <paramref name="folderId"/>
        /// is null.</summary>
        public async System.Threading.Tasks.Task CheckAsync(string folderId, bool scheduled)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            await _work.WaitAsync().ConfigureAwait(false);
            try
            {
                IEnumerable<MegaPublicFolderSubscription> targets = folderId is null
                    ? _store.Snapshot()
                    : new[] { _store.Find(folderId) }.Where(item => item is not null);
                foreach (MegaPublicFolderSubscription subscription in targets.ToArray())
                {
                    if (Volatile.Read(ref _disposed) != 0)
                        return;
                    if (scheduled && subscription.Paused)
                        continue;
                    await CheckOneAsync(subscription).ConfigureAwait(false);
                }
                Changed?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _work.Release();
            }
        }

        private async System.Threading.Tasks.Task CheckOneAsync(MegaPublicFolderSubscription subscription)
        {
            DateTime started = DateTime.UtcNow;
            List<MegaPublicFolderFile> found;
            try
            {
                found = await System.Threading.Tasks.Task.Run(() => ListFiles(subscription)).ConfigureAwait(false);
            }
            catch (MegaQuotaExceededException)
            {
                subscription.LastError = MegaAccountService.OverQuota;
                subscription.LastCheckedUtc = started;
                _store.SaveCheck(subscription);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                ApplicationException)
            {
                subscription.LastError = ReadFailed;
                subscription.LastCheckedUtc = started;
                _store.SaveCheck(subscription);
                Log.WriteWarning("Could not read public folder " + subscription.FolderId + ": " + Log.SafeException(ex));
                return;
            }

            ApplyCheck(subscription, found);
            subscription.LastError = 0;
            subscription.LastCheckedUtc = started;
            int saved = _store.SaveCheck(subscription);
            if (saved != 0)
            {
                subscription.LastError = saved;
                return;
            }

            if (subscription.AutoQueue && !subscription.Paused)
            {
                int count = await QueueFilesAsync(subscription.FolderId, null).ConfigureAwait(false);
                if (count < 0)
                    Log.WriteWarning("Could not queue public folder files: " + count);
            }
        }

        private List<MegaPublicFolderFile> ListFiles(MegaPublicFolderSubscription subscription)
        {
            List<URLProcessor.FileURL> links = MegaFolderHelper.RetrieveLinksFromFolder(
                subscription.FolderId, subscription.FolderKey, string.Empty, string.Empty, null, CancellationToken.None);
            var files = new List<MegaPublicFolderFile>(links.Count);
            foreach (URLProcessor.FileURL link in links)
            {
                string fileId = Fichero.ExtraerFileID(link.URL);
                if (string.IsNullOrEmpty(fileId))
                    continue;
                files.Add(new MegaPublicFolderFile
                {
                    FileId = fileId,
                    Link = link.URL,
                    Name = PathGuard.SanitizeFileName(link.DisplayName ?? string.Empty, "file"),
                    RelativePath = link.Path ?? string.Empty,
                    Bytes = link.SizeBytes ?? 0L
                });
            }
            return files;
        }

        /// <summary>Compares a fresh listing with the previous report and the delivered
        /// files, and keeps only what still needs a decision.</summary>
        private void ApplyCheck(MegaPublicFolderSubscription subscription, List<MegaPublicFolderFile> discovered)
        {
            var extensions = new HashSet<string>(
                subscription.Extensions.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            if (extensions.Count > 0)
            {
                discovered = discovered
                    .Where(file => extensions.Contains(Path.GetExtension(file.Name).TrimStart('.')))
                    .ToList();
            }

            Dictionary<string, MegaPublicFolderFile> previous = subscription.Files
                .GroupBy(file => file.FileId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            var delivered = new HashSet<string>(
                ReadDeliveredPaths(subscription).Select(path => Path.GetFullPath(path)),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> queued = _engine.GetQueuedLinkSnapshot().Keys
                .ToHashSet(StringComparer.Ordinal);

            string root = PathGuard.GetSafePathUnderRoot(subscription.Destination, string.Empty);
            var kept = new List<MegaPublicFolderFile>(discovered.Count);
            foreach (MegaPublicFolderFile file in discovered)
            {
                if (previous.TryGetValue(file.FileId, out MegaPublicFolderFile prior))
                    file.Updated = prior.Bytes != file.Bytes || prior.Name != file.Name ||
                        prior.RelativePath != file.RelativePath;
                if (delivered.Contains(Path.GetFullPath(MegaPublicFolderSubscription.LocalPath(root, file))))
                {
                    file.Status = MegaPublicFolderFileState.Downloaded;
                }
                else if (queued.Contains(BatchLinkPreflight.Identity(Fichero.ExtraerFileID(file.Link),
                    Fichero.ExtraerFileKey(file.Link)) ?? string.Empty))
                {
                    file.Status = MegaPublicFolderFileState.Queued;
                }
                else
                {
                    file.Status = MegaPublicFolderFileState.PendingReview;
                }
                kept.Add(file);
            }
            subscription.Files = kept;
        }

        private IEnumerable<string> ReadDeliveredPaths(MegaPublicFolderSubscription subscription)
        {
            try
            {
                return _history.ReadPublicFolderPaths(subscription.FolderId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Log.WriteWarning("Could not read the public folder delivery history: " + Log.SafeException(ex));
                return Array.Empty<string>();
            }
        }

        /// <summary>Queues the selected pending files as one package rooted at the
        /// subscription destination.</summary>
        public async System.Threading.Tasks.Task<int> QueueFilesAsync(string folderId, HashSet<string> selected)
        {
            await _work.WaitAsync().ConfigureAwait(false);
            try
            {
                MegaPublicFolderSubscription subscription = _store.Find(folderId);
                if (subscription is null || subscription.Files.Count == 0)
                    return 0;
                List<MegaPublicFolderFile> files = subscription.Files
                    .Where(file => file.Status == MegaPublicFolderFileState.PendingReview &&
                        (selected is null || selected.Contains(file.FileId)))
                    .ToList();
                if (files.Count == 0)
                    return 0;

                bool extractArchives = _config.ExtraerAutomaticamente;
                Paquete package = null;
                try
                {
                    package = await System.Threading.Tasks.Task.Run(() =>
                    {
                        var links = files
                            .Select(file => new URLProcessor.FileURL(file.Link, file.RelativePath ?? string.Empty)
                            {
                                DisplayName = file.Name,
                                SizeBytes = file.Bytes
                            })
                            .ToList();
                        return DownloadPackageBuilder.Build(links, subscription.Caption, subscription.Destination,
                            createDirectory: true, extractArchives, string.Empty, CancellationToken.None,
                            megaAccountEmail: string.Empty, publicFolderId: subscription.FolderId);
                    }).ConfigureAwait(false);
                    if (package is null)
                        return 0;
                    foreach (Fichero file in package.SnapshotFicheros())
                    {
                        MegaPublicFolderFile source = files.FirstOrDefault(item =>
                            string.Equals(item.FileId, file.FileID, StringComparison.Ordinal));
                        if (source is not null && MegaPublicFolderSubscription.TryReadHandle(source.FileId, out ulong handle))
                            file.PublicFolderHandle = handle;
                    }
                    int added = _engine.AddUniquePackage(package);
                    if (added > 0)
                        Changed?.Invoke(this, EventArgs.Empty);
                    return added <= 0 ? Math.Min(added, 0) : added;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    Log.WriteWarning("Could not queue public folder files: " + Log.SafeException(ex));
                    return MegaAccountService.LocalError;
                }
                finally
                {
                    if (package is not null && !_engine.SnapshotPackages().Any(item => ReferenceEquals(item, package)))
                    {
                        foreach (Fichero file in package.SnapshotFicheros())
                            file.Dispose();
                    }
                }
            }
            finally
            {
                _work.Release();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
