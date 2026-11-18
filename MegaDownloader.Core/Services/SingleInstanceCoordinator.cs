using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MegaDownloader
{
    /// <summary>
    /// Cross-platform single-instance gate plus an argument queue, so a second launch
    /// (a <c>mega://</c> activation from the browser, or the user double-clicking the app
    /// again) hands its links to the running window instead of starting a second copy.
    /// </summary>
    /// <remarks>
    /// The WinForms shell uses a named <see cref="System.Threading.EventWaitHandle"/> and a
    /// <c>Buffer.dat</c> spool. Named kernel objects do not exist on Unix, so this uses an
    /// exclusively locked file for the gate and a JSON-lines spool for the payload — the
    /// same idea with portable primitives. Both are per-user and per-application, so the
    /// two shells do not block each other.
    /// </remarks>
    public sealed class SingleInstanceCoordinator : IDisposable
    {
        private const string LockFileName = "instance.lock";
        private const string QueueFileName = "instance.queue";

        private readonly string _directory;
        private readonly string _lockPath;
        private readonly string _queuePath;
        private FileStream _lock;
        private bool _disposed;

        public SingleInstanceCoordinator(string appDataDirectory, string applicationName = "MegaDownloader")
        {
            if (string.IsNullOrWhiteSpace(appDataDirectory))
                throw new ArgumentException("An application data directory is required.", nameof(appDataDirectory));

            // A separate directory per application name keeps two different builds (the
            // Avalonia shell and the WinForms reference app) from blocking each other.
            _directory = Path.Combine(appDataDirectory, "Internal", applicationName);
            Directory.CreateDirectory(_directory);
            _lockPath = Path.Combine(_directory, LockFileName);
            _queuePath = Path.Combine(_directory, QueueFileName);
        }

        /// <summary>
        /// True when this process owns the instance lock. When false, the caller should
        /// forward its arguments with <see cref="SendArguments"/> and exit.
        /// </summary>
        public bool IsFirstInstance
        {
            get { return _lock is not null; }
        }

        /// <summary>
        /// Tries to become the single running instance. Returns false when another process
        /// already holds the lock.
        /// </summary>
        public bool TryAcquire()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SingleInstanceCoordinator));
            if (_lock is not null)
                return true;

            try
            {
                // FileShare.None is enforced by the OS on every platform: a second opener
                // gets an IOException instead of silently sharing the file.
                _lock = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                // A read-only or otherwise inaccessible data directory must not stop the
                // app from starting; treat it as the first instance and log the reason.
                Log.WriteWarning("Single instance lock unavailable (" + Log.SafeException(ex) + "); continuing without it.");
                return true;
            }
        }

        /// <summary>
        /// Appends this launch's arguments to the spool the running instance polls. Safe to
        /// call from a short-lived process; retries briefly while the owner drains the file.
        /// </summary>
        /// <remarks>
        /// An empty payload is written as well: it tells the running instance that the
        /// application was launched again, so it can bring its window to the front.
        /// </remarks>
        public void SendArguments(IEnumerable<string> arguments)
        {
            var payload = new List<string>();
            foreach (string argument in arguments)
            {
                if (!string.IsNullOrEmpty(argument))
                    payload.Add(argument);
            }

            string line = JsonSerializer.Serialize(payload);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using var stream = new FileStream(_queuePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                    writer.WriteLine(line);
                    writer.Flush();
                    return;
                }
                catch (IOException)
                {
                    // The owner is draining the file right now; try again shortly.
                    System.Threading.Thread.Sleep(25);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.WriteWarning("Could not forward the launch arguments: " + Log.SafeException(ex));
                    return;
                }
            }

            Log.WriteWarning("Could not forward the launch arguments: the queue file stayed locked.");
        }

        /// <summary>
        /// Drains the spool. Each entry is one launch's argument list. Never throws: a
        /// malformed line is skipped rather than breaking activation.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<string>> ReadPendingArguments()
        {
            var result = new List<IReadOnlyList<string>>();
            if (!File.Exists(_queuePath))
                return result;

            string[] lines;
            try
            {
                using (var stream = new FileStream(_queuePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                    lines = reader.ReadToEnd().Split('\n');
                    // Truncate under the same exclusive handle so a concurrent append waits
                    // instead of interleaving with the drain.
                    stream.SetLength(0);
                }
            }
            catch (IOException)
            {
                return result;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.WriteWarning("Could not read the launch argument queue: " + Log.SafeException(ex));
                return result;
            }

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;
                try
                {
                    string[]? parts = JsonSerializer.Deserialize<string[]>(trimmed);
                    // An empty array is a valid entry: it means the application was
                    // launched again without arguments, so the running window is
                    // brought to the front.
                    if (parts is not null)
                        result.Add(parts);
                }
                catch (JsonException ex)
                {
                    Log.WriteWarning("Skipping a malformed launch argument entry: " + Log.SafeException(ex));
                }
            }

            return result;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                _lock?.Dispose();
            }
            catch (IOException)
            {
            }
            _lock = null;
        }
    }
}
