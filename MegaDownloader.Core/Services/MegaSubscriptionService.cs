using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MegaDownloader
{
    /// <summary>
    /// Runs bound-folder checks while the application is open: compares the cloud tree
    /// with the delivery history, records the latest findings and, when the subscription
    /// asks for it, queues the new files through the desktop engine.
    /// </summary>
    public sealed class MegaSubscriptionService : IDisposable
    {
        private readonly DownloadEngineService _engine;
        private readonly MegaAccountService _accounts;
        private readonly SemaphoreSlim _work = new SemaphoreSlim(1, 1);
        private Timer _timer;
        private int _tickRunning;
        private int _disposed;

        public MegaSubscriptionService(DownloadEngineService engine, MegaAccountService accounts)
        {
            _engine = engine;
            _accounts = accounts;
        }

        /// <summary>Raised after a scheduled or manual check changed subscription reports.</summary>
        public event EventHandler Changed;

        public void Start()
        {
            if (_timer is not null || Volatile.Read(ref _disposed) != 0)
                return;
            _timer = new Timer(_ => _ = TickAsync(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
        }

        private async Task TickAsync()
        {
            if (Volatile.Read(ref _disposed) != 0 || !_engine.IsLoaded || _accounts.IsRestoring)
                return;
            if (Interlocked.CompareExchange(ref _tickRunning, 1, 0) != 0)
                return;
            try
            {
                foreach (MegaAccountService.Status account in _accounts.GetAccounts())
                {
                    if (!account.SignedIn || account.Connecting || Volatile.Read(ref _disposed) != 0)
                        continue;
                    await CheckAsync(account.Email, null, scheduled: true).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Scheduled cloud directory check failed: " + Log.SafeException(ex));
            }
            finally
            {
                Volatile.Write(ref _tickRunning, 0);
            }
        }

        /// <summary>Checks every subscription of one account, or one bound folder.</summary>
        public async Task CheckAsync(string email, ulong? folder, bool scheduled)
        {
            if (string.IsNullOrEmpty(email) || Volatile.Read(ref _disposed) != 0)
                return;
            await _work.WaitAsync().ConfigureAwait(false);
            try
            {
                List<MegaDriveSubscription> available = scheduled
                    ? _accounts.GetDueDriveSubscriptions(email, DateTime.Now)
                    : _accounts.GetDriveSubscriptions(email);
                MegaDriveSubscription[] subscriptions = available
                    .Where(item => (!scheduled || !item.Paused) && (!folder.HasValue || item.Folder == folder.Value))
                    .ToArray();
                if (subscriptions.Length == 0)
                    return;

                List<MegaDriveQueueItem> queue = _engine.SnapshotCloudQueue(email);
                await Task.Run(() => ScanSubscriptions(email, subscriptions, queue)).ConfigureAwait(false);

                foreach (MegaDriveSubscription subscription in subscriptions)
                {
                    if (subscription.AutoQueue && !subscription.Paused && subscription.LastError == 0)
                    {
                        int count = await EnqueueSubscriptionFilesAsync(email, subscription.Folder, null)
                            .ConfigureAwait(false);
                        if (count < 0)
                            Log.WriteWarning("Could not enqueue cloud directory subscription: " + count);
                    }
                }
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
            {
                Log.WriteWarning("Could not check cloud directory subscriptions: " + Log.SafeException(ex));
            }
            finally
            {
                _work.Release();
            }
        }

        private void ScanSubscriptions(string email, MegaDriveSubscription[] subscriptions,
            List<MegaDriveQueueItem> queue)
        {
            var sources = new Dictionary<bool, KeyValuePair<int, MegaDriveSnapshot>>();
            Func<MegaDriveSnapshot, ulong, string, Dictionary<ulong, string>, MegaDriveChanges> compare = null;
            foreach (MegaDriveSubscription subscription in subscriptions)
            {
                DateTime started = DateTime.UtcNow;
                KeyValuePair<int, MegaDriveSnapshot> source = ReadSubscriptionSource(email,
                    subscription.Incoming.GetValueOrDefault(), sources);
                if (!subscription.Incoming.HasValue &&
                    (source.Key != 0 || !source.Value.IsFolder(subscription.Folder)))
                {
                    KeyValuePair<int, MegaDriveSnapshot> shares = ReadSubscriptionSource(email, true, sources);
                    if (shares.Key == 0 && shares.Value.IsFolder(subscription.Folder))
                    {
                        source = shares;
                        subscription.Incoming = true;
                    }
                    else if (source.Key == 0 && shares.Key != 0)
                    {
                        source = shares;
                    }
                }

                subscription.LastError = source.Key;
                if (source.Key == 0)
                {
                    MegaDriveNode folder = source.Value.Find(subscription.Folder);
                    if (folder is null || !folder.IsFolder)
                    {
                        subscription.LastError = -9;
                    }
                    else
                    {
                        subscription.Incoming = subscription.Incoming.GetValueOrDefault();
                        subscription.Name = folder.Name;
                        try
                        {
                            compare ??= _accounts.DriveHistory.CreateBoundComparer(email, queue);
                            MegaDriveChanges changes = compare(source.Value, folder.Id, subscription.Destination,
                                subscription.FolderPaths);
                            subscription.ApplyCheck(source.Value, changes);
                        }
                        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
                        {
                            subscription.LastError = MegaAccountService.LocalError;
                            Log.WriteWarning("Could not compare cloud directory subscription: " + Log.SafeException(ex));
                        }
                    }
                }
                subscription.LastCheckedUtc = started;
                int result = _accounts.SaveDriveSubscriptionCheck(email, subscription);
                if (result != 0)
                    subscription.LastError = result;
            }
        }

        private KeyValuePair<int, MegaDriveSnapshot> ReadSubscriptionSource(string email, bool incoming,
            Dictionary<bool, KeyValuePair<int, MegaDriveSnapshot>> sources)
        {
            if (sources.TryGetValue(incoming, out KeyValuePair<int, MegaDriveSnapshot> result))
                return result;
            int code = _accounts.ReadDrive(email, CancellationToken.None, out MegaDriveSnapshot snapshot,
                incomingShares: incoming);
            result = new KeyValuePair<int, MegaDriveSnapshot>(code, snapshot);
            sources.Add(incoming, result);
            return result;
        }

        /// <summary>Reads the current reports for an account, refreshing file statuses
        /// against the queue and delivery history.</summary>
        public Task<List<MegaDriveSubscription>> ReadReportsAsync(string email)
        {
            List<MegaDriveSubscription> subscriptions = _accounts.GetDriveSubscriptions(email);
            List<MegaDriveQueueItem> queue = _engine.SnapshotCloudQueue(email);
            return Task.Run(() =>
            {
                try
                {
                    _accounts.DriveHistory.UpdateSubscriptionFileStates(email, subscriptions, queue);
                }
                catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
                {
                    Log.WriteWarning("Could not refresh cloud directory subscription results: " + Log.SafeException(ex));
                }
                return subscriptions;
            });
        }

        /// <summary>Queues the selected pending files, grouped by bound folder.</summary>
        public async Task<int> QueueFilesAsync(string email, Dictionary<ulong, HashSet<ulong>> selection)
        {
            await _work.WaitAsync().ConfigureAwait(false);
            try
            {
                int added = 0;
                foreach (KeyValuePair<ulong, HashSet<ulong>> item in selection)
                {
                    int count = await EnqueueSubscriptionFilesAsync(email, item.Key, item.Value)
                        .ConfigureAwait(false);
                    if (count < 0)
                        return count;
                    added += count;
                }
                Changed?.Invoke(this, EventArgs.Empty);
                return added;
            }
            finally
            {
                _work.Release();
            }
        }

        private async Task<int> EnqueueSubscriptionFilesAsync(string email, ulong folder,
            HashSet<ulong> selectedHandles)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return 0;
            int access = _accounts.GetAccessError(email);
            if (access != 0)
                return access;
            MegaDriveSubscription subscription = _accounts.GetDriveSubscriptions(email)
                .FirstOrDefault(item => item.Folder == folder);
            if (subscription is null || subscription.Files.Count == 0)
                return 0;

            List<MegaDriveQueueItem> queue = _engine.SnapshotCloudQueue(email);
            bool extractArchives = _engine.Config.ExtraerAutomaticamente;
            Paquete package = null;
            try
            {
                package = await Task.Run(() =>
                {
                    _accounts.DriveHistory.UpdateSubscriptionFileStates(email, new[] { subscription }, queue);
                    MegaDriveDownload plan = subscription.PlanSelected(subscription.Files.Where(file =>
                        file.Status == MegaDriveSubscriptionFileState.PendingReview &&
                        (selectedHandles is null || selectedHandles.Contains(file.Handle))));
                    if (plan.Files.Count == 0)
                        return null;
                    return plan.Build(email, subscription.Destination, subscription.Caption, extractArchives,
                        CancellationToken.None);
                }).ConfigureAwait(false);
                if (package is null)
                    return 0;

                MegaDriveSubscription latest = _accounts.GetDriveSubscriptions(email)
                    .FirstOrDefault(item => item.Folder == folder);
                if (latest is null || !latest.MatchesSettings(subscription))
                    return 0;

                ILookup<ulong, MegaDriveQueueItem> activeQueue = _engine.SnapshotCloudQueue(email)
                    .ToLookup(item => item.Handle);
                for (int index = package.ListaFicheros.Count - 1; index >= 0; index--)
                {
                    Fichero file = package.ListaFicheros[index];
                    if (activeQueue[file.MegaDriveNodeHandle.Value].Any(item => !item.Completed &&
                        string.Equals(item.LocalDirectory, file.RutaLocal, StringComparison.OrdinalIgnoreCase)))
                    {
                        package.ListaFicheros.RemoveAt(index);
                        file.Dispose();
                    }
                }

                int count = package.ListaFicheros.Count;
                if (count == 0)
                    return 0;
                int added = _engine.AddUniquePackage(package);
                return added <= 0 ? Math.Min(added, 0) : added;
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
            {
                Log.WriteWarning("Could not enqueue cloud directory subscription: " + Log.SafeException(ex));
                return MegaAccountService.LocalError;
            }
            finally
            {
                if (package is not null)
                {
                    // AddUniquePackage consumed the package; only dispose a package that
                    // never entered the queue.
                    bool queued = _engine.SnapshotPackages().Any(item => ReferenceEquals(item, package));
                    if (!queued)
                    {
                        foreach (Fichero file in package.SnapshotFicheros())
                            file.Dispose();
                    }
                }
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
