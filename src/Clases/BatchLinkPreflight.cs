using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MegaDownloader
{

    /// <summary>Read-only metadata inspection; never creates a download or local file.</summary>
    public sealed class BatchLinkPreflight
    {
        public enum LinkState
        {
            Ready,
            AlreadyQueued,
            Duplicate,
            Invalid,
            Unavailable
        }

        public sealed class Entry
        {
            public URLProcessor.FileURL Source;
            public string OriginalUrl;
            public string Name;
            public long? SizeBytes;
            public LinkState State;
            public string Detail;
            public bool Selected;
            private string categoryName;
            private string categorySource;

            public bool CanAdd
            {
                get
                {
                    return Source is not null && (State == LinkState.Ready || State == LinkState.Unavailable);
                }
            }

            public string Category
            {
                get
                {
                    if (categoryName is not null && string.Equals(categorySource, Name, StringComparison.Ordinal))
                        return categoryName;
                    categorySource = Name;
                    string ext = Path.GetExtension(Name ?? "").ToLowerInvariant();
                    switch (ext ?? "")
                    {
                        case ".mp4":
                        case ".mkv":
                        case ".avi":
                        case ".mov":
                        case ".webm":
                        case ".m4v":
                        case ".wmv":
                        case ".ts":
                            categoryName = "Video";
                            break;
                        case ".mp3":
                        case ".flac":
                        case ".wav":
                        case ".aac":
                        case ".ogg":
                        case ".m4a":
                        case ".opus":
                            categoryName = "Audio";
                            break;
                        case ".zip":
                        case ".rar":
                        case ".7z":
                        case ".gz":
                        case ".bz2":
                        case ".tar":
                        case ".xz":
                            categoryName = "Archives";
                            break;
                        case ".pdf":
                        case ".txt":
                        case ".md":
                        case ".doc":
                        case ".docx":
                        case ".xls":
                        case ".xlsx":
                        case ".ppt":
                        case ".pptx":
                        case ".epub":
                            categoryName = "Documents";
                            break;
                        default:
                            categoryName = "Other";
                            break;
                    }
                    return categoryName;
                }
            }
        }

        public static string Identity(string fileId, string fileKey)
        {
            if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(fileKey))
                return null;
            return fileId + '\0' + fileKey.TrimEnd('=');
        }

        public static string Identity(URLProcessor.FileURL source)
        {
            if (source is null || string.IsNullOrEmpty(source.URL))
                return null;
            if (source.PreflightIdentity is not null && string.Equals(source.PreflightIdentityUrl, source.URL, StringComparison.Ordinal))
                return source.PreflightIdentity;
            string url = source.URL.Replace(Fichero.HIDDEN_LINK, "");
            string value = Identity(Fichero.ExtraerFileID(url), Fichero.ExtraerFileKey(url));
            source.PreflightIdentity = value;
            source.PreflightIdentityUrl = source.URL;
            return value;
        }

        /// <summary>Injectable metadata reader allows offline, deterministic regression tests.</summary>
        public static List<Entry> InspectFiles(IEnumerable<URLProcessor.FileURL> files, ISet<string> queued, Func<URLProcessor.FileURL, CancellationToken, Conexion.InformacionFichero> reader, CancellationToken ct, IProgress<int> progress = null)
        {
            var result = new List<Entry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (URLProcessor.FileURL source in files)
            {
                ct.ThrowIfCancellationRequested();
                var entry = new Entry() { Source = source, State = LinkState.Unavailable, Name = "#" + (result.Count + 1).ToString() };
                result.Add(entry);
                try
                {
                    if (source is null)
                        throw new ArgumentException("Missing link");
                    if (!string.IsNullOrEmpty(source.DisplayName))
                        entry.Name = source.DisplayName;
                    entry.SizeBytes = source.SizeBytes;
                    string identityValue = Identity(source);
                    if (identityValue is not null && queued is not null && queued.Contains(identityValue))
                    {
                        entry.State = LinkState.AlreadyQueued;
                    }
                    else if (identityValue is not null && !seen.Add(identityValue))
                    {
                        entry.State = LinkState.Duplicate;
                    }
                    else
                    {
                        Conexion.InformacionFichero info = null;
                        // Folder listings already contain decrypted names and sizes. Do not
                        // issue another API request for every node of a large folder.
                        if (!string.IsNullOrEmpty(source.DisplayName) && source.SizeBytes.HasValue)
                        {
                            entry.State = LinkState.Ready;
                        }
                        else
                        {
                            info = reader(source, ct);
                            ct.ThrowIfCancellationRequested();
                            ApplyInformation(entry, info);
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    entry.State = LinkState.Unavailable;
                    entry.Detail = DownloadAdvice.FirstLine(Log.Redact(ex.Message));
                }
                entry.Selected = entry.State == LinkState.Ready;
                if (progress is not null)
                    progress.Report(result.Count);
            }
            var originals = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (Entry entry in result)
            {
                string key = Identity(entry.Source);
                if (key is null)
                    continue;
                if (entry.State == LinkState.Duplicate && originals.ContainsKey(key))
                {
                    entry.Name = originals[key].Name;
                    entry.SizeBytes = originals[key].SizeBytes;
                }
                else if (!originals.ContainsKey(key))
                {
                    originals.Add(key, entry);
                }
            }
            return result;
        }

        private static void ApplyInformation(Entry entry, Conexion.InformacionFichero info)
        {
            if (info is not null && info.Err == Conexion.TipoError.SinErrores && !string.IsNullOrEmpty(info.Nombre))
            {
                entry.Name = PathGuard.SanitizeFileName(info.Nombre, "file");
                entry.SizeBytes = Math.Max(0L, info.Tamano);
                entry.Source.DisplayName = entry.Name;
                entry.Source.SizeBytes = entry.SizeBytes;
                entry.State = LinkState.Ready;
            }
            else
            {
                entry.Detail = info is null ? "" : DownloadAdvice.FirstLine(Log.Redact(info.Errtxt));
                if (info is not null && DownloadAdvice.Classify(info.Errtxt, info.Err == Conexion.TipoError.QuotaExceeded, false) == DownloadAdvice.FailureKind.Permanent)
                {
                    entry.State = LinkState.Invalid;
                }
                else
                {
                    entry.State = LinkState.Unavailable;
                }
            }
        }

        public static List<Entry> Resolve(IEnumerable<string> urls, Configuracion config, ISet<string> queued, CancellationToken ct, IProgress<int> progress = null, IProgress<PreflightProgress> detailedProgress = null)
        {
            var files = new List<URLProcessor.FileURL>();
            var failures = new List<Entry>();
            foreach (string url in urls)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (detailedProgress is not null)
                        detailedProgress.Report(new PreflightProgress(PreflightProgress.Phase.Reading));
                    var folderProgress = progress;
                    if (detailedProgress is not null)
                    {
                        folderProgress = new PreflightProgress.ItemReporter(count =>
                            {
                                if (progress is not null)
                                    progress.Report(count);
                                detailedProgress.Report(new PreflightProgress(PreflightProgress.Phase.Reading, processed: count));
                            });
                    }
                    files.AddRange(URLProcessor.ProcessURLs(new List<string>() { url }, ref config, folderProgress, ct));
                }
                catch (OperationCanceledException ex)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures.Add(new Entry()
                    {
                        OriginalUrl = url,
                        Name = "#" + (files.Count + failures.Count + 1).ToString(),
                        State = DownloadAdvice.Classify(ex.Message, false, false) == DownloadAdvice.FailureKind.Permanent ? LinkState.Invalid : LinkState.Unavailable,
                        Detail = DownloadAdvice.FirstLine(Log.Redact(ex.Message))
                    });
                }
            }
            if (detailedProgress is not null)
                detailedProgress.Report(new PreflightProgress(PreflightProgress.Phase.Checking, itemCount: files.Count));
            var fileProgress = progress;
            if (detailedProgress is not null)
            {
                fileProgress = new PreflightProgress.ItemReporter(count =>
                    {
                        if (progress is not null)
                            progress.Report(count);
                        if (count % 100 == 0 || count == files.Count)
                            detailedProgress.Report(new PreflightProgress(PreflightProgress.Phase.Checking, processed: count, itemCount: files.Count));
                    });
            }
            var results = InspectFiles(files, queued, (source, token) =>
    {
        if (MegaQuotaManager.IsQuarantined())
            return new Conexion.InformacionFichero() { Err = Conexion.TipoError.QuotaExceeded, Errtxt = Language.GetText("Advice_Quota") };
        string url = source.URL.Replace(Fichero.HIDDEN_LINK, "");
        return Conexion.ObtenerInformacionFichero(config, Fichero.ExtraerFileID(url), Fichero.ExtraerFileKey(url), false, token);
    }, ct, fileProgress);
            results.AddRange(failures);
            return results;
        }

        public static long? AvailableSpace(string destination)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(destination));
                // Do not block metadata inspection on an unavailable network share.
                if (root.StartsWith(@"\\", StringComparison.Ordinal))
                    return default;
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                    return drive.AvailableFreeSpace;
            }
            catch (IOException ex)
            {
            }
            catch (ArgumentException ex)
            {
            }
            catch (UnauthorizedAccessException ex)
            {
            }
            catch (System.Security.SecurityException ex)
            {
            }
            return default;
        }
    }
}