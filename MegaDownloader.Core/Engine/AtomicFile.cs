using System;
using System.IO;
using System.Text;
using System.Xml;

namespace MegaDownloader
{

    internal sealed class AtomicFile
    {
        private AtomicFile()
        {
        }

        public static void SaveXml(XmlDocument document, string destinationPath, bool preserveBackup = false)
        {
            if (document is null)
                throw new ArgumentNullException("document");
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("A destination path is required.", "destinationPath");

            string fullDestination = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullDestination);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The destination directory could not be determined.");
            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, "." + Path.GetFileName(fullDestination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var settings = new XmlWriterSettings()
                    {
                        Encoding = new UTF8Encoding(false),
                        Indent = false,
                        CloseOutput = false
                    };
                    using (var writer = XmlWriter.Create(stream, settings))
                    {
                        document.Save(writer);
                        writer.Flush();
                    }
                    stream.Flush(true);
                }

                if (File.Exists(fullDestination))
                {
                    File.Replace(tempPath, fullDestination, preserveBackup ? null : fullDestination + ".bak", true);
                }
                else
                {
                    File.Move(tempPath, fullDestination);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
    /// Loads an XML document, using ".bak" for a missing or corrupt primary.
    /// A locked primary is left untouched instead of loading an older backup.
    /// transientFailure reports that reading failed with IOException
    /// (sharing violation: antivirus scan / backup / sync client) rather than a
    /// corrupt document. Callers must treat that as "keep the on-disk file as-is":
    /// loading defaults and then saving would replace a perfectly good file with
    /// defaults and rotate it into ".bak", destroying user data.
    /// </summary>
        public static bool TryLoadXml(string sourcePath, ref XmlDocument document, ref bool recoveredFromBackup, ref bool transientFailure)
        {
            // Original short retry window, kept for callers and regression tests.
            // The name is deliberately unique: tests resolve "TryLoadXml" by name via reflection.
            return TryLoadXmlWithRetries(sourcePath, ref document, ref recoveredFromBackup, ref transientFailure, 3, 100);
        }

        /// <summary>Startup retry window (20 x 150ms = 3s). Antivirus / sync clients routinely
    /// hold a config file for less than a second; waiting here avoids entering the degraded
    /// "defaults + write-protected" state at all, which is the real B1/B2 root cause.</summary>
        public const int StartupAttempts = 20;
        public const int StartupRetryDelayMs = 150;

        internal static bool TryLoadXmlWithRetries(string sourcePath, ref XmlDocument document, ref bool recoveredFromBackup, ref bool transientFailure, int attempts, int retryDelayMs)
        {
            string blockedSource = null;
            return TryLoadXmlWithRecoveryInfo(sourcePath, ref document, ref recoveredFromBackup, ref transientFailure, ref blockedSource, attempts, retryDelayMs);
        }

        internal static bool TryLoadXmlWithRecoveryInfo(string sourcePath, ref XmlDocument document, ref bool recoveredFromBackup, ref bool transientFailure, ref string blockedSource, int attempts, int retryDelayMs)
        {
            return TryLoadXmlCancelable(sourcePath, ref document, ref recoveredFromBackup, ref transientFailure, ref blockedSource, attempts, retryDelayMs, System.Threading.CancellationToken.None);
        }

        internal static bool TryLoadXmlCancelable(string sourcePath, ref XmlDocument document, ref bool recoveredFromBackup, ref bool transientFailure, ref string blockedSource, int attempts, int retryDelayMs, System.Threading.CancellationToken cancellationToken)
        {
            recoveredFromBackup = false;
            transientFailure = false;
            blockedSource = null;
            string fullSource = Path.GetFullPath(sourcePath);
            var state = ReadXml(fullSource, ref document, attempts, retryDelayMs, cancellationToken);
            if (state == XmlReadState.Loaded)
                return true;
            if (state == XmlReadState.Blocked)
            {
                blockedSource = fullSource;
            }
            else
            {
                string backupPath = fullSource + ".bak";
                state = ReadXml(backupPath, ref document, attempts, retryDelayMs, cancellationToken);
                if (state == XmlReadState.Loaded)
                {
                    recoveredFromBackup = true;
                    return true;
                }
                if (state == XmlReadState.Blocked)
                    blockedSource = backupPath;
            }
            transientFailure = state == XmlReadState.Blocked;
            return false;
        }

        internal enum XmlReadState
        {
            Missing,
            Loaded,
            Invalid,
            Blocked
        }

        internal static XmlReadState ReadXml(string source, ref XmlDocument document, int attempts, int delayMs, System.Threading.CancellationToken cancellationToken)
        {
            document = null;
            for (int attempt = 1, loopTo = Math.Max(1, attempts); attempt <= loopTo; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var xml = new XmlDocument() { XmlResolver = null };
                        xml.Load(stream);
                        document = xml;
                        return XmlReadState.Loaded;
                    }
                }
                catch (FileNotFoundException ex)
                {
                    return XmlReadState.Missing;
                }
                catch (DirectoryNotFoundException ex)
                {
                    return XmlReadState.Missing;
                }
                catch (XmlException ex)
                {
                    return XmlReadState.Invalid;
                }
                catch (UnauthorizedAccessException ex)
                {
                    return XmlReadState.Blocked;
                }
                catch (IOException ex)
                {
                    if (attempt < attempts && delayMs > 0)
                        WaitForRetry(delayMs, cancellationToken);
                }
            }
            return XmlReadState.Blocked;
        }

        private static void WaitForRetry(int delayMs, System.Threading.CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                if (cancellationToken.WaitHandle.WaitOne(delayMs))
                    cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                System.Threading.Thread.Sleep(delayMs);
            }
        }

        internal static bool TryReloadAfterTransientFailure(string sourcePath, string blockedSource, ref XmlDocument document)
        {
            string fullSource = Path.GetFullPath(sourcePath);
            if (string.Equals(blockedSource, fullSource + ".bak", StringComparison.OrdinalIgnoreCase))
            {
                // A backup lock was reached only after a missing/corrupt primary. Retry
                // the normal preference order, still refusing fallback from a locked primary.
                bool recovered = false;
                bool transientFailure = false;
                return TryLoadXmlWithRetries(fullSource, ref document, ref recovered, ref transientFailure, 1, 0);
            }
            return TryReloadPrimary(fullSource, ref document);
        }

        /// <summary>True when the file can be opened with an exclusive read share, i.e. the
    /// process that held it during startup has released it.</summary>
        public static bool CanReadExclusive(string sourcePath)
        {
            try
            {
                using (var stream = new FileStream(Path.GetFullPath(sourcePath), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                }
                return true;
            }
            catch (IOException ex)
            {
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                return false;
            }
        }

        /// <summary>Re-reads ONLY the primary file after a transient lock. Never falls back to
    /// the .bak: a stale backup must not become the recovered writable state.</summary>
        public static bool TryReloadPrimary(string sourcePath, ref XmlDocument document)
        {
            return ReadXml(Path.GetFullPath(sourcePath), ref document, 1, 0, System.Threading.CancellationToken.None) == XmlReadState.Loaded;
        }
    }
}