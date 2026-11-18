using System;
using System.IO;

namespace MegaDownloader
{

    public class Log
    {

        public enum LevelLogType
        {
            Minimal = 0, // Only errors
            Normal = 1,  // Errors + warnings
            Info = 2,    // Errors + warnings + info
            Debug = 3   // Errors + warnings + info + debug
        }

        private static object _syncObject = new object();
        private static LevelLogType _LogLevel = LevelLogType.Normal;
        private static System.Text.StringBuilder _Buffer = null;
        private static DateTime _LastWrite = DateTime.UtcNow;
        // Log retention: files older than this many days are purged. Purge runs once
        // per process session to avoid scanning the log folder on every flush.
        private const int RetentionDays = 30;
        private static bool _purgePerformed = false;


        public static LevelLogType SetLogLevel
        {
            set
            {
                _LogLevel = value;
            }
        }

        public static void WriteError(string Text)
        {
            WriteLog(Redact(Text), LevelLogType.Minimal);
        }

        /// <summary>
    /// Exception text without full URI / MEGA link material.
    /// </summary>
        public static string SafeException(Exception ex)
        {
            if (ex is null)
                return string.Empty;
            return Redact(ex.GetType().Name + ": " + ex.Message);
        }

        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            string t = text;
            try
            {
                t = System.Text.RegularExpressions.Regex.Replace(t, @"https?://[^\s""']+", "[url]");
                t = System.Text.RegularExpressions.Regex.Replace(t, @"mega://[^\s""']+", "[mega-link]");
                t = System.Text.RegularExpressions.Regex.Replace(t, @"(?i)(filekey|key|sid|password|token)\s*[=:]\s*[^\s,;]+", "$1=[redacted]");
            }
            catch
            {
            }
            return t;
        }

        public static void WriteWarning(string Text)
        {
            WriteLog(Redact(Text), LevelLogType.Normal);
        }

        public static void WriteInfo(string Text)
        {
            WriteLog(Redact(Text), LevelLogType.Info);
        }

        public static void WriteDebug(string Text)
        {
            WriteLog(Redact(Text), LevelLogType.Debug);
        }


        public static void WriteLog(string Text, LevelLogType Level)
        {
            if (_LogLevel < Level)
                return;

            lock (_syncObject)
            {

                if (_Buffer is null)
                {
                    _Buffer = new System.Text.StringBuilder();
                }

                _Buffer.Append(DateTime.UtcNow.ToString("s"));
                _Buffer.Append(":");
                _Buffer.Append(DateTime.UtcNow.ToString("fff"));
                _Buffer.Append("Z [ID#");
                _Buffer.Append(System.Threading.Thread.CurrentThread.ManagedThreadId);
                _Buffer.Append("] >>> ");
                _Buffer.AppendLine(Text);

            }

            Flush(false);
        }

        public static void Flush(bool forceFlush)
        {
            bool DoFlush = false;

            lock (_syncObject)
            {
                DoFlush = _LastWrite.AddSeconds(10d) < DateTime.UtcNow;

                if ((DoFlush | forceFlush) & _Buffer is not null && _Buffer.Length > 0)
                {
                    string PathLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Log");

                    // N-P2-3: never let logging throw. A log file held open by another
                    // process (notepad) or a full disk used to make WriteError itself throw
                    // from inside background catch blocks, masking the original error and
                    // (on raw worker threads) killing the process. Best effort: on failure
                    // drop this batch and retry on the next write.
                    try
                    {
                        if (!Directory.Exists(PathLog))
                        {
                            Directory.CreateDirectory(PathLog);
                        }
                        using (var t = new StreamWriter(PathLog + @"\Log_" + DateTime.UtcNow.ToString("yyyyMMdd") + ".txt", true))
                        {
                            t.Write(_Buffer.ToString());
                        }
                        _Buffer = null;
                        _LastWrite = DateTime.UtcNow;

                        // Purge old log files once per process session.
                        if (!_purgePerformed)
                        {
                            _purgePerformed = true;
                            PurgeOldLogs(PathLog, RetentionDays);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Nowhere to report this — swallow and DROP this batch (re-appending
                        // it on the next write would grow the buffer without bound while the
                        // log directory stays broken; a lost log batch is the lesser evil).
                        // _LastWrite is refreshed so the next attempt happens after the
                        // regular interval instead of hammering a failing disk.
                        _Buffer = null;
                        _LastWrite = DateTime.UtcNow;
                    }
                }
            }
        }

        /// <summary>
    /// Deletes log files older than the retention period. Best-effort: any error
    /// is swallowed so logging never fails because of cleanup issues.
    /// </summary>
        private static void PurgeOldLogs(string logDir, int days)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-days);
                foreach (string f in Directory.GetFiles(logDir, "Log_*.txt"))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTimeUtc < cutoff)
                            fi.Delete();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }



    }
}