using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace MegaDownloader
{
    /// <summary>
    /// Preferences persisted with a signed-in MEGA account: the last browsed folder,
    /// favorites and the local destinations bound to cloud folders.
    /// </summary>
    public sealed class MegaDrivePreferences
    {
        public ulong? LastFolder { get; set; }
        public ulong? LastSharedFolder { get; set; }
        public List<ulong> Favorites { get; set; } = new List<ulong>();
        public Dictionary<ulong, string> FolderDestinations { get; set; } = new Dictionary<ulong, string>();
        public Dictionary<ulong, Dictionary<ulong, string>> BoundFolderPaths { get; set; } =
            new Dictionary<ulong, Dictionary<ulong, string>>();

        public MegaDrivePreferences Copy() => new MegaDrivePreferences
        {
            LastFolder = LastFolder,
            LastSharedFolder = LastSharedFolder,
            Favorites = new List<ulong>(Favorites),
            FolderDestinations = new Dictionary<ulong, string>(FolderDestinations),
            BoundFolderPaths = BoundFolderPaths.ToDictionary(
                binding => binding.Key,
                binding => new Dictionary<ulong, string>(binding.Value))
        };
    }

    /// <summary>One node returned by the native account bridge.</summary>
    public sealed class MegaDriveNode
    {
        public enum FileKind
        {
            All,
            Video,
            Archive,
            Document,
            Image,
            Audio,
            Other
        }

        public ulong Id { get; }
        public ulong ParentId { get; }
        public string Name { get; }
        public bool IsFolder { get; }
        public long Bytes { get; }
        public long Modified { get; }
        public FileKind Kind { get; }

        public MegaDriveNode(ulong id, ulong parent, string name, bool folder, long bytes, long modified)
        {
            Id = id;
            ParentId = parent;
            Name = name ?? string.Empty;
            IsFolder = folder;
            Bytes = bytes;
            Modified = modified;
            Kind = folder ? FileKind.All : Classify(Name);
        }

        private static FileKind Classify(string name)
        {
            int dot = name.LastIndexOf('.');
            if (dot < 0)
                return FileKind.Other;
            switch (name.Substring(dot + 1).ToLowerInvariant())
            {
                case "mp4":
                case "mkv":
                case "webm":
                case "mov":
                case "avi":
                case "wmv":
                case "m4v":
                case "mpeg":
                case "mpg":
                case "flv":
                case "ts":
                case "rmvb":
                case "3gp":
                    return FileKind.Video;
                case "zip":
                case "rar":
                case "7z":
                case "tar":
                case "gz":
                case "bz2":
                case "xz":
                case "tgz":
                case "zst":
                case "cab":
                case "iso":
                    return FileKind.Archive;
                case "pdf":
                case "doc":
                case "docx":
                case "odt":
                case "txt":
                case "md":
                case "rtf":
                case "xls":
                case "xlsx":
                case "ods":
                case "csv":
                case "ppt":
                case "pptx":
                case "odp":
                case "epub":
                    return FileKind.Document;
                case "jpg":
                case "jpeg":
                case "png":
                case "gif":
                case "webp":
                case "avif":
                case "heic":
                case "bmp":
                case "tif":
                case "tiff":
                case "svg":
                case "ico":
                    return FileKind.Image;
                case "mp3":
                case "flac":
                case "wav":
                case "ogg":
                case "opus":
                case "aac":
                case "m4a":
                case "wma":
                case "aiff":
                    return FileKind.Audio;
                default:
                    return FileKind.Other;
            }
        }
    }

    /// <summary>Immutable tree of one MEGA drive read or one "Shared with me" read.</summary>
    public sealed class MegaDriveSnapshot
    {
        private readonly Dictionary<ulong, MegaDriveNode> _nodes;
        private readonly ILookup<ulong, MegaDriveNode> _children;

        public ulong Root { get; }

        public MegaDriveSnapshot(ulong root, IEnumerable<MegaDriveNode> items)
        {
            Root = root;
            _nodes = items.ToDictionary(node => node.Id);
            _children = _nodes.Values.Where(node => node.Id != root).ToLookup(node => node.ParentId);
        }

        public MegaDriveNode Find(ulong handle)
        {
            _nodes.TryGetValue(handle, out MegaDriveNode node);
            return node;
        }

        public bool IsFolder(ulong handle) => Find(handle)?.IsFolder == true;

        public IEnumerable<MegaDriveNode> InDirectory(ulong folder) => _children[folder];

        public List<MegaDriveNode> Breadcrumbs(ulong folder)
        {
            var trail = new List<MegaDriveNode>();
            MegaDriveNode node = Find(folder);
            while (node is not null)
            {
                trail.Add(node);
                if (node.Id == Root)
                    break;
                node = Find(node.ParentId);
            }
            trail.Reverse();
            return trail;
        }

        public int BrowseCount(ulong folder, bool wholeDrive) =>
            wholeDrive ? _nodes.Count : _children[folder].Count();

        public MegaDriveNode[] Browse(ulong folder, string query, bool wholeDrive, MegaDriveNode.FileKind kind,
            bool sortByTime, bool descending)
        {
            IEnumerable<MegaDriveNode> candidates = wholeDrive ? _nodes.Values : InDirectory(folder);
            IEnumerable<MegaDriveNode> matches = candidates.Where(node => node.Id != Root &&
                (kind == MegaDriveNode.FileKind.All || node.Kind == kind || (node.IsFolder && !wholeDrive)) &&
                CultureInfo.CurrentCulture.CompareInfo.IndexOf(node.Name, query, CompareOptions.IgnoreCase) >= 0);
            return Sort(matches, sortByTime, descending);
        }

        public static MegaDriveNode[] Sort(IEnumerable<MegaDriveNode> items, bool byTime, bool descending)
        {
            IOrderedEnumerable<MegaDriveNode> foldersFirst = items.OrderByDescending(node => node.IsFolder);
            if (byTime)
            {
                foldersFirst = descending
                    ? foldersFirst.ThenByDescending(node => node.Modified)
                    : foldersFirst.ThenBy(node => node.Modified);
                return foldersFirst.ThenBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(node => node.Id).ToArray();
            }

            IOrderedEnumerable<MegaDriveNode> byName = descending
                ? foldersFirst.ThenByDescending(node => node.Name, StringComparer.CurrentCultureIgnoreCase)
                : foldersFirst.ThenBy(node => node.Name, StringComparer.CurrentCultureIgnoreCase);
            return byName.ThenBy(node => node.Id).ToArray();
        }

        public MegaDriveDownload PlanDownload(IEnumerable<ulong> selection, ulong? relativeToFolder = null) =>
            PlanDownloadCore(selection, relativeToFolder, null);

        public MegaDriveDownload PlanBoundDownload(ulong folder, Dictionary<ulong, string> previousPaths) =>
            PlanDownloadCore(new[] { folder }, folder, previousPaths);

        private MegaDriveDownload PlanDownloadCore(IEnumerable<ulong> selection, ulong? relativeToFolder,
            Dictionary<ulong, string> previousPaths)
        {
            var selected = new HashSet<ulong>(selection);
            var download = new MegaDriveDownload();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<KeyValuePair<MegaDriveNode, string>>();
            foreach (MegaDriveNode node in Sort(selected.Select(Find).Where(item => item is not null), false, false).Reverse())
            {
                if (Breadcrumbs(node.ParentId).Any(parent => selected.Contains(parent.Id)))
                    continue;
                pending.Push(new KeyValuePair<MegaDriveNode, string>(node, string.Empty));
            }

            while (pending.Count > 0)
            {
                KeyValuePair<MegaDriveNode, string> item = pending.Pop();
                MegaDriveNode node = item.Key;
                if (!node.IsFolder)
                {
                    download.Files.Add(item);
                    download.TotalBytes += node.Bytes;
                    continue;
                }

                string path = item.Value;
                if (!relativeToFolder.HasValue || node.Id != relativeToFolder.Value)
                {
                    if (!download.FolderPaths.TryGetValue(node.Id, out _))
                    {
                        string name = PathGuard.SanitizeFileName(node.Name, "folder");
                        path = PathGuard.CombineSafeRelativePath(item.Value, name);
                        int number = 1;
                        while (!paths.Add(path))
                        {
                            number++;
                            path = PathGuard.CombineSafeRelativePath(item.Value, SuffixedFolderName(name, number));
                        }
                        if (previousPaths is not null)
                            download.FolderPaths.Add(node.Id, path);
                    }
                    download.Directories.Add(path);
                }

                MegaDriveNode[] descendants = InDirectory(node.Id).OrderByDescending(value => value.Id).ToArray();
                if (previousPaths is not null)
                {
                    // Reserve existing sibling paths before assigning any newly discovered names.
                    foreach (MegaDriveNode child in descendants)
                    {
                        if (child.IsFolder && previousPaths.TryGetValue(child.Id, out string previousPath) &&
                            string.Equals(Path.GetDirectoryName(previousPath), path, StringComparison.OrdinalIgnoreCase) &&
                            MatchesFolderName(child, Path.GetFileName(previousPath)) && paths.Add(previousPath))
                        {
                            download.FolderPaths.Add(child.Id, previousPath);
                        }
                    }
                }
                foreach (MegaDriveNode child in descendants)
                    pending.Push(new KeyValuePair<MegaDriveNode, string>(child, path));
            }

            return download;
        }

        private static string SuffixedFolderName(string name, int number)
        {
            string suffix = " (" + number.ToString(CultureInfo.InvariantCulture) + ")";
            return name.Substring(0, Math.Min(name.Length, 240 - suffix.Length)) + suffix;
        }

        public static bool MatchesFolderName(MegaDriveNode folder, string leaf)
        {
            string name = PathGuard.SanitizeFileName(folder.Name, "folder");
            if (string.Equals(leaf, name, StringComparison.OrdinalIgnoreCase))
                return true;
            int suffix = leaf.LastIndexOf(" (", StringComparison.Ordinal);
            return suffix >= 0 && leaf.EndsWith(")", StringComparison.Ordinal) &&
                int.TryParse(leaf.Substring(suffix + 2, leaf.Length - suffix - 3), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int number) &&
                number >= 2 && string.Equals(leaf, SuffixedFolderName(name, number), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Files and directories selected for a cloud-drive download.</summary>
    public sealed class MegaDriveDownload
    {
        public List<KeyValuePair<MegaDriveNode, string>> Files { get; } =
            new List<KeyValuePair<MegaDriveNode, string>>();

        public List<string> Directories { get; } = new List<string>();

        public Dictionary<ulong, string> FolderPaths { get; } = new Dictionary<ulong, string>();

        public decimal TotalBytes { get; set; }

        public MegaDriveDownload KeepFiles(Func<KeyValuePair<MegaDriveNode, string>, bool> include, bool preserveEmpty)
        {
            var filtered = new MegaDriveDownload();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var original = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<MegaDriveNode, string> file in Files)
            {
                if (preserveEmpty)
                    AddParentDirectories(original, file.Value);
                if (!include(file))
                    continue;
                filtered.Files.Add(file);
                filtered.TotalBytes += file.Key.Bytes;
                AddParentDirectories(used, file.Value);
            }

            if (preserveEmpty)
            {
                foreach (string relative in Directories)
                {
                    if (!original.Contains(relative))
                        AddParentDirectories(used, relative);
                }
            }

            filtered.Directories.AddRange(Directories.Where(used.Contains));
            return filtered;
        }

        private static void AddParentDirectories(HashSet<string> directories, string relative)
        {
            while (!string.IsNullOrEmpty(relative) && directories.Add(relative))
                relative = Path.GetDirectoryName(relative);
        }

        public Paquete Build(string email, string destination, string name, bool extractArchives, CancellationToken cancellation)
        {
            string root = PathGuard.GetSafePathUnderRoot(destination, string.Empty);
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { string.Empty, root } };
            foreach (string relative in Directories)
            {
                cancellation.ThrowIfCancellationRequested();
                paths.Add(relative, PathGuard.GetSafePathUnderRoot(root, relative, allowRoot: false));
            }
            foreach (string path in paths.Values)
            {
                cancellation.ThrowIfCancellationRequested();
                Directory.CreateDirectory(path);
            }

            var package = new Paquete { Nombre = name, RutaLocal = root, ListaFicheros = new List<Fichero>() };
            package.set_SetDescargaExtraccionAutomatica(string.Empty, extractArchives);
            try
            {
                foreach (KeyValuePair<MegaDriveNode, string> item in Files)
                {
                    cancellation.ThrowIfCancellationRequested();
                    MegaDriveNode node = item.Key;
                    var file = new Fichero
                    {
                        MegaAccountEmail = email,
                        MegaDriveNodeHandle = node.Id,
                        NombreFichero = PathGuard.SanitizeFileName(node.Name),
                        TamanoBytes = node.Bytes,
                        RutaLocal = paths[item.Value],
                        RutaRelativa = item.Value,
                        LinkVisible = false
                    };
                    file.set_SetDescargaExtraccionAutomatica(string.Empty, extractArchives);
                    package.ListaFicheros.Add(file);
                }
                cancellation.ThrowIfCancellationRequested();
                return package;
            }
            catch
            {
                foreach (Fichero file in package.ListaFicheros)
                    file.Dispose();
                throw;
            }
        }
    }
}
