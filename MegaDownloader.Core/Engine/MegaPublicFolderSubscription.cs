using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MegaDownloader
{
    public enum MegaPublicFolderFileState
    {
        PendingReview,
        Queued,
        Downloaded
    }

    /// <summary>One file found by an anonymous public-folder check.</summary>
    public sealed class MegaPublicFolderFile
    {
        /// <summary>The task identity: <c>N?&lt;handle&gt;</c> for files read from a folder.</summary>
        public string FileId { get; set; }

        /// <summary>
        /// The resolved link with its own file key, as the folder listing decrypted it.
        /// Queueing uses this; re-listing the share is not needed to download one file.
        /// </summary>
        public string Link { get; set; }

        public string Name { get; set; }
        public string RelativePath { get; set; }
        public long Bytes { get; set; }
        public bool Updated { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public MegaPublicFolderFileState Status { get; set; }

        public MegaPublicFolderFile Copy() => (MegaPublicFolderFile)MemberwiseClone();
    }

    /// <summary>
    /// A subscription to a public folder link. Nothing is signed in: the folder key in
    /// the link is enough to list the share, and every discovered file keeps its own
    /// file key so it can be queued without an account.
    /// </summary>
    public sealed class MegaPublicFolderSubscription
    {
        public string FolderId { get; set; } = string.Empty;
        public string FolderKey { get; set; } = string.Empty;

        /// <summary>The link the user pasted, kept for the report and for re-checking.</summary>
        public string Link { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string Destination { get; set; }
        public int? CheckMinutes { get; set; }
        public string Extensions { get; set; } = string.Empty;
        public bool AutoQueue { get; set; }
        public bool Paused { get; set; }
        public DateTime ScheduleFromUtc { get; set; }
        public DateTime? LastCheckedUtc { get; set; }
        public int LastError { get; set; }
        public List<MegaPublicFolderFile> Files { get; set; } = new List<MegaPublicFolderFile>();

        public string Identity => FolderId ?? string.Empty;

        public MegaPublicFolderSubscription Copy()
        {
            var result = (MegaPublicFolderSubscription)MemberwiseClone();
            result.Files = Files.Select(file => file.Copy()).ToList();
            return result;
        }

        public DateTime? NextCheckLocal() =>
            MegaDriveSubscription.ComputeNextCheckLocal(Paused, CheckMinutes, ScheduleFromUtc, LastCheckedUtc);

        public MegaFolderSubscriptionSettings ReadSettings() => new MegaFolderSubscriptionSettings
        {
            Destination = Destination,
            Extensions = Extensions,
            CheckMinutes = CheckMinutes,
            AutoQueue = AutoQueue,
            Paused = Paused
        };

        public void ApplySettings(MegaFolderSubscriptionSettings settings, bool destinationChanged)
        {
            Destination = settings.Destination;
            Extensions = MegaDriveSubscription.NormalizeExtensions(settings.Extensions);
            CheckMinutes = settings.CheckMinutes;
            AutoQueue = settings.AutoQueue;
            Paused = settings.Paused;
            if (destinationChanged)
                Files.Clear();
        }

        public string Caption => string.IsNullOrEmpty(Name) ? Destination : Name;

        private static readonly Regex FolderLink = new Regex(
            @"(?:https?://)?(?:www\.)?mega\.(?:co\.)?nz/(?:folder/|#F!)(?<Id>[A-Za-z0-9_\-]+)(?:[#!](?<Key>[A-Za-z0-9_\-]+))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Reads a public folder link. Returns false when the text holds no folder link
        /// or the key is missing, since a share cannot be listed without it.
        /// </summary>
        public static bool TryParseLink(string text, out string folderId, out string folderKey, out string link)
        {
            folderId = string.Empty;
            folderKey = string.Empty;
            link = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            Match match = FolderLink.Match(text.Trim());
            if (!match.Success)
                return false;
            folderId = match.Groups["Id"].Value;
            folderKey = match.Groups["Key"].Value;
            if (folderId.Length == 0 || folderKey.Length == 0)
                return false;
            link = "https://mega.nz/folder/" + folderId + "#" + folderKey;
            return true;
        }

        /// <summary>The local path this file occupies under a package root.</summary>
        public static string LocalPath(string packageRoot, MegaPublicFolderFile file) =>
            PathGuard.GetSafeFilePathUnderRoot(
                PathGuard.GetSafePathUnderRoot(packageRoot, file.RelativePath ?? string.Empty),
                PathGuard.SanitizeFileName(file.Name, "file"));

        /// <summary>Reads the node handle out of an <c>N?&lt;handle&gt;</c> task id.</summary>
        public static bool TryReadHandle(string fileId, out ulong handle)
        {
            handle = 0;
            if (string.IsNullOrEmpty(fileId) || !fileId.StartsWith("N?", StringComparison.Ordinal))
                return false;
            string value = fileId.Substring(2);
            return ulong.TryParse(value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out handle) ||
                TryDecodeBase64Handle(value, out handle);
        }

        private static bool TryDecodeBase64Handle(string value, out ulong handle)
        {
            handle = 0;
            try
            {
                byte[] bytes = Criptografia.base64urldecodeBytes(value);
                if (bytes.Length < 8)
                    return false;
                ulong result = 0;
                for (int index = 0; index < 8; index++)
                    result = (result << 8) | bytes[index];
                handle = result;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
