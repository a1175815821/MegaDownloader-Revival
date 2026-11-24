using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MegaDownloader
{
    public enum MegaDriveSubscriptionFileState
    {
        PendingReview,
        Queued,
        Downloaded
    }

    /// <summary>A cloud file discovered by a bound-folder check.</summary>
    public sealed class MegaDriveSubscriptionFile
    {
        public ulong Handle { get; set; }
        public string Name { get; set; }
        public string RelativePath { get; set; }
        public long Bytes { get; set; }
        public long Modified { get; set; }
        public bool Updated { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public MegaDriveSubscriptionFileState Status { get; set; }

        public bool SameVersion(MegaDriveNode node) =>
            node is not null && !node.IsFolder && node.Id == Handle && node.Bytes == Bytes &&
            node.Modified == Modified && node.Name == Name;

        public MegaDriveSubscriptionFile Copy() => (MegaDriveSubscriptionFile)MemberwiseClone();
    }

    /// <summary>
    /// The editable part of a folder subscription. Account-bound folders and anonymous
    /// public-folder links store the same settings, so one dialog edits both.
    /// </summary>
    public sealed class MegaFolderSubscriptionSettings
    {
        public string Destination { get; set; }
        public string Extensions { get; set; } = string.Empty;
        public int? CheckMinutes { get; set; }
        public bool AutoQueue { get; set; }
        public bool Paused { get; set; }

        public MegaFolderSubscriptionSettings Copy() => (MegaFolderSubscriptionSettings)MemberwiseClone();
    }

    /// <summary>Automatic check settings and latest result for one bound cloud folder.</summary>
    public sealed class MegaDriveSubscription
    {
        public ulong Folder { get; set; }
        public string Name { get; set; }
        public bool? Incoming { get; set; }
        public string Destination { get; set; }
        public int? CheckMinutes { get; set; }
        public string Extensions { get; set; } = string.Empty;
        public bool AutoQueue { get; set; }

        /// <summary>A paused subscription keeps its files and history but skips automatic checks.</summary>
        public bool Paused { get; set; }

        public DateTime ScheduleFromUtc { get; set; }
        public DateTime? LastCheckedUtc { get; set; }
        public int LastError { get; set; }
        public List<MegaDriveSubscriptionFile> Files { get; set; } = new List<MegaDriveSubscriptionFile>();
        public Dictionary<ulong, string> FolderPaths { get; set; } = new Dictionary<ulong, string>();

        public MegaDriveSubscription Copy()
        {
            var result = (MegaDriveSubscription)MemberwiseClone();
            result.Files = Files.Select(file => file.Copy()).ToList();
            result.FolderPaths = new Dictionary<ulong, string>(FolderPaths);
            return result;
        }

        public static string NormalizeExtensions(string value)
        {
            IEnumerable<string> extensions = (value ?? string.Empty)
                .Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(extension => extension.TrimStart('.').ToLowerInvariant())
                .Where(extension => extension.Length > 0)
                .Distinct(StringComparer.Ordinal);
            return string.Join(", ", extensions);
        }

        public DateTime? NextCheckLocal() =>
            ComputeNextCheckLocal(Paused, CheckMinutes, ScheduleFromUtc, LastCheckedUtc);

        /// <summary>
        /// The next daily check for a subscription, or null when it is paused or has no
        /// schedule. Shared with <see cref="MegaPublicFolderSubscription"/>.
        /// </summary>
        public static DateTime? ComputeNextCheckLocal(bool paused, int? checkMinutes, DateTime scheduleFromUtc,
            DateTime? lastCheckedUtc)
        {
            if (paused || !checkMinutes.HasValue)
                return null;
            DateTime reference = scheduleFromUtc;
            if (reference == default)
                reference = DateTime.UtcNow;
            if (lastCheckedUtc.HasValue && lastCheckedUtc.Value > reference)
                reference = lastCheckedUtc.Value;
            DateTime local = reference.ToLocalTime();
            DateTime now = DateTime.Now;
            DateTime nextCheck = local.Date.AddMinutes(checkMinutes.Value);
            while (nextCheck <= now)
                nextCheck = nextCheck.AddDays(1);
            return nextCheck;
        }

        public void ApplyCheck(MegaDriveSnapshot snapshot, MegaDriveChanges changes)
        {
            var extensions = new HashSet<string>(
                Extensions.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            List<MegaDriveSubscriptionFile> discovered = changes.Plan.Files
                .Where(file => extensions.Count == 0 ||
                    extensions.Contains(Path.GetExtension(file.Key.Name).TrimStart('.')))
                .Select(file => new MegaDriveSubscriptionFile
                {
                    Handle = file.Key.Id,
                    Name = file.Key.Name,
                    Bytes = file.Key.Bytes,
                    Modified = file.Key.Modified,
                    RelativePath = file.Value,
                    Updated = changes.Updated.Contains(file.Key.Id)
                })
                .ToList();
            Dictionary<ulong, MegaDriveSubscriptionFile> previous = Files.ToDictionary(file => file.Handle);
            bool hasNew = discovered.Any(file =>
            {
                if (!previous.TryGetValue(file.Handle, out MegaDriveSubscriptionFile prior))
                    return true;
                return prior.Modified != file.Modified || prior.Bytes != file.Bytes || prior.Name != file.Name;
            });
            if (!hasNew)
            {
                var active = new HashSet<ulong>(discovered.Select(file => file.Handle));
                discovered.AddRange(Files.Where(file =>
                {
                    if (active.Contains(file.Handle))
                        return false;
                    MegaDriveNode node = snapshot.Find(file.Handle);
                    return file.SameVersion(node) &&
                        snapshot.Breadcrumbs(node.ParentId).Any(parent => parent.Id == Folder) &&
                        (extensions.Count == 0 || extensions.Contains(Path.GetExtension(file.Name).TrimStart('.')));
                }));
            }
            Files = discovered;
        }

        public MegaDriveDownload PlanSelected(IEnumerable<MegaDriveSubscriptionFile> selection)
        {
            var plan = new MegaDriveDownload();
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MegaDriveSubscriptionFile file in selection)
            {
                var node = new MegaDriveNode(file.Handle, Folder, file.Name, false, file.Bytes, file.Modified);
                plan.Files.Add(new KeyValuePair<MegaDriveNode, string>(node, file.RelativePath));
                plan.TotalBytes += file.Bytes;
                string directory = file.RelativePath;
                while (!string.IsNullOrEmpty(directory) && directories.Add(directory))
                {
                    plan.Directories.Add(directory);
                    directory = Path.GetDirectoryName(directory);
                }
            }
            return plan;
        }

        public bool MatchesSettings(MegaDriveSubscription other) =>
            string.Equals(Destination, other.Destination, StringComparison.OrdinalIgnoreCase) &&
            CheckMinutes.Equals(other.CheckMinutes) && AutoQueue == other.AutoQueue &&
            Extensions == other.Extensions && Paused == other.Paused &&
            ScheduleFromUtc == other.ScheduleFromUtc;

        public MegaFolderSubscriptionSettings ReadSettings() => new MegaFolderSubscriptionSettings
        {
            Destination = Destination,
            Extensions = Extensions,
            CheckMinutes = CheckMinutes,
            AutoQueue = AutoQueue,
            Paused = Paused
        };

        public string Caption => string.IsNullOrEmpty(Name) ? Destination : Name;
    }
}
