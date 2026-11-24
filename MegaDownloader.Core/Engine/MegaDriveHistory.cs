using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MegaDownloader
{
    /// <summary>One cloud-drive task currently in the desktop queue.</summary>
    public sealed class MegaDriveQueueItem
    {
        public ulong Handle { get; }
        public string LocalDirectory { get; }
        public string FileName { get; }
        public long Bytes { get; }
        public bool Completed { get; }

        public MegaDriveQueueItem(ulong handle, string localDirectory, string fileName, long bytes, bool completed)
        {
            Handle = handle;
            LocalDirectory = localDirectory;
            FileName = fileName;
            Bytes = bytes;
            Completed = completed;
        }
    }

    /// <summary>Result of comparing a cloud folder with the files already delivered locally.</summary>
    public sealed class MegaDriveChanges
    {
        public MegaDriveDownload Plan { get; }
        public HashSet<ulong> Pending { get; }
        public HashSet<ulong> Updated { get; }

        public MegaDriveChanges(MegaDriveDownload plan, HashSet<ulong> pending, HashSet<ulong> updated)
        {
            Plan = plan;
            Pending = pending;
            Updated = updated;
        }
    }

    /// <summary>
    /// Append-only record of cloud files this app has delivered. It lets a bound folder
    /// tell "already downloaded" apart from "new" or "changed" without trusting names alone.
    /// </summary>
    public sealed class MegaDriveHistory
    {
        private sealed class Delivery
        {
            public ulong Handle { get; set; }
            public string CloudName { get; set; }
            public string LocalPath { get; set; }
            public long Bytes { get; set; }
            public long WriteUtcTicks { get; set; }
        }

        private static readonly object Gate = new object();
        private readonly string _folder;

        public MegaDriveHistory(string folder)
        {
            _folder = folder;
        }

        private string AccountFile(string email)
        {
            string key = Utils.SecretFingerprint(email.ToUpperInvariant()).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            return Path.Combine(_folder, key + ".jsonl");
        }

        public void Record(string email, ulong handle, string cloudName, string output)
        {
            try
            {
                var info = new FileInfo(output);
                var entry = new Delivery
                {
                    Handle = handle,
                    CloudName = cloudName,
                    LocalPath = info.FullName,
                    Bytes = info.Length,
                    WriteUtcTicks = info.LastWriteTimeUtc.Ticks
                };
                string line = JsonConvert.SerializeObject(entry);
                lock (Gate)
                {
                    Directory.CreateDirectory(_folder);
                    // Start a fresh line even if the prior append was interrupted.
                    File.AppendAllText(AccountFile(email), "\n" + line, new UTF8Encoding(false));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.WriteWarning("Could not record MEGA cloud drive delivery: " + Log.SafeException(ex));
            }
        }

        public void RemoveAccount(string email)
        {
            lock (Gate)
            {
                try
                {
                    File.Delete(AccountFile(email));
                }
                catch (DirectoryNotFoundException)
                {
                    // No history has been recorded yet.
                }
            }
        }

        /// <summary>
        /// Local files delivered by an anonymous public-folder subscription. Public
        /// files carry no account, so they are tracked per folder link instead.
        /// </summary>
        public List<string> ReadPublicFolderPaths(string folderId)
        {
            string history = PublicFolderFile(folderId);
            lock (Gate)
            {
                var paths = new List<string>();
                if (!File.Exists(history))
                    return paths;
                foreach (string line in File.ReadLines(history, Encoding.UTF8))
                {
                    if (line.Length == 0)
                        continue;
                    try
                    {
                        Delivery entry = JsonConvert.DeserializeObject<Delivery>(line);
                        if (entry?.LocalPath is not null)
                            paths.Add(entry.LocalPath);
                    }
                    catch (JsonException)
                    {
                        // An interrupted final write does not invalidate previous deliveries.
                    }
                }
                return paths;
            }
        }

        public void RecordPublicFolderDelivery(string folderId, ulong handle, string cloudName, string output)
        {
            try
            {
                var info = new FileInfo(output);
                var entry = new Delivery
                {
                    Handle = handle,
                    CloudName = cloudName,
                    LocalPath = info.FullName,
                    Bytes = info.Length,
                    WriteUtcTicks = info.LastWriteTimeUtc.Ticks
                };
                string line = JsonConvert.SerializeObject(entry);
                lock (Gate)
                {
                    Directory.CreateDirectory(_folder);
                    File.AppendAllText(PublicFolderFile(folderId), "\n" + line, new UTF8Encoding(false));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.WriteWarning("Could not record the public folder delivery: " + Log.SafeException(ex));
            }
        }

        private string PublicFolderFile(string folderId)
        {
            string key = Utils.SecretFingerprint("public:" + folderId)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            return Path.Combine(_folder, key + ".jsonl");
        }

        private ILookup<ulong, Delivery> ReadDeliveries(string email)
        {
            var delivered = new Dictionary<string, Delivery>(StringComparer.OrdinalIgnoreCase);
            string history = AccountFile(email);
            lock (Gate)
            {
                if (File.Exists(history))
                {
                    foreach (string line in File.ReadLines(history, Encoding.UTF8))
                    {
                        if (line.Length == 0)
                            continue;
                        try
                        {
                            Delivery entry = JsonConvert.DeserializeObject<Delivery>(line);
                            delivered[entry.LocalPath] = entry;
                        }
                        catch (JsonException)
                        {
                            // An interrupted final write does not invalidate previous deliveries.
                        }
                    }
                }
            }
            return delivered.Values.ToLookup(item => item.Handle);
        }

        private static bool LocalFileMatches(string path, long bytes, long? writeUtcTicks = null)
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length == bytes &&
                (!writeUtcTicks.HasValue || info.LastWriteTimeUtc.Ticks == writeUtcTicks.Value);
        }

        private static bool MatchesLegacyDeliveryName(string cloudName, string localName)
        {
            string leaf = PathGuard.SanitizeFileName(cloudName, "file");
            if (string.Equals(leaf, localName, StringComparison.Ordinal))
                return true;
            Match suffix = Regex.Match(localName, " \\(([0-9]+)\\)(?:\\.[^.]*)?\\z");
            return suffix.Success && int.TryParse(suffix.Groups[1].Value, out int number) && number >= 2 &&
                string.Equals(Fichero.MegaAccountCollisionLeaf(leaf, number), localName, StringComparison.Ordinal);
        }

        private static bool IsInDirectory(string candidate, string canonicalDirectory) =>
            !string.IsNullOrEmpty(candidate) &&
            string.Equals(PathGuard.GetSafePathUnderRoot(candidate, string.Empty), canonicalDirectory,
                StringComparison.OrdinalIgnoreCase);

        public MegaDriveChanges FindBoundChanges(string email, MegaDriveSnapshot snapshot, ulong folder, string destination,
            IEnumerable<MegaDriveQueueItem> queue, Dictionary<ulong, string> paths) =>
            CreateBoundComparer(email, queue)(snapshot, folder, destination, paths);

        public Func<MegaDriveSnapshot, ulong, string, Dictionary<ulong, string>, MegaDriveChanges> CreateBoundComparer(
            string email, IEnumerable<MegaDriveQueueItem> queue)
        {
            ILookup<ulong, Delivery> delivered = ReadDeliveries(email);
            return (snapshot, folder, destination, paths) =>
                CompareBinding(snapshot, folder, destination, queue, paths, delivered);
        }

        private static MegaDriveChanges CompareBinding(MegaDriveSnapshot snapshot, ulong folder, string destination,
            IEnumerable<MegaDriveQueueItem> queue, Dictionary<ulong, string> paths,
            ILookup<ulong, Delivery> delivered)
        {
            string root = PathGuard.GetSafePathUnderRoot(destination, string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (MegaDriveQueueItem item in queue)
                RecoverFolderPaths(snapshot, folder, root, item.Handle, item.LocalDirectory, paths);
            foreach (IGrouping<ulong, Delivery> group in delivered)
            {
                foreach (Delivery item in group)
                    RecoverFolderPaths(snapshot, folder, root, item.Handle, Path.GetDirectoryName(item.LocalPath), paths);
            }

            MegaDriveDownload fullPlan = snapshot.PlanBoundDownload(folder, paths);
            paths.Clear();
            foreach (KeyValuePair<ulong, string> binding in fullPlan.FolderPaths)
                paths.Add(binding.Key, binding.Value);
            return ComparePlan(fullPlan, destination, queue, delivered);
        }

        private static void RecoverFolderPaths(MegaDriveSnapshot snapshot, ulong boundFolder, string root,
            ulong fileHandle, string directory, Dictionary<ulong, string> paths)
        {
            MegaDriveNode file = snapshot.Find(fileHandle);
            if (file is null || paths.ContainsKey(file.ParentId) || string.IsNullOrEmpty(directory) ||
                !directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string relative = directory.Substring(root.Length);
            MegaDriveNode folder = snapshot.Find(file.ParentId);
            var recovered = new List<KeyValuePair<ulong, string>>();
            while (folder is not null && folder.Id != boundFolder)
            {
                if (string.IsNullOrEmpty(relative) ||
                    !MegaDriveSnapshot.MatchesFolderName(folder, Path.GetFileName(relative)))
                {
                    return;
                }
                if (paths.TryGetValue(folder.Id, out string known))
                {
                    if (!string.Equals(known, relative, StringComparison.OrdinalIgnoreCase))
                        return;
                    break;
                }
                recovered.Add(new KeyValuePair<ulong, string>(folder.Id, relative));
                relative = Path.GetDirectoryName(relative);
                folder = snapshot.Find(folder.ParentId);
            }
            if (folder is null || (folder.Id == boundFolder && relative != string.Empty))
                return;
            foreach (KeyValuePair<ulong, string> binding in recovered)
                paths.Add(binding.Key, binding.Value);
        }

        public MegaDriveChanges FindChanges(string email, MegaDriveDownload fullPlan, string destination,
            IEnumerable<MegaDriveQueueItem> queue) =>
            ComparePlan(fullPlan, destination, queue, ReadDeliveries(email));

        public void UpdateSubscriptionFileStates(string email, IEnumerable<MegaDriveSubscription> subscriptions,
            IEnumerable<MegaDriveQueueItem> queue)
        {
            ILookup<ulong, Delivery> delivered = ReadDeliveries(email);
            foreach (MegaDriveSubscription subscription in subscriptions)
            {
                if (subscription.Files.Count == 0)
                    continue;
                MegaDriveChanges changes = ComparePlan(subscription.PlanSelected(subscription.Files),
                    subscription.Destination, queue, delivered);
                var undelivered = new HashSet<ulong>(changes.Plan.Files.Select(file => file.Key.Id));
                foreach (MegaDriveSubscriptionFile file in subscription.Files)
                {
                    if (!undelivered.Contains(file.Handle))
                        file.Status = MegaDriveSubscriptionFileState.Downloaded;
                    else if (changes.Pending.Contains(file.Handle))
                        file.Status = MegaDriveSubscriptionFileState.Queued;
                    else
                        file.Status = MegaDriveSubscriptionFileState.PendingReview;
                }
            }
        }

        private static MegaDriveChanges ComparePlan(MegaDriveDownload fullPlan, string destination,
            IEnumerable<MegaDriveQueueItem> queue, ILookup<ulong, Delivery> delivered)
        {
            ILookup<ulong, MegaDriveQueueItem> queued = queue.ToLookup(item => item.Handle);
            var pending = new HashSet<ulong>();
            var updated = new HashSet<ulong>();
            var destinations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Func<KeyValuePair<MegaDriveNode, string>, bool> compare = file =>
            {
                MegaDriveNode node = file.Key;
                if (!destinations.TryGetValue(file.Value, out string parent))
                {
                    parent = PathGuard.GetSafePathUnderRoot(destination, file.Value);
                    destinations.Add(file.Value, parent);
                }
                Delivery[] previous = delivered[node.Id]
                    .Where(item => IsInDirectory(Path.GetDirectoryName(item.LocalPath), parent)).ToArray();
                if (previous.Any(item => item.Bytes == node.Bytes &&
                    (item.CloudName is null
                        ? MatchesLegacyDeliveryName(node.Name, Path.GetFileName(item.LocalPath))
                        : string.Equals(item.CloudName, node.Name, StringComparison.Ordinal)) &&
                    LocalFileMatches(item.LocalPath, item.Bytes, item.WriteUtcTicks)))
                {
                    return false;
                }

                MegaDriveQueueItem[] tasks = queued[node.Id]
                    .Where(item => IsInDirectory(item.LocalDirectory, parent)).ToArray();
                // Queue entries from before delivery history was introduced can still account for existing files.
                if (previous.Length == 0 && tasks.Any(item => item.Completed && item.Bytes == node.Bytes &&
                    MatchesLegacyDeliveryName(node.Name, item.FileName) &&
                    LocalFileMatches(Path.Combine(parent, item.FileName), node.Bytes)))
                {
                    return false;
                }
                if (tasks.Any(item => !item.Completed))
                    pending.Add(node.Id);
                if (previous.Length > 0 || File.Exists(PathGuard.GetSafeFilePathUnderRoot(parent, node.Name)))
                    updated.Add(node.Id);
                return true;
            };
            return new MegaDriveChanges(fullPlan.KeepFiles(compare, preserveEmpty: false), pending, updated);
        }
    }
}
