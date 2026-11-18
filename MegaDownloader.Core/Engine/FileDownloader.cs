using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{


    #region Public Class FileDownloader

    public class FileDownloader : IDisposable
    {

        #region Nested types

        #region Public Structure FileInfo

        public class DataPart
        {

            public class Chunk
            {

                public long StartIndex; // Position in the file where the chunk starts
                public long Size; // Total size of the chunk
                public long Index; // Position inside the chunk
                public bool Available;
                // P0-1 方案B：已 fsync 的边界（≤ Index）。内存游标 Index 照常按每次落盘推进，
                // XML 只持久化 SyncedIndex（Fichero.GuardarXML 钳制），保证"持久化进度 ≤ 已 fsync 数据"。
                // 不参与 XML 序列化之外的任何持久化；全 0 默认，首刷必 fsync。
                public long SyncedIndex;

                // Ex: file of 10000 bytes, 4 chunks, the fourth chunk has 60% completed:
                // * StartIndex: 7500
                // * Size: 2500
                // * Index: 1500

                public Chunk()
                {
                    StartIndex = 0L;
                    Size = 0L;
                    Index = 0L;
                    SyncedIndex = 0L;
                    Available = true;
                }
            }


            public List<Chunk> ChunkList;
            public bool AllFinished;
            private object _Mutex;

            public DataPart()
            {
                _Mutex = new object();
                AllFinished = false;
                ChunkList = new List<Chunk>();
            }

            public DataPart(long size, int numParts) : this()
            {
                long chunksize = (long)Math.Round(Math.Ceiling(size / (double)(long)numParts));
                long k16 = 16 * 1024;
                if (size < k16 | numParts < 2 | chunksize < k16)
                {
                    // Just 1 chunk
                    var Chunk = new Chunk();
                    Chunk.StartIndex = 0L;
                    Chunk.Index = 0L;
                    Chunk.Size = size;
                    Chunk.Available = true;
                    ChunkList.Add(Chunk);
                    Log.WriteDebug(string.Format("Setting chunk {0}: From: {1} - Size: {2}", 1, Chunk.StartIndex, Chunk.Size));
                }
                else
                {
                    // Lo hacemos múltiplo de 16k
                    chunksize = (long)Math.Round(Math.Ceiling(chunksize / (double)k16) * k16);
                    long lastchunksize = size - chunksize * (numParts - 1);
                    if (lastchunksize < 0L)
                    {
                        // ???
                        // Just 1 chunk
                        var Chunk = new Chunk();
                        Chunk.StartIndex = 0L;
                        Chunk.Index = 0L;
                        Chunk.Size = size;
                        Chunk.Available = true;
                        ChunkList.Add(Chunk);
                        Log.WriteDebug(string.Format("Setting chunk {0}: From: {1} - Size: {2}", 1, Chunk.StartIndex, Chunk.Size));
                    }
                    else
                    {
                        for (int i = 1, loopTo = numParts; i <= loopTo; i++)
                        {
                            var Chunk = new Chunk();
                            Chunk.StartIndex = (i - 1) * chunksize;
                            Chunk.Index = 0L;
                            Chunk.Available = true;
                            if (i == numParts)
                            {
                                Chunk.Size = lastchunksize;
                            }
                            else
                            {
                                Chunk.Size = chunksize;
                            }
                            ChunkList.Add(Chunk);
                            Log.WriteDebug(string.Format("Setting chunk {0}: From: {1} - Size: {2}", i, Chunk.StartIndex, Chunk.Size));
                        }
                    }

                }

            }

            public void ResetAvailableParts()
            {
                lock (_Mutex)
                {
                    foreach (Chunk c1 in ChunkList)
                    {
                        if (c1.Index != c1.Size)
                        {
                            c1.Available = true;
                        }
                    }
                }
            }

            /// <summary>P0-1:线程安全地汇总已下载字节(各 chunk Index 之和),供看门狗判断有无进度。</summary>
            public long GetTotalProgress()
            {
                lock (_Mutex)
                {
                    long total = 0L;
                    if (ChunkList is not null)
                    {
                        foreach (Chunk c1 in ChunkList)
                            total += c1.Index;
                    }
                    return total;
                }
            }


            public int? NextAvailablePartIndex
            {
                get
                {
                    lock (_Mutex)
                    {
                        int? ret = default;
                        int i = 0;
                        foreach (Chunk c1 in ChunkList)
                        {
                            if (c1.Available)
                            {
                                ret = i;
                                c1.Available = false;
                                break;
                            }
                            i += 1;
                        }
                        return ret;
                    }
                }
            }

            public void SetProgress(int chunkIndex, long index)
            {
                lock (_Mutex)
                {
                    var c = ChunkList[chunkIndex];
                    c.Index = index;
                    if (c.Index == c.Size)
                    {
                        bool Missing = false;
                        foreach (Chunk c1 in ChunkList)
                        {
                            if (c1.Index != c1.Size)
                            {
                                Missing = true;
                            }
                        }
                        if (!Missing)
                        {
                            AllFinished = true;
                        }
                    }
                }
            }

            /// <summary>
        /// Validates chunk layout and clears AllFinished when incomplete or inconsistent.
        /// </summary>
            public bool ValidateAndNormalize(long expectedSize)
            {
                lock (_Mutex)
                {
                    if (ChunkList is null || ChunkList.Count == 0)
                    {
                        AllFinished = false;
                        return false;
                    }
                    if (expectedSize < 0L)
                    {
                        AllFinished = false;
                        return false;
                    }

                    var ordered = ChunkList.OrderBy(c => c.StartIndex).ToList();
                    long cursor = 0L;
                    foreach (Chunk c in ordered)
                    {
                        if (c.StartIndex < 0L || c.Size < 0L || c.Index < 0L || c.Index > c.Size)
                        {
                            AllFinished = false;
                            return false;
                        }
                        if (c.StartIndex != cursor)
                        {
                            AllFinished = false;
                            return false;
                        }
                        cursor += c.Size;
                    }
                    if (cursor != expectedSize)
                    {
                        AllFinished = false;
                        return false;
                    }

                    // Round any non-aligned partial progress down to a 16-byte boundary.
                    // The CTR keystream can only be seeked in whole blocks; resuming from a
                    // non-aligned offset (persisted by older builds) would decrypt all
                    // following data with the wrong keystream and corrupt the file.
                    foreach (Chunk c in ordered)
                    {
                        if (c.Index != c.Size && c.Index > 0L && (c.Index & 15L) != 0L)
                        {
                            Log.WriteWarning("ValidateAndNormalize: rounding chunk progress down to a 16-byte boundary (" + c.StartIndex + "+" + c.Index + " -> " + (c.StartIndex + (c.Index & (long)~15)) + ")");
                            c.Index = c.Index & ~15L;
                        }
                        // 方案B：已同步边界永不超过持久化游标（旧版本无此字段时 SyncedIndex=0，
                        // 钳制只会让重启多重下零星数据，方向安全）。
                        if (c.SyncedIndex > c.Index)
                            c.SyncedIndex = c.Index;
                    }

                    bool missing = ordered.Any(c => c.Index != c.Size);
                    AllFinished = !missing;
                    return true;
                }
            }

        }

        /// <summary>Simple structure for managing file info</summary>
        public class FileInfo
        {

            /// <summary>The complete path of the file (directory + filename)</summary>
            public string Path;

            public string FileID;
            public string FileKey;
            public string PartialFileName = CreatePartialFileName();
            internal bool AllowLegacyPartial;
            public int NumParts;
            private long _Size;

            private DataPart _dataPart;
            private object _Mutex;
            /// <summary>The name of the file</summary>
            private string _Name;

            public string Name
            {
                get
                {
                    return _Name;
                }
                set
                {
                    _Name = PathGuard.SanitizeFileName(value + "", "file");
                }
            }
            /// <summary>Create a new instance of FileInfo</summary>
        /// <param name="path">The complete path of the file (directory + filename)</param>
            public FileInfo(string path)
            {
                _Mutex = new object();
                Path = path;
                Name = Path.Split('/')[Path.Split('/').Length - 1];
                NumParts = 1;
                Size = 0L;
                _dataPart = null;
            }

            public bool DataPartInitialized
            {
                get
                {
                    return _dataPart is not null;
                }
            }


            public DataPart GetDataPart
            {
                get
                {
                    if (Size == 0L)
                    {
                        throw new InvalidOperationException("Must specify size");
                    }
                    lock (_Mutex)
                    {
                        if (_dataPart is null)
                        {
                            _dataPart = new DataPart(Size, NumParts);
                        }
                        return _dataPart;
                    }
                }
            }

            public void SetDataPart(DataPart d)
            {
                lock (_Mutex)
                {
                    if (d is not null)
                    {
                        _dataPart = d;
                        _dataPart.ResetAvailableParts();
                    }
                }
            }

            public long Size
            {
                get
                {
                    return _Size;
                }
                set
                {
                    _Size = value;
                    if (DataPartInitialized)
                    {
                        long Tamano = 0L;
                        lock (_Mutex)
                        {
                            foreach (DataPart.Chunk c in _dataPart.ChunkList)
                            {
                                // Tamano += c.Size
                                // Now the chunks can be resized depending on MEGA response... so we will take the biggest chunk
                                if (c.StartIndex + c.Size > Tamano)
                                {
                                    Tamano = c.StartIndex + c.Size;
                                }
                            }
                        }
                        if (value != Tamano)
                        {
                            throw new InvalidOperationException("File size does not match [" + value + " - " + Tamano + "]");
                        }
                    }
                }
            }

        }
        #endregion

        #region Private Enum [Event]
        /// <summary>Holder for events that are triggered in the background worker but need to be fired in the main thread</summary>
        private enum Event
        {
            CalculationFileSizesStarted,

            FileSizesCalculationComplete,
            CreatingFilesLocal,
            FilesLocalCreated,
            DeletingFilesAfterCancel,

            FileDownloadAttempting,
            FileDownloadStarted,
            FileDownloadStopped,
            FileDownloadSucceeded,

            ProgressChanged
        }
        #endregion

        #region Private Enum InvokeType
        /// <summary>Holder for the action that needs to be invoked</summary>
        private enum InvokeType
        {
            EventRaiser,
            FileDownloadFailedRaiser,
            ChunkDownloadFailedRaiser,
            CalculatingFileNrRaiser,
            StartDownloaderRaiser
        }
        #endregion

        private class DownloaderWorker : BackgroundWorker
        {

            private int _ChunkIndex;
            private FileInfo _file;
            private bool _ChunkDownloadFailed;
            public readonly long Generation;

            public DownloaderWorker(int ChunkIndex, FileInfo @file, long generation)
            {
                Generation = generation;
                _ChunkIndex = ChunkIndex;
                _file = @file;
                _ChunkDownloadFailed = false;
            }

            public bool ChunkDownloadFailed
            {
                get
                {
                    return _ChunkDownloadFailed;
                }
                set
                {
                    _ChunkDownloadFailed = value;
                }
            }
            public FileInfo File
            {
                get
                {
                    return _file;
                }
            }
            public int ChunkIndex
            {
                get
                {
                    return _ChunkIndex;
                }
            }
        }

        #endregion

        #region Events
        /// <summary>Occurs when the file downloading has started</summary>
        public event EventHandler Started;
        /// <summary>Occurs when the file downloading has been paused</summary>
        public event EventHandler Paused;
        /// <summary>Occurs when the file downloading has been resumed</summary>
        public event EventHandler Resumed;
        /// <summary>Occurs when the user has requested to cancel the downloads</summary>
        public event EventHandler CancelRequested;
        /// <summary>Occurs when the user has requested to cancel the downloads and the cleanup of the downloaded files has started</summary>
        public event EventHandler DeletingFilesAfterCancel;
        /// <summary>Occurs when the file downloading has been canceled by the user</summary>
        public event EventHandler Canceled;
        /// <summary>Occurs when the file downloading has been completed (without canceling it)</summary>
        public event EventHandler Completed;
        /// <summary>Occurs when the file downloading has been stopped by either cancellation or completion</summary>
        public event EventHandler Stopped;

        /// <summary>Occurs when the busy state of the FileDownloader has changed</summary>
        public event EventHandler IsBusyChanged;
        /// <summary>Occurs when the pause state of the FileDownloader has changed</summary>
        public event EventHandler IsPausedChanged;
        /// <summary>Occurs when the either the busy or pause state of the FileDownloader have changed</summary>
        public event EventHandler StateChanged;

        /// <summary>Occurs when the calculation of the file sizes has started</summary>
        public event EventHandler CalculationFileSizesStarted;
        /// <summary>Occurs when the calculation of the file sizes has started</summary>
        public event FileSizeCalculationEventHandler CalculatingFileSize;
        /// <summary>Occurs when the calculation of the file sizes has been completed</summary>
        public event EventHandler FileSizesCalculationComplete;

        /// <summary>Occurs when the FileDownloader attempts to get a web response to download the file</summary>
        public event EventHandler FileDownloadAttempting;
        /// <summary>Occurs when a file download has started</summary>
        public event EventHandler FileDownloadStarted;
        /// <summary>Occurs when a file download has stopped</summary>
        public event EventHandler FileDownloadStopped;
        /// <summary>Occurs when a file download has been completed successfully</summary>
        public event EventHandler FileDownloadSucceeded;
        /// <summary>Occurs when the .part file is renamed to its final name on disk.
    /// The final name may differ from the requested name ("name (2).ext" on collision, GUID after 9999 tries).
    /// Raised synchronously right after the rename, always before FileDownloadSucceeded.</summary>
        public event FileRenamedEventHandler FileRenamed;

        public delegate void FileRenamedEventHandler(object sender, FileRenamedEventArgs e);
        /// <summary>Occurs when a file download has been completed unsuccessfully</summary>
        public event FileDownloadFailedEventHandler FileDownloadFailed;

        public delegate void FileDownloadFailedEventHandler(object sender, Exception e);
        /// <summary>Occurs when a chunk download has been completed unsuccessfully</summary>
        public event ChunkDownloadFailedEventHandler ChunkDownloadFailed;

        public delegate void ChunkDownloadFailedEventHandler(object sender, Exception e);
        /// <summary>Occurs every time a block of data has been downloaded</summary>
        public event EventHandler ProgressChanged;

        public event EventHandler FileLocalCreated;

        #endregion

        #region Fields
        public delegate void FileSizeCalculationEventHandler(object sender);


        private BackgroundWorker bgwDownloader;
        private List<DownloaderWorker> listDownloaders;
        private object Mutex = new object(); // Sync workers for object manipulation
        private object MutexFile = new object(); // Sync workers for disk write
        private static readonly object FinalFileLock = new object();
        private string m_completedFilePath;
        // P0-1 方案A：.part 长生命周期写流。FlushToDisk 复用本流，只做 Position+Write+Flush(True)，
        // 省掉每次 open/close；fsync 语义不变，"持久化进度 ≤ 已 fsync 数据"红线不变。
        // 仅在 MutexFile 保护下访问；.part 重建/重命名前必须先 ClosePartStream。
        private FileStream m_partStream = null;
        private string m_partStreamPath = null;
        // P0-1 方案B：fsync 节流记账。达到任一阈值才 Flush(True)，其余落盘只 Write。
        private DateTime m_lastFsyncUtc = DateTime.MinValue;
        private const long FsyncEveryBytes = 8L * 1024L * 1024L;
        private const double FsyncEverySeconds = 5.0d;
        private System.Threading.ManualResetEvent trigger = new System.Threading.ManualResetEvent(true);
        private int m_num_connections, m_parts_per_file;


        // Preferences
        private bool m_supportsProgress, m_deleteFiles, m_deleteCompletedFiles;
        private int m_packageSize, m_bufferSize, m_stopWatchCycles;

        // State
        private bool m_disposed = false;
        private bool m_busy, m_paused, m_canceled;
        private bool m_stopCompletionPending;
        // 第10项:停滞看门狗/停止路径统一取消 chunk worker 后禁止复活的标记。
        // 旧代码这些路径只 CancelAsync 不改 m_canceled(该字段只在 IsBusy setter 赋值),
        // 于是 ChunkDownloader_RunWorkerCompleted 的 HasBeenCanceled 恒 False →
        // NewDownloaderWorker() 一比一复活:失败已报但字节仍在流、排空循环白等满 30s、
        // 孤儿 worker 下完后终结块早已跳过 → 完整文件留成 .part 且标红。
        // 复用 IsBusy=False(m_canceled)不可行:Canceled 事件会把 Erroneo 状态覆盖成
        // EnCola 并拆掉 Downloader。此标记在每次 Start(IsBusy=True)时复位。
        private bool m_abortChunkRestart;
        // 退避指数必须来自"连续失败次数"。此前用存活 worker 数,而失败的 worker 已被移除,
        // 最后一个连接失败时该数恒为 0 → 恒定 1 秒重试,指数退避形同虚设。
        private int m_consecutiveChunkFailures;
        // Mutations and chunk creation share Mutex. Workers retain the generation assigned at creation.
        private long m_chunkGeneration;
        private long m_currentFileProgress, m_totalProgress, m_currentFileSize;
        private Dictionary<string, long> m_currentSpeed;

        /// <summary>
    /// P1-1:停滞看门狗的"活"判据 —— 内存已收字节数(每 ~50KB 动一次)。
    /// 此前用 GetTotalProgress()(各 chunk 已落盘 Index 之和),而 Index 只在
    /// FlushToDisk 里推进,落盘粒度 = BufferSize-PackageSize(默认 700 KiB)。
    /// 于是慢速下载(总速低于阈值)会长时间"落盘进度不动"而被判停滞误杀,
    /// 表现为进度条在走却每 ~2 分钟变红一次。
    /// 内存进度在每个包(50KB)读满时推进,粒度细两个数量级,慢速也能及时续命。
    /// 线程安全:读取与写入同用 Mutex(写侧见 ChunkDownloader 内的 SyncLock Mutex)。
    /// </summary>
        private long GetMemoryProgress()
        {
            lock (Mutex)
                return m_currentFileProgress;
        }

        // Data
        private string m_localDirectory;
        private FileInfo m_file;
        private long m_totalSize;
        // 启动时探大小失败的暂存：不再立即上报（探活 HEAD 抖一下就报失败，
        // 而 downloadFile 里会重探并可能成功——先报失败后报成功会把好文件钉成 Erroneo）。
        // 若后续下载也失败，以下载阶段自己的错误为准；若成功，此错误直接丢弃。
        private Exception m_sizeProbeError = null;
        /// <summary>v2.5 beta: 配额异常归一上报。返回 True 表示已按配额处理(调用方不再普通重试)。</summary>
        internal static bool ReportQuotaIfMatch(Exception ex)
        {
            if (ex is MegaQuotaExceededException)
            {
                MegaQuotaManager.ReportQuota();
                return true;
            }
            WebException wex = ex as WebException;
            if (wex is not null && MegaQuotaManager.IsQuotaWebException(wex))
            {
                var hint = MegaQuotaManager.TryGetRetryAfterSeconds(wex);
                MegaQuotaManager.ReportQuota(hint.HasValue ? hint.Value : 0L);
                return true;
            }
            if (ex is not null && Conexion.IsQuotaErrorText(ex.Message))
            {
                MegaQuotaManager.ReportQuota();
                return true;
            }
            return false;
        }
        #endregion

        #region Constructors
        /// <summary>Create a new instance of a FileDownloader</summary>
    /// <param name="supportsProgress">Optional. Boolean. Should the FileDownloader support total progress statistics?</param>
        public FileDownloader(bool supportsProgress = false)
        {
            bgwDownloader = new BackgroundWorker();
            listDownloaders = new List<DownloaderWorker>();
            // Set the bgw properties
            bgwDownloader.WorkerReportsProgress = true;
            bgwDownloader.WorkerSupportsCancellation = true;
            // Set the default class preferences
            SupportsProgress = supportsProgress;
            BufferSize = 500 * 1024;
            PackageSize = 50 * 1024;
            StopWatchCyclesAmount = 30;
            PartsPerFile = 1;
            NumConnections = 1;
            DeleteCompletedFilesAfterCancel = false;
            DeleteFilesAfterCancel = true;
            m_currentSpeed = new Dictionary<string, long>();
            bgwDownloader.DoWork += bgwDownloader_DoWork;
            bgwDownloader.ProgressChanged += bwgDownloader_ProgressChanged;
            bgwDownloader.RunWorkerCompleted += bgwDownloader_RunWorkerCompleted;
        }
        #endregion

        #region Public methods

        public void AddFileInfo(string FileID, string FileKey, string path, string name, DataPart part, string partialFileName = null)
        {
            var Info = new FileInfo(path);
            Info.Name = name;
            Info.AllowLegacyPartial = string.IsNullOrEmpty(partialFileName);
            if (!string.IsNullOrEmpty(partialFileName))
                Info.PartialFileName = ValidatePartialFileName(partialFileName);
            Info.NumParts = PartsPerFile;
            Info.SetDataPart(part);
            Info.FileID = FileID;
            Info.FileKey = FileKey;
            File = Info;
            MigrateLegacyPartial(Info, part);
        }

        public static string CreatePartialFileName()
        {
            return ".megadownloader-" + Guid.NewGuid().ToString("N") + ".part";
        }

        internal static string ValidatePartialFileName(string value)
        {
            PathGuard.RequireSafePathSegment(value, "partial file name");
            const string prefix = ".megadownloader-";
            Guid taskId;
            if (value.Length != prefix.Length + 32 + 5 || !value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(".part", StringComparison.Ordinal) || !Guid.TryParseExact(value.Substring(prefix.Length, 32), "N", out taskId))
            {
                throw new ArgumentException("Invalid task partial file name.");
            }
            return value;
        }

        public static string GetPartialFilePath(string rootPath, string fileName, string partialFileName)
        {
            if (string.IsNullOrEmpty(partialFileName))
            {
                return PathGuard.GetSafeFilePathUnderRoot(rootPath, fileName + ".part");
            }
            ValidatePartialFileName(partialFileName);
            return PathGuard.GetSafePathUnderRoot(rootPath, partialFileName, allowRoot: false);
        }

        private void MigrateLegacyPartial(FileInfo @file, DataPart part)
        {
            if (!@file.AllowLegacyPartial || part is null || string.IsNullOrEmpty(LocalDirectory))
                return;
            lock (FinalFileLock)
            {
                string target = GetPartialFilePath(LocalDirectory, @file.Name, @file.PartialFileName);
                if (!System.IO.File.Exists(target) && part.GetTotalProgress() > 0L)
                {
                    string legacy = GetPartialFilePath(LocalDirectory, @file.Name, null);
                    if (System.IO.File.Exists(legacy))
                        System.IO.File.Move(legacy, target);
                }
                @file.AllowLegacyPartial = false;
            }
        }

        /// <summary>Start the downloads</summary>
        public void Start()
        {
            if (CanStart)
                IsBusy = true;
        }

        /// <summary>pause the downloads</summary>
        public void Pause()
        {
            IsPaused = true;
        }

        /// <summary>Resume the downloads</summary>
        public void Resume()
        {
            IsPaused = false;
        }

        /// <summary>Stop the downloads</summary>
        public void Stop()
        {
            IsBusy = false;
        }

        /// <summary>Stop the downloads</summary>
    /// <param name="deleteCompletedFiles">Required. Boolean. Indicates wether the complete downloads should be deleted</param>
        public void Stop(bool deleteCompletedFiles)
        {
            DeleteCompletedFilesAfterCancel = deleteCompletedFiles;
            Stop();
        }

        /// <summary>Release the recources held by the FileDownloader</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Format an amount of bytes to a more readible notation with binary notation symbols</summary>
    /// <param name="size">Required. Int64. The raw amount of bytes</param>
    /// <param name="decimals">Optional. Int32. The amount of decimals you want to have displayed in the notation</param>
        public static string FormatSizeBinary(long size, int decimals = 2)
        {
            // By De Dauw Jeroen - April 2009 - jeroen_dedauw@yahoo.com
            string[] sizes = new string[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB" };
            double formattedSize = Math.Abs((double)size);
            int sizeIndex = 0;
            while (formattedSize >= 1024d && sizeIndex < sizes.Length - 1)
            {
                formattedSize /= 1024d;
                sizeIndex += 1;
            }
            return Math.Round(formattedSize, decimals).ToString() + sizes[sizeIndex];
        }

        /// <summary>Format an amount of bytes to a more readible notation with decimal notation symbols</summary>
    /// <param name="size">Required. Int64. The raw amount of bytes</param>
    /// <param name="decimals">Optional. Int32. The amount of decimals you want to have displayed in the notation</param>
        public static string FormatSizeDecimal(long size, int decimals = 2)
        {
            // By De Dauw Jeroen - April 2009 - jeroen_dedauw@yahoo.com
            string[] sizes = new string[] { "B", "kB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            double formattedSize = Math.Abs((double)size);
            int sizeIndex = 0;
            while (formattedSize >= 1000d && sizeIndex < sizes.Length - 1)
            {
                formattedSize /= 1000d;
                sizeIndex += 1;
            }
            return Math.Round(formattedSize, decimals).ToString() + sizes[sizeIndex];
        }

        /// <summary>
    /// Resolves remote content length via HEAD, falling back to a 1-byte ranged GET.
    /// </summary>
        private static long ProbeRemoteFileSize(string url)
        {
            try
            {
                var webReq = Conexion.CreateHttpWebRequest(url);
                webReq.Method = "HEAD";
                webReq.Timeout = 15000;
                using (HttpWebResponse webResp = (HttpWebResponse)webReq.GetResponse())
                {
                    if (webResp.ContentLength >= 0L)
                        return webResp.ContentLength;
                }
            }
            catch
            {
                // HEAD may be unsupported; try ranged GET below
            }

            var rangeReq = Conexion.CreateHttpWebRequest(url);
            rangeReq.Method = "GET";
            rangeReq.Timeout = 15000;
            rangeReq.AddRange(0, 0);
            using (HttpWebResponse rangeResp = (HttpWebResponse)rangeReq.GetResponse())
            {
                string contentRange = rangeResp.Headers["Content-Range"];
                if (!string.IsNullOrEmpty(contentRange) && contentRange.Contains("/"))
                {
                    string totalPart = contentRange.Substring(contentRange.LastIndexOf('/') + 1).Trim();
                    long totalLen;
                    if (totalPart != "*" && long.TryParse(totalPart, out totalLen) && totalLen >= 0L)
                    {
                        return totalLen;
                    }
                }
                if (rangeResp.ContentLength >= 0L && rangeResp.StatusCode == HttpStatusCode.OK)
                {
                    return rangeResp.ContentLength;
                }
            }
            return -1;
        }
        #endregion

        #region Private/protected methods


        private void bgwDownloader_DoWork(object ender, DoWorkEventArgs e)
        {
            try
            {
                m_sizeProbeError = null;
                try
                {
                    if (SupportsProgress)
                        calculateFilesSize();
                }
                catch (Exception ex)
                {
                    Log.WriteError("Error in bgwDownloader.DoWork/calculateFilesSize (deferred, reported only if download never succeeds): " + ex.ToString());
                    m_sizeProbeError = ex;
                }


                if (!Directory.Exists(LocalDirectory))
                    Directory.CreateDirectory(LocalDirectory);

                if (!bgwDownloader.CancellationPending)
                {

                    downloadFile();

                    if (bgwDownloader.CancellationPending)
                    {
                        if (DeleteFilesAfterCancel)
                        {
                            fireEventFromBgw(Event.DeletingFilesAfterCancel);
                            // 取消删 .part 前先关复用流，否则打开中的句柄会挡住 Delete。
                            try
                            {
                                lock (MutexFile)
                                    ClosePartStream();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteWarning("DoWork: part stream close before cleanup failed: " + Log.SafeException(ex));
                            }
                            cleanUpFile();
                        }
                        e.Cancel = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Error in bgwDownloader.DoWork: " + ex.ToString());
                // Do not call MessageBox.Show from a background thread — it has no parent window,
                // blocks the worker, and can hang the download pipeline. Surface the failure via
                // the standard progress channel so the UI thread can present it.
                try
                {
                    bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, ex);
                }
                catch (Exception reportEx)
                {
                    Log.WriteError("Failed to report bgwDownloader.DoWork error: " + reportEx.ToString());
                }
            }
        }

        private void downloadFile()
        {

            var @file = m_file;

            Log.WriteWarning("Starting file download " + @file.Name);

            string FicheroPART = GetPartialFilePath(LocalDirectory, @file.Name, @file.PartialFileName);
            string FicheroReal = PathGuard.GetSafeFilePathUnderRoot(LocalDirectory, @file.Name);

            Exception exc = null;

            do
            {
                try
                {

                    // Get size (HEAD, fall back to ranged GET if HEAD unsupported)
                    if (@file.Size == 0L)
                    {
                        try
                        {
                            long TotalSize = ProbeRemoteFileSize(File.Path);
                            if (TotalSize < 0L)
                            {
                                throw new ApplicationException("Could not determine remote file size.");
                            }
                            // P1-6: the persisted chunk layout belongs to the OLD file size and
                            // the Size setter validates the layout extent, throwing on mismatch.
                            // That exception used to be caught below and exit downloadFile right
                            // here, so the ValidateAndNormalize self-heal further down was dead
                            // code for size-changed files — every retry (and the 15-minute
                            // self-heal cycle) failed identically, forever. Rebuild the layout
                            // from the freshly probed size and retry the assignment.
                            try
                            {
                                @file.Size = TotalSize;
                            }
                            catch (InvalidOperationException exSize)
                            {
                                Log.WriteWarning("File size changed for " + @file.Name + " (now " + TotalSize + "); resetting chunk layout - " + Log.SafeException(exSize));
                                @file.SetDataPart(new DataPart(TotalSize, @file.NumParts));
                                @file.Size = TotalSize;
                            }
                            m_currentFileSize = TotalSize;
                            Log.WriteInfo("File size " + @file.Name + ": " + TotalSize);
                        }
                        catch (WebException ex)
                        {
                            ReportQuotaIfMatch(ex);
                            exc = ex;
                        }
                        catch (Exception ex)
                        {
                            ReportQuotaIfMatch(ex);
                            exc = ex;
                        }
                    }

                    // Check errors
                    if (exc is not null)
                    {
                        Log.WriteError("Error downloading file " + @file.Name + " - " + Log.SafeException(exc));
                        bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, exc);
                        exc = null;
                        break;
                    }

                    // Claim old name.part downloads once, then recover completed output
                    // before allocating disk space or starting any chunk requests.
                    if (@file.Size > 0L && @file.DataPartInitialized)
                    {
                        MigrateLegacyPartial(@file, @file.GetDataPart);
                        if (@file.GetDataPart.ValidateAndNormalize(@file.Size) && @file.GetDataPart.AllFinished && !System.IO.File.Exists(FicheroPART) && TryReuseCompletedFile(FicheroReal, @file))
                        {
                            return;
                        }
                    }

                    // Existing partial files already reserve space on disk.
                    try
                    {
                        string root = Path.GetPathRoot(Path.GetFullPath(LocalDirectory));
                        if (!string.IsNullOrEmpty(root))
                        {
                            var di = new DriveInfo(root);
                            if (di.IsReady)
                                EnsureDownloadSpace(FicheroPART, @file.Size, di.AvailableFreeSpace);
                        }
                    }
                    catch (IOException ex)
                    {
                        exc = ex;
                    }
                    catch (Exception ex)
                    {
                        Log.WriteWarning("Disk space check skipped: " + Log.SafeException(ex));
                    }
                    if (exc is not null)
                    {
                        Log.WriteError("Error preparing download " + @file.Name + " - " + Log.SafeException(exc));
                        bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, exc);
                        exc = null;
                        break;
                    }

                    // B1-③:0 字节空文件短路。探活后仍为 0 即 MEGA 占位空文件:不建 DataPart
                    // (FileInfo.Size=0 时 GetDataPart 抛 "Must specify size")、不开连接,
                    // 直接落 0 字节文件、验 MetaMAC(空文件期望 (0,0))、走正常重命名与成功事件。
                    // 此前必经 GetDataPart 抛错 + 自愈每 15 分钟空转,永不成功。
                    if (@file.Size == 0L && !bgwDownloader.CancellationPending)
                    {
                        try
                        {
                            Log.WriteInfo("Finalizing empty (0-byte) file " + @file.Name);
                            lock (MutexFile)
                            {
                                using (var fs = new FileStream(FicheroPART, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                }
                            }
                            string keyForEmpty = @file.FileKey;
                            if (!string.IsNullOrEmpty(keyForEmpty) && keyForEmpty.Contains("=###n="))
                            {
                                keyForEmpty = keyForEmpty.Substring(0, keyForEmpty.IndexOf("=###n="));
                            }
                            if (string.IsNullOrEmpty(keyForEmpty))
                            {
                                throw new ApplicationException("FileKey not defined");
                            }
                            if (!Criptografia.VerifyMegaMetaMac(FicheroPART, keyForEmpty))
                            {
                                throw new ApplicationException("MEGA MetaMAC verification failed for empty file " + @file.Name + ".");
                            }
                            string finalNameEmpty = "";
                            lock (MutexFile)
                                finalNameEmpty = RenamePartToReal(FicheroPART, FicheroReal);
                            if (!string.IsNullOrEmpty(finalNameEmpty))
                                FileRenamed?.Invoke(this, new FileRenamedEventArgs(finalNameEmpty));
                            Log.WriteWarning("File downloaded successfully");
                            fireEventFromBgw(Event.FileDownloadSucceeded);
                        }
                        catch (Exception emptyEx)
                        {
                            Log.WriteError("Error finalizing empty file " + @file.Name + " - " + Log.SafeException(emptyEx));
                            bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, emptyEx);
                        }
                        break;
                    }

                    // Validate resume metadata before trusting AllFinished
                    if (@file.DataPartInitialized)
                    {
                        if (!@file.GetDataPart.ValidateAndNormalize(@file.Size))
                        {
                            Log.WriteWarning("Invalid resume metadata for " + @file.Name + "; restarting chunks.");
                            @file.SetDataPart(new DataPart(@file.Size, @file.NumParts));
                        }
                    }

                    // Check size
                    if (!System.IO.File.Exists(FicheroPART))
                    {
                        // No bytes on disk means no resumable progress, including partial
                        // (not just AllFinished) metadata left after cleanup or a move.
                        @file.SetDataPart(new DataPart(@file.Size, @file.NumParts));

                        fireEventFromBgw(Event.CreatingFilesLocal);
                        // Creamos un fichero vacío con ese tamaño
                        lock (MutexFile)
                        {
                            try
                            {
                                // 复用流必须先关，否则 File.Create 会 sharing violation；语义不变。
                                ClosePartStream();
                                using (var Stream = System.IO.File.Create(FicheroPART, 64 * 1024, FileOptions.RandomAccess))
                                {
                                    Stream.SetLength(@file.Size);
                                    Stream.Flush(true);
                                }
                            }
                            catch (Exception ex)
                            {
                                exc = ex;
                            }
                        }
                    }
                    else
                    {
                        // ①:.part 尺寸与远端不一致(远端替换/截断/磁盘满短文件)必须就地修复,
                        // 否则报一次错后自愈 preserve 原样保留,下次必进同一分支,每 15min 空转一次。
                        // 此处删坏件、重置分块、按正确尺寸重建后继续下载,不再抛错。
                        bool rebuiltPart = false;
                        lock (MutexFile)
                        {
                            try
                            {
                                var inf = new System.IO.FileInfo(FicheroPART);
                                if (inf.Length != @file.Size)
                                {
                                    Log.WriteWarning("Part size mismatch for " + @file.Name + " (disk " + inf.Length + " vs expected " + @file.Size + "); rebuilding .part.");
                                    // 重建前关闭复用流，否则 Delete/Create 会被占用中的句柄挡住。
                                    ClosePartStream();
                                    try
                                    {
                                        System.IO.File.Delete(FicheroPART);
                                    }
                                    catch (Exception delEx)
                                    {
                                        exc = new ApplicationException("The file exists and does not have the expected size [" + inf.Length + " - " + @file.Size + "]");
                                        Log.WriteWarning("Could not delete mismatched .part: " + Log.SafeException(delEx));
                                    }
                                    if (exc is null)
                                    {
                                        @file.SetDataPart(new DataPart(@file.Size, @file.NumParts));
                                        using (var Stream = System.IO.File.Create(FicheroPART, 64 * 1024, FileOptions.RandomAccess))
                                        {
                                            Stream.SetLength(@file.Size);
                                            Stream.Flush(true);
                                        }
                                        rebuiltPart = true;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                exc = ex;
                            }
                        }
                        if (rebuiltPart)
                            fireEventFromBgw(Event.CreatingFilesLocal);

                    }

                    // Check errors
                    if (exc is not null)
                    {
                        Log.WriteError("Error creating file on disk " + @file.Name + " - " + Log.SafeException(exc));
                        bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, exc);
                        exc = null;
                        break;
                    }

                    // Actualizamos el estado descargado (por si hemos parado)
                    foreach (DataPart.Chunk chunk in @file.GetDataPart.ChunkList)
                    {
                        m_currentFileProgress += chunk.Index;
                        m_totalProgress += chunk.Index;
                    }

                    fireEventFromBgw(Event.FilesLocalCreated);
                    if (bgwDownloader.CancellationPending)
                    {
                        Log.WriteWarning("File download stopped - " + @file.Name);
                        return;
                    }

                    // Already fully downloaded on disk with valid metadata — still verify MAC before success
                    if (!@file.GetDataPart.AllFinished)
                    {
                        for (int i = 1, loopTo = NumConnections; i <= loopTo; i++)
                        {
                            Log.WriteDebug("Event raised for starting a new connection");
                            bgwDownloader.ReportProgress((int)InvokeType.StartDownloaderRaiser, System.Threading.Interlocked.Read(ref m_chunkGeneration));
                        }
                        // Wait until all chunks finish. NOTE: there is deliberately NO size-based
                        // force-finish here — the .part file is preallocated to the full size, so
                        // "file size == expected size" is ALWAYS true and proves nothing about the
                        // downloaded content. AllFinished is only ever set by real chunk completion
                        // (a 403 storm at the tail previously left holes that were force-finished).
                        var waitStartTime = DateTime.Now;
                        bool timedOut = false;
                        // P0-1:无进度超时(stall watchdog)。此前是总时长墙钟:大文件即使一直在推进,
                        // 只要墙钟超 120s 就判失败,叠加自愈删 .part 形成无限循环。此处每次有 chunk
                        // 推进就重置计时,只有真正停滞 120s 才超时。
                        // 
                        // P1-1:判据从"已落盘进度"改为"内存进度"。GetTotalProgress() 只累加各 chunk 的
                        // Index,而 Index 仅在 FlushToDisk 里推进,落盘粒度 = BufferSize-PackageSize
                        // (默认 750KB-50KB = 700 KiB)。于是慢速下载在两次落盘之间内存进度静止,
                        // 只要总速低于 落盘粒度/120s 就会被误判停滞 → 进度条在走却周期性变红。
                        // m_currentFileProgress 每读满一个包(50KB)就推进,粒度细两个数量级。
                        // 两个信号取"或":任一推进即视为存活,只有两者同时静止才算真停滞。
                        // (落盘信号保留作兜底:内存计数在 IsBusy 复位时归零,极端时序下仍以落盘为准。)
                        long lastProgress = GetMemoryProgress();
                        long lastLandedProgress = @file.GetDataPart.GetTotalProgress();
                        do
                        {
                            System.Threading.Thread.Sleep(100);

                            // ④:暂停补偿。trigger.WaitOne() 在暂停期无限阻塞,墙钟会把暂停时长
                            // 算成停滞,暂停超 120s 恢复即变红。此处 100ms 轮询,暂停期不断顺延计时。
                            while (!trigger.WaitOne(100))
                            {
                                waitStartTime = DateTime.Now;
                                if (bgwDownloader.CancellationPending)
                                    break;
                            }
                            if (bgwDownloader.CancellationPending)
                            {
                                Log.WriteDebug("Aborting connection - stop requested");
                                // 第10项:统一取消后禁止 chunk worker 复活(见 m_abortChunkRestart)
                                lock (Mutex)
                                {
                                    m_abortChunkRestart = true;
                                    foreach (DownloaderWorker w in listDownloaders)
                                    {
                                        if (w.IsBusy)
                                        {
                                            w.CancelAsync();
                                        }
                                    }
                                }
                                Log.WriteWarning("File download stopped - " + @file.Name);
                                break;
                            }

                            if (@file.GetDataPart.AllFinished)
                            {
                                break;
                            }

                            // P1-1:内存进度(每 ~50KB 推进一次)为主判据,落盘进度为兜底。
                            // 任一推进即重置计时 → 慢速但持续前进的下载不再被误杀。
                            long curProgress = GetMemoryProgress();
                            long curLandedProgress = @file.GetDataPart.GetTotalProgress();
                            if (curProgress != lastProgress || curLandedProgress != lastLandedProgress)
                            {
                                lastProgress = curProgress;
                                lastLandedProgress = curLandedProgress;
                                waitStartTime = DateTime.Now;
                            }

                            // Stall timeout without any progress — report failure but keep the
                            // chunk state so a retry resumes from the .part file.
                            // ⑤:配额熔断期 worker 不再重启,零速干等 120s+30s 才变红。
                            // 熔断期内阈值收紧到 10s,快速转红并标配额,由边沿唤醒恢复。
                            int idleLimitSeconds = MegaQuotaManager.IsQuarantined() ? 10 : 120;
                            if (DateTime.Now.Subtract(waitStartTime).TotalSeconds > idleLimitSeconds)
                            {
                                Log.WriteError("Download stalled with no progress for " + idleLimitSeconds + "s for " + @file.Name + "; aborting.");
                                timedOut = true;
                                // P1-5a: cancel the chunk workers as well. The task is judged failed
                                // here, but without this the workers kept retrying and writing into
                                // the .part file; a retry reuses the same FileDownloader instance, so
                                // two generations could write the same regions concurrently.
                                // 第10项:同时置 m_abortChunkRestart —— 旧代码只 CancelAsync 不改
                                // m_canceled,完成回调的 HasBeenCanceled 恒 False,worker 一比一复活。
                                lock (Mutex)
                                {
                                    m_abortChunkRestart = true;
                                    foreach (DownloaderWorker w in listDownloaders)
                                    {
                                        if (w.IsBusy)
                                        {
                                            w.CancelAsync();
                                        }
                                    }
                                }
                                break;
                            }
                        }
                        while (true);

                        // Wait until chunk workers finish before rename/timeout decision.
                        // B2:看门狗失败判定必须在排空后做——超时瞬间与撞线完成竞态时,
                        // 若已 AllFinished 则走正常重命名/MetaMAC 流程,不得再报 FileDownloadFailed
                        // (否则成功与失败双事件竞态,失败先到会把完好文件钉成 Erroneo)。
                        // ⑤:配额期 worker 已不再重启,排空通常瞬间结束;熔断期只等 5s,避免再叠 30s。
                        int drainSeconds = timedOut && MegaQuotaManager.IsQuarantined() ? 5 : 30;
                        var waitUntil = DateTime.Now.AddSeconds(drainSeconds);
                        while (DateTime.Now < waitUntil)
                        {
                            bool busy;
                            lock (Mutex)
                                busy = listDownloaders.Any(w => w.IsBusy);
                            if (!busy)
                                break;
                            System.Threading.Thread.Sleep(50);
                        }

                        // Report the timeout as a download failure. Chunk state is intentionally
                        // preserved for resumption. RC:配额熔断期内超时必须报配额异常(否则 FailedByQuota=False,
                        // 立即重试捞不回);非配额超时为无进度停滞(正常 120s,暂停不计,熔断期 10s;
                        // 有推进会自动顺延,不再是总时长上限)。
                        // B2:排空后已 AllFinished 则不报(交由下述重命名流程成功)。
                        if (timedOut && !bgwDownloader.CancellationPending && !@file.GetDataPart.AllFinished)
                        {
                            if (MegaQuotaManager.IsQuarantined())
                            {
                                bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, new MegaQuotaExceededException("MEGA transfer quota exceeded (EOVERQUOTA / HTTP 509). Download paused during quota quarantine; use Retry now after changing IP/proxy or wait for auto-resume. Progress preserved."));
                            }
                            else
                            {
                                bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, new ApplicationException("Download stalled with no progress (idle watchdog 120s; pause not counted; progress resets the timer). Progress preserved, retry resumes from the .part file."));
                            }
                        }
                        else if (timedOut && @file.GetDataPart.AllFinished)
                        {
                            Log.WriteWarning("Download watchdog timed out but all chunks finished during drain for " + @file.Name + "; proceeding to finalize instead of failing.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    exc = ex;
                }
                finally
                {
                    // If all chunks finished despite an earlier non-fatal error, the download itself succeeded.
                    // Do NOT report FileDownloadFailed — that would wrongly mark a completed file as error.
                    if (exc is not null && @file.Size > 0L && @file.GetDataPart.AllFinished && !bgwDownloader.CancellationPending)
                    {
                        Log.WriteWarning("Non-fatal error during download of " + @file.Name + " but all chunks finished. Suppressing failure: " + Log.SafeException(exc));
                        exc = null;
                    }
                    if (exc is not null)
                    {
                        Log.WriteError("Error trying to download file " + @file.Name + " - " + Log.SafeException(exc));
                        bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, exc);
                    }
                }
            }
            while (false);

            try
            {
                // file.Size=0(远端探活失败)时 GetDataPart 会抛 "Must specify size",
                // 掩盖上一步已通过 FileDownloadFailed 报告的真实错误(连接失败/404 等),必须前置短路
                if (@file.Size > 0L && @file.GetDataPart.AllFinished && !bgwDownloader.CancellationPending)
                {
                    lock (MutexFile)
                        // 终结前必须先关复用流：VerifyMegaMetaMac 以 FileShare.Read 打开 .part，
                        // 而常驻写句柄权限是 Write（读共享不包含写），不关则 open 必报
                        // sharing violation（v2.5.1 每次写完即关故无此问题；常驻流是 v2.5.2 引入的）。
                        // 关流自带 Flush(True)，落盘语义不变。
                        ClosePartStream();
                    if (!System.IO.File.Exists(FicheroPART))
                    {
                        throw new ApplicationException("Download reported finished but partial file is missing.");
                    }
                    var partInfo = new System.IO.FileInfo(FicheroPART);
                    if (partInfo.Length != @file.Size)
                    {
                        throw new ApplicationException("Download size mismatch before rename.");
                    }

                    // Integrity: MEGA MetaMAC. The schedule is computed exactly as MEGA
                    // clients do (ChunkedHash: 128 KiB * i for i = 1..8, then a fixed 1 MiB),
                    // and the final MAC is compared once over the whole file, exactly as the
                    // SDK does (generateMetaMac + macsmac). The .part file is preallocated
                    // so the size check above proves nothing about content — a mismatch
                    // means the bytes on disk genuinely differ from the uploaded file and
                    // MUST fail hard instead of delivering a damaged file as a success.
                    // 4-word public link keys have no embedded MetaMAC and skip
                    // verification inside VerifyMegaMetaMac.
                    string keyForMac = @file.FileKey;
                    if (!string.IsNullOrEmpty(keyForMac) && keyForMac.Contains("=###n="))
                    {
                        keyForMac = keyForMac.Substring(0, keyForMac.IndexOf("=###n="));
                    }
                    if (!string.IsNullOrEmpty(keyForMac))
                    {
                        Log.WriteInfo("Verifying MEGA MetaMAC for " + @file.Name);
                        if (!Criptografia.VerifyMegaMetaMac(FicheroPART, keyForMac))
                        {
                            throw new ApplicationException("MEGA MetaMAC verification failed: the downloaded data does not match the uploaded file (size was exact: " + partInfo.Length + " bytes). The partial file has been reset; the download will restart from the beginning.");
                        }
                    }

                    string finalName = "";
                    lock (MutexFile)
                    {
                        // 重命名前关闭复用流：Windows 下打开中的文件无法 Rename，
                        // 且关闭前已由每次 FlushToDisk 的 Flush(True) 保证落盘，语义不变。
                        ClosePartStream();
                        finalName = RenamePartToReal(FicheroPART, FicheroReal);
                    }
                    if (!string.IsNullOrEmpty(finalName))
                        FileRenamed?.Invoke(this, new FileRenamedEventArgs(finalName));
                    Log.WriteWarning("File downloaded successfully");
                    fireEventFromBgw(Event.FileDownloadSucceeded);
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Error finalizing download " + @file.Name + " - " + Log.SafeException(ex));
                bgwDownloader.ReportProgress((int)InvokeType.FileDownloadFailedRaiser, ex);

                // 修复：验证/重命名失败后必须重置 AllFinished 和 chunk 状态，
                // 否则下次启动会因 AllFinished=True 跳过下载直接进入验证，导致
                // "重设也无法下载" 的死循环。
                try
                {
                    @file.GetDataPart.AllFinished = false;
                    foreach (DataPart.Chunk c in @file.GetDataPart.ChunkList)
                    {
                        c.Index = 0L;
                        c.SyncedIndex = 0L;
                        c.Available = true;
                    }
                    Log.WriteWarning("Reset chunk state after finalization failure for " + @file.Name);
                }
                catch (Exception cleanupEx)
                {
                    Log.WriteWarning("Error during cleanup after finalization failure: " + Log.SafeException(cleanupEx));
                }
            }
        }

        internal static void EnsureDownloadSpace(string partialPath, long fileSize, long availableSpace)
        {
            long reserved = System.IO.File.Exists(partialPath) ? new System.IO.FileInfo(partialPath).Length : 0L;
            long additional = Math.Max(0L, fileSize - reserved);
            const long margin = 64L * 1024L * 1024L;
            if (additional > 0L && (availableSpace < additional || availableSpace - additional < margin))
            {
                throw new IOException("Insufficient disk space for download.");
            }
        }

        private bool TryReuseCompletedFile(string completedPath, FileInfo @file)
        {
            if (!System.IO.File.Exists(completedPath))
                return false;
            try
            {
                if (new System.IO.FileInfo(completedPath).Length != @file.Size)
                    return false;
                string key = @file.FileKey;
                if (string.IsNullOrEmpty(key))
                    return false;
                if (key.Contains("=###n="))
                    key = key.Substring(0, key.IndexOf("=###n="));
                // Reusing a final file requires a real embedded MetaMAC, not the
                // verifier's compatibility fallback for keys without one.
                if (Criptografia.base64urldecodeBytes(key).Length != 32)
                    return false;
                if (!Criptografia.VerifyMegaMetaMac(completedPath, key))
                    return false;
                if (bgwDownloader.CancellationPending)
                    return true;
                m_currentFileSize = @file.Size;
                lock (Mutex)
                {
                    m_currentFileProgress = @file.Size;
                    m_totalProgress = @file.Size;
                }
                m_completedFilePath = completedPath;
                Log.WriteInfo("Reusing verified completed file " + @file.Name);
                fireEventFromBgw(Event.FileDownloadSucceeded);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Could not reuse completed file " + @file.Name + ": " + Log.SafeException(ex));
                return false;
            }
        }

        /// <summary>
    /// B1-③:把 FicheroPART 重命名为成品(含 "file (i)" 冲突消解),供正常终结与空文件短路共用。
    /// 调用方必须已持有 Me.MutexFile(与原内联代码的加锁位置一致)。
    /// 返回终局文件名(非全路径,无事可做返回 "");调用方出锁后凭此 Raise FileRenamed。
    /// </summary>
        private string RenamePartToReal(string FicheroPART, string FicheroReal)
        {
            lock (FinalFileLock)
            {
                if (!System.IO.File.Exists(FicheroPART))
                    return "";
                string directory = Path.GetDirectoryName(FicheroReal);
                string leaf = Path.GetFileName(FicheroReal);
                int dot = leaf.LastIndexOf('.');
                string @extension = dot > 0 ? leaf.Substring(dot) : "";
                string baseName = dot > 0 ? leaf.Substring(0, dot) : leaf;
                string candidate = FicheroReal;
                int i = 1;
                do
                {
                    PathGuard.EnsurePathUnderRoot(LocalDirectory, candidate, allowRoot: false);
                    if (!System.IO.File.Exists(candidate) && !Directory.Exists(candidate))
                    {
                        Log.WriteInfo("Rename from " + FicheroPART + " to " + candidate);
                        System.IO.File.Move(FicheroPART, candidate);
                        m_completedFilePath = candidate;
                        return Path.GetFileName(candidate);
                    }
                    i += 1;
                    string suffix = i <= 9999 ? i.ToString() : Guid.NewGuid().ToString("N").Substring(0, 8);
                    // F4: cap the collision leaf at PathGuard's 240-char limit so the
                    // on-disk name round-trips through GetSafeFilePathUnderRoot unmangled.
                    string suffixPart = " (" + suffix + ")";
                    string shortBase = baseName;
                    if (shortBase.Length + suffixPart.Length + @extension.Length > 240)
                    {
                        int keep = 240 - suffixPart.Length - @extension.Length;
                        if (keep < 1)
                        {
                            // huge extension: drop it for a GUID leaf, as before
                            candidate = Path.Combine(directory, Guid.NewGuid().ToString("N").Substring(0, 8));
                        }
                        else
                        {
                            if (shortBase.Length > keep)
                                shortBase = shortBase.Substring(0, keep).TrimEnd(' ', '.');
                            if (string.IsNullOrEmpty(shortBase))
                                shortBase = "file";
                            candidate = Path.Combine(directory, shortBase + suffixPart + @extension);
                        }
                    }
                    else
                    {
                        candidate = Path.Combine(directory, baseName + suffixPart + @extension);
                    }
                }
                while (true);
            }
        }

        private void bwgDownloader_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            switch ((InvokeType)e.ProgressPercentage)
            {
                case InvokeType.EventRaiser:
                    switch ((Event)Conversions.ToInteger(e.UserState))
                    {
                        case Event.CalculationFileSizesStarted:
                            CalculationFileSizesStarted?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileSizesCalculationComplete:
                            FileSizesCalculationComplete?.Invoke(this, new EventArgs());
                            break;
                        case Event.FilesLocalCreated:
                            FileLocalCreated?.Invoke(this, new EventArgs());
                            break;
                        case Event.DeletingFilesAfterCancel:
                            DeletingFilesAfterCancel?.Invoke(this, new EventArgs());
                            break;

                        case Event.FileDownloadAttempting:
                            FileDownloadAttempting?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileDownloadStarted:
                            FileDownloadStarted?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileDownloadStopped:
                            FileDownloadStopped?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileDownloadSucceeded:
                            FileDownloadSucceeded?.Invoke(this, new EventArgs());
                            break;
                        case Event.ProgressChanged:
                            ProgressChanged?.Invoke(this, new EventArgs());
                            break;
                    }

                    break;
                case InvokeType.FileDownloadFailedRaiser:
                    FileDownloadFailed?.Invoke(this, (Exception)e.UserState);
                    break;
                case InvokeType.CalculatingFileNrRaiser:
                    CalculatingFileSize?.Invoke(this);
                    break;
                case InvokeType.StartDownloaderRaiser:
                    NewDownloaderWorker(Conversions.ToLong(e.UserState));
                    break;
            }
        }

        private void bgwDownloader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsPaused = false;
            bool canceled;
            lock (Mutex)
            {
                System.Threading.Volatile.Write(ref m_stopCompletionPending, true);
                System.Threading.Volatile.Write(ref m_busy, false);
                m_abortChunkRestart = true;
                canceled = m_canceled;
            }

            try
            {
                if (canceled)
                {
                    Canceled?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Completed?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref m_stopCompletionPending, false);
            }

            Stopped?.Invoke(this, new EventArgs());
            IsBusyChanged?.Invoke(this, new EventArgs());
            StateChanged?.Invoke(this, new EventArgs());
        }

        private void ChunkDownloader_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (((DownloaderWorker)sender).Generation != System.Threading.Interlocked.Read(ref m_chunkGeneration))
                return;
            switch ((InvokeType)e.ProgressPercentage)
            {
                case InvokeType.EventRaiser:
                    switch ((Event)Conversions.ToInteger(e.UserState))
                    {
                        case Event.CalculationFileSizesStarted:
                            CalculationFileSizesStarted?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileSizesCalculationComplete:
                            FileSizesCalculationComplete?.Invoke(this, new EventArgs());
                            break;
                        case Event.DeletingFilesAfterCancel:
                            DeletingFilesAfterCancel?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileDownloadAttempting:
                            FileDownloadAttempting?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileDownloadStarted:
                            FileDownloadStarted?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileDownloadStopped:
                            FileDownloadStopped?.Invoke(this, new EventArgs());
                            break;
                        case Event.FileDownloadSucceeded:
                            FileDownloadSucceeded?.Invoke(this, new EventArgs());
                            break;
                        case Event.ProgressChanged:
                            ProgressChanged?.Invoke(this, new EventArgs());
                            break;
                    }

                    break;
                case InvokeType.ChunkDownloadFailedRaiser:
                    ChunkDownloadFailed?.Invoke(this, (Exception)e.UserState);
                    break;
                case InvokeType.CalculatingFileNrRaiser:
                    CalculatingFileSize?.Invoke(this);
                    break;
            }
        }

        private void ChunkDownloader_DoWork(object sender, DoWorkEventArgs e)
        {
            // 第14项:失败事件防重入。报失败后 Exit Sub/Exit Try 退栈要经过两层 Finally 关流
            // (Stream.Close/webResp.Close),关闭抛出的异常会落进外层 Catch 二次发
            // ChunkDownloadFailedRaiser,Fichero.NumErroresChunk 双倍计数(30 次上限提前一倍命中)。
            // 该标记在每次上报前置位,外层 Catch 只对"尚未上报过"的裸区异常补报一次。
            // 注意:必须声明在最外层 Try 之前,否则 VB 块作用域导致 Catch 里看不到。
            bool failureReported = false;
            try
            {

                DownloaderWorker worker = (DownloaderWorker)sender;

                var @file = worker.File;

                string FicheroPART = GetPartialFilePath(LocalDirectory, @file.Name, @file.PartialFileName);

                string FileKey = @file.FileKey;

                HttpWebRequest webReq = null;
                HttpWebResponse webResp = null;

                var Chunk = @file.GetDataPart.ChunkList[worker.ChunkIndex];

                Log.WriteDebug("Starting chunk " + FicheroPART + " position " + (Chunk.StartIndex + Chunk.Index));

                Exception exc = null;

                long readings = 0L;
                int currentBuffersize = 0;
                int currentPackageSize = -1;
                var BufferMem = new byte[PackageSize];
                var BufferDisk = new byte[BufferSize];
                int bytesDownloadedSinceLastTimer = 0;
                var speedTimer = new Stopwatch();
                var arraySpeed = new KeyValuePair<double, double>[m_stopWatchCycles];
                var lastSpeedRefresh = DateTime.MinValue;

                long ChunkStart = Chunk.StartIndex + Chunk.Index;
                long ChunkStop = Chunk.StartIndex + Chunk.Size - 1L;
                do
                {
                    try
                    {
                        if (ChunkStart >= ChunkStop + 1L) // Si el indice del chunk es mayor al esperado, presuponemos que se ha descargado del todo y está todo correcto...
                        {
                            @file.GetDataPart.SetProgress(worker.ChunkIndex, Chunk.Size);
                            break;
                        }

                        if (string.IsNullOrEmpty(FileKey))
                        {
                            exc = new ApplicationException("FileKey not defined");
                            worker.ChunkDownloadFailed = true;
                            Log.WriteError("Error: FileKey not defined");
                            failureReported = true;
                            worker.ReportProgress((int)InvokeType.ChunkDownloadFailedRaiser, exc);
                            break;
                        }


                        try
                        {
                            webReq = Conexion.CreateHttpWebRequest(@file.Path);
                            // webReq.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.8.0.4) Gecko/20060508 Firefox/1.5.0.4" ' http://stackoverflow.com/questions/6305292/cant-get-html-code-through-httpwebrequest


                            Log.WriteInfo("Starting connection - " + @file.Name + " from byte " + ChunkStart + " to " + ChunkStop);

                            // Resume-offset alignment guard: the CTR keystream is seeked in whole
                            // 16-byte blocks, so a resume from a non-aligned offset would decrypt
                            // every following block with the wrong keystream (silent corruption).
                            // Fail the chunk instead; the aligned progress is kept for the retry.
                            // (Reaching the tail of the chunk is exempt: no data follows.)
                            if ((ChunkStart & 15L) != 0L && ChunkStart < Chunk.StartIndex + Chunk.Size)
                            {
                                throw new ApplicationException(string.Format("Resume offset {0} for {1} is not block-aligned; aborting chunk to avoid keystream misalignment.", ChunkStart, @file.Name));
                            }

                            // Pequeño hack para soportar ficheros de más de 2 GB:
                            // http://forums.codeguru.com/showthread.php?467570-WebRequest.AddRange-what-about-files-gt-2gb&p=1794639
                            // http://www.freesoft.org/CIE/RFC/2068/198.htm
                            string key = "Range";
                            string val = string.Format("bytes={0}-{1}", ChunkStart, ChunkStop);

                            var @method = typeof(WebHeaderCollection).GetMethod("AddWithoutValidate", BindingFlags.Instance | BindingFlags.NonPublic);
                            @method.Invoke(webReq.Headers, new object[] { key, val });

                            webResp = (HttpWebResponse)webReq.GetResponse();

                            // Require Partial Content for ranged requests
                            if (webResp.StatusCode != HttpStatusCode.PartialContent && webResp.StatusCode != HttpStatusCode.OK)
                            {
                                if (MegaQuotaManager.IsQuotaStatusCode(webResp.StatusCode))
                                {
                                    MegaQuotaManager.ReportQuota();
                                    exc = new MegaQuotaExceededException("MEGA transfer quota exceeded (HTTP 509) during download.");
                                }
                                else
                                {
                                    exc = new ApplicationException("Unexpected HTTP status for ranged download: " + ((int)webResp.StatusCode).ToString());
                                }
                            }
                            else
                            {
                                string contentRangeHeader = webResp.Headers["Content-Range"];
                                // Content-Range: bytes 1769472-2147111/2147112
                                if (webResp.StatusCode == HttpStatusCode.OK && string.IsNullOrEmpty(contentRangeHeader))
                                {
                                    // Server ignored Range and returned full body
                                    if (ChunkStart != 0L)
                                    {
                                        exc = new ApplicationException("Server ignored Range request (HTTP 200 without Content-Range).");
                                    }
                                }
                                else if (string.IsNullOrEmpty(contentRangeHeader) || !contentRangeHeader.StartsWith("bytes "))
                                {
                                    if (ChunkStart != 0L)
                                    {
                                        exc = new ApplicationException("Missing Content-Range for partial download.");
                                    }
                                }
                                else
                                {
                                    string rangeBody = contentRangeHeader.Substring(6).Trim();
                                    int slashIdx = rangeBody.IndexOf('/');
                                    string rangePart = slashIdx >= 0 ? rangeBody.Substring(0, slashIdx) : rangeBody;
                                    string totalPart = slashIdx >= 0 ? rangeBody.Substring(slashIdx + 1) : "";
                                    int dashIdx = rangePart.IndexOf('-');
                                    if (dashIdx < 0)
                                    {
                                        exc = new ApplicationException("Invalid Content-Range: " + contentRangeHeader);
                                    }
                                    else
                                    {
                                        string tokenInit = rangePart.Substring(0, dashIdx);
                                        string tokenEnd = rangePart.Substring(dashIdx + 1);
                                        long InitDescarga;
                                        long EndDescarga;
                                        if (!long.TryParse(tokenInit, out InitDescarga) || !long.TryParse(tokenEnd, out EndDescarga))
                                        {
                                            exc = new ApplicationException("Invalid Content-Range numbers: " + contentRangeHeader);
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(totalPart) && totalPart != "*")
                                            {
                                                long totalLen;
                                                if (long.TryParse(totalPart, out totalLen) && @file.Size > 0L && totalLen != @file.Size)
                                                {
                                                    exc = new ApplicationException(string.Format("Content-Range total length mismatch: {0} vs {1}", totalLen, @file.Size));
                                                }
                                            }
                                            if (exc is null && InitDescarga != ChunkStart)
                                            {
                                                if (InitDescarga < Chunk.StartIndex || InitDescarga > Chunk.StartIndex + Chunk.Index)
                                                {
                                                    exc = new ApplicationException(string.Format("Error when downloading chunk {0}-{1}: received start {2}", ChunkStart, ChunkStop, InitDescarga));
                                                }
                                                else if (InitDescarga != Chunk.StartIndex + Chunk.Index)
                                                {
                                                    // Align resume to what the server actually sent (must stay inside chunk)
                                                    Chunk.Index = InitDescarga - Chunk.StartIndex;
                                                    ChunkStart = Chunk.StartIndex + Chunk.Index;
                                                    ChunkStop = Chunk.StartIndex + Chunk.Size - 1L;
                                                }
                                            }
                                            if (exc is null && EndDescarga < ChunkStart)
                                            {
                                                exc = new ApplicationException("Content-Range end is before requested start.");
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        catch (WebException ex)
                        {
                            ReportQuotaIfMatch(ex);
                            exc = ex;
                        }
                        catch (Exception ex)
                        {
                            ReportQuotaIfMatch(ex);
                            exc = ex;
                        }


                        if (exc is not null)
                        {
                            worker.ChunkDownloadFailed = true;
                            Log.WriteError("Connection error when downloading file " + @file.Name + " - " + Log.SafeException(exc));
                            failureReported = true;
                            worker.ReportProgress((int)InvokeType.ChunkDownloadFailedRaiser, exc);
                            break;
                        }


                        string FileKeyWithoutN = FileKey;
                        if (FileKeyWithoutN.Contains("=###n="))
                        {
                            FileKeyWithoutN = FileKeyWithoutN.Substring(0, FileKey.IndexOf("=###n="));
                        }

                        // Inicializamos el Cipher (floor seek by absolute file offset)
                        Log.WriteDebug("Starting SicBlockCipher seek position " + ChunkStart);
                        var crono = DateTime.Now;
                        var oCipher = Criptografia.GetInstaceCipher(FileKeyWithoutN);
                        oCipher.SeekToFileOffset(ChunkStart);
                        Log.WriteDebug("Finishing SicBlockCipher seek [" + DateTime.Now.Subtract(crono).TotalMilliseconds + "ms]");

                        if (m_currentFileProgress > 0L)
                        {
                            fireEventFromDownloader(worker, Event.ProgressChanged);
                        }

                        var Stream = webResp.GetResponseStream();
                        var TStream = new ThrottledStream(Stream);
                        ThrottledStreamController.GetController().AddStream(ref TStream, @file.FileID);
                        speedTimer.Start();
                        try
                        {
                            while (Chunk.Index < Chunk.Size | currentBuffersize > 0)
                            {

                                if (worker.CancellationPending)
                                {
                                    if (currentBuffersize > 0)
                                    {
                                        FlushToDisk(worker, FicheroPART, ref BufferDisk, ref currentBuffersize, ref Chunk, true);
                                    }
                                    speedTimer.Stop();
                                    return;
                                }

                                // Not trigger.WaitOne(0) -> IsPaused? -> Flush to disk
                                if (currentBuffersize > 0 & (Chunk.Index + currentBuffersize >= Chunk.Size | !trigger.WaitOne(0) | worker.CancellationPending | currentBuffersize + PackageSize > BufferSize))



                                {

                                    bool @continue = FlushToDisk(worker, FicheroPART, ref BufferDisk, ref currentBuffersize, ref Chunk);
                                    if (!@continue)
                                    {
                                        break;
                                    }

                                }

                                trigger.WaitOne();
                                if (worker.CancellationPending)
                                {
                                    if (currentBuffersize > 0)
                                    {
                                        FlushToDisk(worker, FicheroPART, ref BufferDisk, ref currentBuffersize, ref Chunk, true);
                                    }
                                    Log.WriteDebug("Download stopped - " + @file.Name);
                                    speedTimer.Stop();
                                    return;
                                }

                                try
                                {

                                    // Fill the packageSize
                                    currentPackageSize = 0;
                                    bool unexpectedEof = false;

                                    while (currentPackageSize < PackageSize)
                                    {
                                        int bytesDownloaded = TStream.Read(BufferMem, currentPackageSize, PackageSize - currentPackageSize);
                                        currentPackageSize += bytesDownloaded;

                                        if (bytesDownloaded == 0)
                                        {
                                            if (Chunk.Index + currentBuffersize + currentPackageSize < Chunk.Size)
                                            {
                                                unexpectedEof = true;
                                            }
                                            break;
                                        }
                                        if (Chunk.Index + currentBuffersize + currentPackageSize >= Chunk.Size)
                                        {
                                            break;
                                        }
                                    }

                                    if (unexpectedEof)
                                    {
                                        throw new ApplicationException("UnexpectedEndOfStream: connection closed before chunk completed.");
                                    }

                                    if (currentPackageSize == 0)
                                    {
                                        if (Chunk.Index + currentBuffersize >= Chunk.Size)
                                        {
                                            break;
                                        }
                                        throw new ApplicationException("UnexpectedEndOfStream: zero-byte read with incomplete chunk.");
                                    }

                                    for (int tempIndex = 0, loopTo = currentPackageSize - 1; oCipher.GetBlockSize() >= 0 ? tempIndex <= loopTo : tempIndex >= loopTo; tempIndex += oCipher.GetBlockSize())
                                        oCipher.ProcessBlock(BufferMem, tempIndex, BufferDisk, currentBuffersize + tempIndex);
                                }

                                catch (WebException ex)
                                {
                                    ReportQuotaIfMatch(ex);
                                    exc = ex;
                                }
                                catch (Exception ex)
                                {
                                    ReportQuotaIfMatch(ex);
                                    exc = ex;
                                }

                                if (exc is not null)
                                {
                                    if (currentBuffersize > 0)
                                    {
                                        try
                                        {
                                            FlushToDisk(worker, FicheroPART, ref BufferDisk, ref currentBuffersize, ref Chunk, true);
                                        }
                                        catch (Exception flushEx)
                                        {
                                            Log.WriteError("ChunkDownloader_DoWork: best-effort FlushToDisk before failure reporting failed: " + flushEx.ToString());
                                        }
                                    }
                                    worker.ChunkDownloadFailed = true;
                                    failureReported = true;
                                    worker.ReportProgress((int)InvokeType.ChunkDownloadFailedRaiser, exc);
                                    speedTimer.Stop();
                                    return;
                                }

                                lock (Mutex)
                                {
                                    m_currentFileProgress += currentPackageSize;
                                    m_totalProgress += currentPackageSize;
                                }

                                bytesDownloadedSinceLastTimer += currentPackageSize;

                                // P0-2:热路径 ProgressChanged 已确认零订阅者（全仓仅 FileDownloader
                                // 内部 RaiseEvent，无外部 AddHandler/Handles），此处不再经
                                // BackgroundWorker 编组到 UI 线程。chunk 启动时的低频触发
                                // （:1311 fireEventFromDownloader ProgressChanged）予以保留。
                                currentBuffersize += currentPackageSize;

                                if (lastSpeedRefresh.AddMilliseconds(175d) < DateTime.Now) // Refrescamos cada 175ms
                                {
                                    lastSpeedRefresh = DateTime.Now;
                                    readings += 1L;

                                    speedTimer.Stop();
                                    long ticks = speedTimer.ElapsedTicks;
                                    if (ticks == 0L)
                                        ticks = 1L;
                                    double time = ticks / (double)Stopwatch.Frequency;
                                    speedTimer.Reset();
                                    speedTimer.Start();

                                    int indexArraySpeed = (int)(readings % StopWatchCyclesAmount);
                                    arraySpeed[indexArraySpeed] = new KeyValuePair<double, double>(bytesDownloadedSinceLastTimer, time);
                                    bytesDownloadedSinceLastTimer = 0;

                                    string key = worker.ChunkIndex.ToString();
                                    int avgSpeed = CalculateAvgSpeed(readings, StopWatchCyclesAmount, arraySpeed);
                                    lock (Mutex)
                                        m_currentSpeed[key] = avgSpeed;

                                }
                            }
                        }
                        finally
                        {
                            speedTimer.Stop();
                            ThrottledStreamController.GetController().RemoveStream(ref TStream);
                            // 第14项:退栈路径(含报失败后的 Exit Sub)会经此处关流,关闭异常绝不能抛出,
                            // 否则落进外层 Catch 二次发 ChunkDownloadFailedRaiser,错误数双倍计。
                            try
                            {
                                Stream.Close();
                            }
                            catch (Exception closeEx)
                            {
                                Log.WriteWarning("ChunkDownloader_DoWork: part stream close failed: " + Log.SafeException(closeEx));
                            }
                        }



                        Log.WriteInfo("Finishing connection - " + @file.Name + " - chunk downloaded");
                    }
                    finally
                    {
                        // 同上:webResp 关闭异常也不得抛出(会二次发失败事件)。
                        if (webResp is not null)
                        {
                            try
                            {
                                webResp.Close();
                            }
                            catch (Exception respCloseEx)
                            {
                                Log.WriteWarning("ChunkDownloader_DoWork: web response close failed: " + Log.SafeException(respCloseEx));
                            }
                        }
                    }
                }
                while (false);

                fireEventFromDownloader(worker, Event.FileDownloadStopped);
            }
            catch (Exception ex)
            {
                // P1-5b: exceptions from the "bare zone" between the connection Try and the
                // download loop (cipher init/seek, GetResponseStream, ThrottledStream setup)
                // used to be logged only. ChunkDownloadFailed stayed False, so
                // RunWorkerCompleted restarted the worker IMMEDIATELY without backoff —
                // a persistent failure (bad key, DNS down) became a tight restart loop with
                // no error surfaced to the user. Mark the chunk failed and report it so the
                // exponential backoff and the error counting/UI path both apply.
                // NOTE: locals declared inside the Try block are NOT in scope here in VB,
                // so re-obtain the worker from sender.
                Log.WriteError("Error in Downloader.DoWork: " + ex.ToString());
                DownloaderWorker failedWorker = sender as DownloaderWorker;
                if (failedWorker is not null)
                {
                    failedWorker.ChunkDownloadFailed = true;
                    // 第14项:已上报过的失败不再二次发事件(退栈关流异常等),防 NumErroresChunk 双倍计。
                    if (!failureReported)
                    {
                        failureReported = true;
                        try
                        {
                            failedWorker.ReportProgress((int)InvokeType.ChunkDownloadFailedRaiser, ex);
                        }
                        catch
                        {
                            // Reporting must never throw from an error path (worker may be disposing)
                        }
                    }
                }
            }
        }

        public bool FlushToDisk(object worker, string filePath, ref byte[] BufferDisk, ref int CurrentBufferSize, ref DataPart.Chunk Chunk, bool forceSync = false)


        {

            // ''''''''''''''''''''''''
            // Dim BufferDisk2(Me.BufferSize - 1) As Byte
            // For tempIndex As Integer = 0 To CurrentBufferSize - 1 Step oCipher.GetBlockSize
            // oCipher.ProcessBlock(BufferDisk, tempIndex, BufferDisk2, tempIndex)
            // 'Buffer.BlockCopy(BufferDisk, tempIndex, BufferDisk2, tempIndex, oCipher.GetBlockSize)
            // Next

            // ''''''''''''''''''''''


            // Block alignment guard. The CTR keystream can only be seeked in whole
            // 16-byte blocks: if a failed/interrupted download leaves Chunk.Index at a
            // non-aligned offset, the retry would resume decryption with the keystream
            // of the wrong block and corrupt EVERYTHING written after that point
            // (verified root cause of "size correct but file damaged" downloads).
            // Unless this write reaches the end of the chunk (no further data follows),
            // round the persisted progress down to a 16-byte boundary. The dropped
            // tail (< 16 bytes of already-decrypted data) is simply re-fetched on retry.
            if (Chunk.Index + CurrentBufferSize < Chunk.Size && (CurrentBufferSize & 15) != 0)
            {
                int originalSize = CurrentBufferSize;
                int roundedSize = CurrentBufferSize & ~15;
                Log.WriteWarning("FlushToDisk: rounding buffered bytes down to a 16-byte boundary (" + originalSize + " -> " + roundedSize + ") to keep the resume offset block-aligned");
                int discarded = originalSize - roundedSize;
                if (discarded > 0)
                {
                    lock (Mutex)
                    {
                        m_currentFileProgress -= discarded;
                        if (m_currentFileProgress < 0L)
                            m_currentFileProgress = 0L;
                        m_totalProgress -= discarded;
                        if (m_totalProgress < 0L)
                            m_totalProgress = 0L;
                    }
                }
                CurrentBufferSize = roundedSize;
                if (CurrentBufferSize == 0)
                    return Chunk.Index < Chunk.Size;
            }

            long IndexProgress = Chunk.Index + CurrentBufferSize;
            if (IndexProgress >= Chunk.Size)
            {
                IndexProgress = Chunk.Size;
            }
            bool isFinal = IndexProgress >= Chunk.Size;
            // 阈值判断在锁外读 SyncedIndex/m_lastFsyncUtc：仅决定本次是否 fsync，
            // 误判只造成多/少一次 fsync，不变量由锁内更新 + XML 钳制保证。
            bool syncNow = forceSync || isFinal || IndexProgress - Chunk.SyncedIndex >= FsyncEveryBytes || DateTime.UtcNow.Subtract(m_lastFsyncUtc).TotalSeconds >= FsyncEverySeconds;


            lock (MutexFile)
            {
                var writer = GetPartStream(filePath);
                writer.Position = Chunk.StartIndex + Chunk.Index;
                writer.Write(BufferDisk, 0, CurrentBufferSize);
                // 方案B：仅节流点 Flush(True)。Index 照常推进供进程内使用；
                // 断电安全由 SyncedIndex 记账 + XML 钳制保证（红线）。
                if (syncNow)
                {
                    writer.Flush(true);
                    m_lastFsyncUtc = DateTime.UtcNow;
                }
            }

            DownloaderWorker dWorker = (DownloaderWorker)worker;

            File.GetDataPart.SetProgress(dWorker.ChunkIndex, IndexProgress);
            Chunk = File.GetDataPart.ChunkList[dWorker.ChunkIndex];
            if (syncNow)
                Chunk.SyncedIndex = IndexProgress;

            CurrentBufferSize = 0;

            return Chunk.Index < Chunk.Size;
        }

        /// <summary>
    /// P0-1 方案A：获取 .part 复用写流。调用方必须已持有 MutexFile。
    /// 路径切换（新文件/重建后）时自动关闭旧流重开；只 open 不 close。
    /// </summary>
        private FileStream GetPartStream(string filePath)
        {
            if (m_partStream is not null)
            {
                if (string.Equals(m_partStreamPath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return m_partStream;
                }
                ClosePartStream();
            }
            m_partStream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 64 * 1024, FileOptions.RandomAccess);
            m_partStreamPath = filePath;
            return m_partStream;
        }

        /// <summary>
    /// 关闭复用写流（幂等）。调用方必须已持有 MutexFile；重命名/删除 .part 前必须先调。
    /// 流的 Flush(True) 由每次 FlushToDisk 保证，此处只做 Flush + Close，不改变持久化语义。
    /// </summary>
        private void ClosePartStream()
        {
            if (m_partStream is not null)
            {
                try
                {
                    m_partStream.Flush(true);
                }
                catch
                {
                }
                try
                {
                    m_partStream.Close();
                }
                catch
                {
                }
                try
                {
                    m_partStream.Dispose();
                }
                catch
                {
                }
                m_partStream = null;
                m_partStreamPath = null;
            }
        }

        private async void ChunkDownloader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DownloaderWorker worker = (DownloaderWorker)sender;
            int delayMs = 0;
            try
            {
                lock (Mutex)
                {
                    bool wasTracked = listDownloaders.Remove(worker);
                    worker.DoWork -= ChunkDownloader_DoWork;
                    worker.ProgressChanged -= ChunkDownloader_ProgressChanged;
                    worker.RunWorkerCompleted -= ChunkDownloader_RunWorkerCompleted;
                    worker.Dispose();
                    if (!wasTracked)
                        return;

                    if (!CanStartChunk(worker.Generation))
                        return;
                    m_currentSpeed.Remove(worker.ChunkIndex.ToString());

                    if (worker.ChunkDownloadFailed)
                    {
                        if (MegaQuotaManager.IsQuarantined())
                            return;
                        m_consecutiveChunkFailures = Math.Min(5, m_consecutiveChunkFailures + 1);
                        delayMs = (1000 << m_consecutiveChunkFailures - 1) + DateTime.Now.Millisecond % 500;
                    }
                    else
                    {
                        m_consecutiveChunkFailures = 0;
                    }
                }
                if (delayMs > 0)
                    await Task.Delay(delayMs).ConfigureAwait(false);
                NewDownloaderWorker(worker.Generation, worker.ChunkIndex);
            }
            catch (Exception ex)
            {
                Log.WriteError("Could not restart a download chunk: " + Log.SafeException(ex));
            }
        }

        private void fireEventFromBgw(Event eventName)
        {
            bgwDownloader.ReportProgress((int)InvokeType.EventRaiser, eventName);
        }

        private void fireEventFromDownloader(DownloaderWorker d, Event eventName)
        {
            d.ReportProgress((int)InvokeType.EventRaiser, eventName);
        }


        private static int CalculateAvgSpeed(long readings, int StopWatchCyclesAmount, KeyValuePair<double, double>[] arraySpeed)
        {
            double TotalDescargado = 0d;
            double TiempoDescarga = 0d;
            int index = StopWatchCyclesAmount - 1;

            // Al menos 5 ciclos, para evitar picos al inicio
            if (readings < 5L & StopWatchCyclesAmount > 5)
            {
                index = -1;
            }
            else if (readings < StopWatchCyclesAmount)
            {
                index = (int)readings; // Solo la media de los elementos que hemos metido (el resto es basura)
            }

            for (int i = 0, loopTo = index; i <= loopTo; i++)
            {
                var item = arraySpeed[i];
                TotalDescargado += item.Key;
                TiempoDescarga += item.Value;
            }
            if (TiempoDescarga == 0d)
                TiempoDescarga = 1d;
            return (int)Math.Round(TotalDescargado / TiempoDescarga);
        }

        private bool CanStartChunk(long generation)
        {
            return m_busy && !m_disposed && !m_canceled && !m_abortChunkRestart && generation == System.Threading.Interlocked.Read(ref m_chunkGeneration);
        }

        private void NewDownloaderWorker(long generation, int retryIndex = -1)
        {
            lock (Mutex)
            {
                if (!CanStartChunk(generation))
                    return;
                if (retryIndex >= 0)
                {
                    var previous = File.GetDataPart.ChunkList[retryIndex];
                    if (previous.Index < previous.Size)
                        previous.Available = true;
                }
                var part = File.GetDataPart.NextAvailablePartIndex;
                if (!part.HasValue)
                    return;
                var worker = new DownloaderWorker(part.Value, File, generation) { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
                worker.DoWork += ChunkDownloader_DoWork;
                worker.ProgressChanged += ChunkDownloader_ProgressChanged;
                worker.RunWorkerCompleted += ChunkDownloader_RunWorkerCompleted;
                listDownloaders.Add(worker);
                worker.RunWorkerAsync();
            }
        }

        private void cleanUpFile()
        {
            if (File is not null)
            {
                string fullPath = GetPartialFilePath(LocalDirectory, File.Name, File.PartialFileName);
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
                if (DeleteCompletedFilesAfterCancel && !string.IsNullOrEmpty(m_completedFilePath))
                {
                    PathGuard.EnsurePathUnderRoot(LocalDirectory, m_completedFilePath, allowRoot: false);
                    if (System.IO.File.Exists(m_completedFilePath))
                        System.IO.File.Delete(m_completedFilePath);
                }
            }
        }

        private void calculateFilesSize()
        {
            fireEventFromBgw(Event.CalculationFileSizesStarted);


            bgwDownloader.ReportProgress((int)InvokeType.CalculatingFileNrRaiser, null);
            try
            {
                long size = ProbeRemoteFileSize(File.Path);
                if (size < 0L)
                    throw new ApplicationException("Could not determine remote file size.");
                m_totalSize = size;
            }
            catch (Exception ex)
            {
                ReportQuotaIfMatch(ex);
                throw new ApplicationException("Connection error: " + ex.Message);
            }

            fireEventFromBgw(Event.FileSizesCalculationComplete);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (Mutex)
            {
                if (m_disposed)
                    return;
                m_disposed = true;
                m_abortChunkRestart = true;
                System.Threading.Interlocked.Increment(ref m_chunkGeneration);
            }
            if (disposing)
            {
                try
                {
                    trigger.Set();
                    if (bgwDownloader is not null && bgwDownloader.IsBusy)
                    {
                        bgwDownloader.CancelAsync();
                    }
                    lock (Mutex)
                    {
                        foreach (DownloaderWorker w in listDownloaders.ToList())
                        {
                            if (w.IsBusy)
                                w.CancelAsync();
                        }
                    }
                    var waitUntil = DateTime.Now.AddSeconds(5d);
                    while (DateTime.Now < waitUntil)
                    {
                        bool busy;
                        lock (Mutex)
                            busy = listDownloaders.Any(x => x.IsBusy);
                        if (!busy)
                            break;
                        System.Threading.Thread.Sleep(50);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteError("FileDownloader.Dispose: error while cancelling workers: " + ex.ToString());
                }
                // Dispose every worker we spawned, not just the outer bgwDownloader.
                // Workers that were still running when CancelAsync was called will not
                // have hit their RunWorkerCompleted handler, so they must be disposed here.
                lock (Mutex)
                {
                    foreach (DownloaderWorker w in listDownloaders.ToList())
                    {
                        try
                        {
                            w.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("FileDownloader.Dispose: worker dispose failed: " + ex.ToString());
                        }
                    }
                    listDownloaders.Clear();
                }
                if (bgwDownloader is not null)
                {
                    // 等主 worker 退出后再做收尾,避免 DoWork 仍在使用共享状态时被 Dispose 干扰
                    var waitMain = DateTime.Now.AddSeconds(10d);
                    while (bgwDownloader.IsBusy && DateTime.Now < waitMain)
                        System.Threading.Thread.Sleep(50);
                    bgwDownloader.Dispose();
                }

                // 确定性释放本实例的内核对象。SyncLock 锁对象无需释放；
                // trigger（ManualResetEvent）仍需关闭。复用写流先关，避免 .part 句柄泄漏
                // 挡住后续删除/重建。
                try
                {
                    lock (MutexFile)
                        ClosePartStream();
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("FileDownloader.Dispose: part stream close failed: " + ex.ToString());
                }
                try
                {
                    if (trigger is not null)
                    {
                        trigger.Set(); // 唤醒可能还在 Pause 等待的路径,让其尽快退出
                        trigger.Close();
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("FileDownloader.Dispose: trigger close failed: " + ex.ToString());
                }
                // SyncLock 锁对象无需释放（此前 Mutex 每个下载任务泄漏 3 个内核句柄，
                // 改轻量锁后泄漏源头消失）。trigger 是 ManualResetEvent，仍需关闭。
                // 复用写流已在上一步关闭（MutexFile 保护下）。
            }
            m_file = null;
            System.Threading.Volatile.Write(ref m_busy, false);
        }
        #endregion

        #region Properties
        internal string CompletedFilePath
        {
            get
            {
                return m_completedFilePath;
            }
        }

        /// <summary>Gets or sets the list of files to download</summary>
        public FileInfo File
        {
            get
            {
                return m_file;
            }
            set
            {
                if (IsBusy)
                {
                    throw new InvalidOperationException("You can not change the file during the download");
                }
                else if (!ReferenceEquals(m_file, value))
                    m_file = value;
            }
        }

        /// <summary>Gets or sets the local directory in which files will be stored</summary>
        public string LocalDirectory
        {
            get
            {
                return m_localDirectory;
            }
            set
            {
                string value2 = value;
                foreach (char c in Path.GetInvalidPathChars())
                    value2 = value2.Replace(c, ' ');

                if ((value2 ?? "") != (LocalDirectory ?? ""))
                {
                    m_localDirectory = value2;
                }
            }
        }

        /// <summary>Gets or sets if the FileDownloader should support total progress statistics. Note that when enabled, the FileDownloader will have to get the size of each file before starting to download them, which can delay the operation.</summary>
        public bool SupportsProgress
        {
            get
            {
                return m_supportsProgress;
            }
            set
            {
                if (IsBusy)
                {
                    throw new InvalidOperationException("You can not change the SupportsProgress property during the download");
                }
                else
                {
                    m_supportsProgress = value;
                }
            }
        }

        /// <summary>Gets or sets if when the download process is cancelled the complete downloads should be deleted</summary>
        public bool DeleteCompletedFilesAfterCancel
        {
            get
            {
                return m_deleteCompletedFiles;
            }
            set
            {
                m_deleteCompletedFiles = value;
            }
        }

        /// <summary>Gets or sets if when the download process is cancelled the downloads should be deleted</summary>
        public bool DeleteFilesAfterCancel
        {
            get
            {
                return m_deleteFiles;
            }
            set
            {
                m_deleteFiles = value;
            }
        }

        public int OpenConnections
        {
            get
            {
                lock (Mutex)
                    return listDownloaders.Count;
            }
        }

        public int NumConnections
        {
            get
            {
                return m_num_connections;
            }
            set
            {
                if (value > 0)
                {
                    m_num_connections = value;
                }
                else
                {
                    throw new InvalidOperationException("The NumConnections needs to be greater than 0");
                }
            }
        }

        public int PartsPerFile
        {
            get
            {
                return m_parts_per_file;
            }
            set
            {
                if (value > 0)
                {
                    m_parts_per_file = value;
                }
                else
                {
                    throw new InvalidOperationException("The PartsPerFile needs to be greater than 0");
                }
            }
        }

        public int BufferSize
        {
            get
            {
                return m_bufferSize;
            }
            set
            {
                if (value < PackageSize)
                {
                    throw new InvalidOperationException("The BufferSize needs to be greater than the PackageSize");
                }
                else if (value > 0)
                {
                    m_bufferSize = value;
                }
                else
                {
                    throw new InvalidOperationException("The BufferSize needs to be greater than 0");
                }
            }
        }

        /// <summary>Gets or sets the size of the blocks that will be downloaded</summary>
        public int PackageSize
        {
            get
            {
                return m_packageSize;
            }
            set
            {
                if (value > BufferSize)
                {
                    throw new InvalidOperationException("The BufferSize needs to be greater than the PackageSize");
                }
                else if (value > 0)
                {
                    m_packageSize = value;
                }
                else
                {
                    throw new InvalidOperationException("The PackageSize needs to be greater than 0");
                }
            }
        }

        public void setBufferAndPackageSize(int bufferSize, int packageSize)
        {
            const int MaxPackage = 1024 * 1024; // 1 MiB
            const int MaxBuffer = 16 * 1024 * 1024; // 16 MiB
            if (bufferSize <= 0)
            {
                bufferSize = 750 * 1024;
                Log.WriteWarning("Warning: BufferSize is 0 or less, setting default value 750KB");
            }
            if (packageSize <= 0)
            {
                packageSize = 50 * 1024;
                Log.WriteWarning("Warning: PackageSize is 0 or less, setting default value 50KB");
            }
            if (packageSize > MaxPackage)
                packageSize = MaxPackage;
            if (bufferSize > MaxBuffer)
                bufferSize = MaxBuffer;
            if (bufferSize < packageSize)
            {
                Log.WriteWarning("Warning: BufferSize needs to be greater than PackageSize, setting 5x");
                bufferSize = Math.Min(MaxBuffer, 5 * packageSize);
            }
            m_packageSize = packageSize;
            m_bufferSize = bufferSize;
        }


        /// <summary>Gets or sets the amount of blocks that need to be downloaded before the progress speed is re-calculated. Note: setting this to a low value might decrease the accuracy</summary>
        public int StopWatchCyclesAmount
        {
            get
            {
                return m_stopWatchCycles;
            }
            set
            {
                if (value > 0)
                {
                    m_stopWatchCycles = value;
                }
                else
                {
                    throw new InvalidOperationException("The StopWatchCyclesAmount needs to be greather then 0");
                }
            }
        }

        /// <summary>Gets or sets the busy state of the FileDownloader</summary>
        public bool IsBusy
        {
            get
            {
                return System.Threading.Volatile.Read(ref m_busy);
            }
            set
            {
                lock (Mutex)
                {
                    if (m_busy == value || m_disposed)
                        return;
                    if (value && (bgwDownloader.IsBusy || m_stopCompletionPending || listDownloaders.Any(w => w.IsBusy)))
                        return;
                    System.Threading.Interlocked.Increment(ref m_chunkGeneration);
                    m_canceled = !value;
                    System.Threading.Volatile.Write(ref m_busy, value);
                    if (value)
                    {
                        m_currentFileProgress = 0L;
                        m_totalProgress = 0L;
                        m_abortChunkRestart = false;
                        m_completedFilePath = null;
                        m_consecutiveChunkFailures = 0;
                        m_currentSpeed.Clear();
                        if (File is not null && File.Size > 0L && File.DataPartInitialized)
                            File.GetDataPart.ResetAvailableParts();
                        bgwDownloader.RunWorkerAsync();
                    }
                    else
                    {
                        System.Threading.Volatile.Write(ref m_stopCompletionPending, true);
                        bgwDownloader.CancelAsync();
                        foreach (DownloaderWorker worker in listDownloaders)
                        {
                            if (worker.IsBusy)
                                worker.CancelAsync();
                        }
                        trigger.Set();
                        m_paused = false;
                    }
                }

                if (value)
                {
                    Started?.Invoke(this, EventArgs.Empty);
                    IsBusyChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    CancelRequested?.Invoke(this, EventArgs.Empty);
                }
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the pause state of the FileDownloader</summary>
        public bool IsPaused
        {
            get
            {
                return m_paused;
            }
            set
            {
                if (IsBusy)
                {
                    if (value != IsPaused)
                    {
                        m_paused = value;
                        if (IsPaused)
                        {
                            trigger.Reset();
                            Paused?.Invoke(this, new EventArgs());
                        }
                        else
                        {
                            trigger.Set();
                            Resumed?.Invoke(this, new EventArgs());
                        }
                        IsPausedChanged?.Invoke(this, new EventArgs());
                        StateChanged?.Invoke(this, new EventArgs());
                    }
                }
            }
        }

        /// <summary>Gets if the FileDownloader can start</summary>
        public bool CanStart
        {
            get
            {
                // Stop clears m_busy when cancellation is requested; the worker can
                // still be blocked in HTTP or draining its connections at that point.
                lock (Mutex)
                    return !m_disposed && !m_busy && !bgwDownloader.IsBusy && !System.Threading.Volatile.Read(ref m_stopCompletionPending) && !listDownloaders.Any(w => w.IsBusy);
            }
        }

        /// <summary>Gets if the FileDownloader can pause</summary>
        public bool CanPause
        {
            get
            {
                return IsBusy & !IsPaused & !bgwDownloader.CancellationPending;
            }
        }

        /// <summary>Gets if the FileDownloader can resume</summary>
        public bool CanResume
        {
            get
            {
                return IsBusy & IsPaused & !bgwDownloader.CancellationPending;
            }
        }

        /// <summary>Gets if the FileDownloader can stop</summary>
        public bool CanStop
        {
            get
            {
                return IsBusy & !bgwDownloader.CancellationPending;
            }
        }

        /// <summary>Gets the total size of all files together. Only avaible when the FileDownloader suports progress</summary>
        public long TotalSize
        {
            get
            {
                if (SupportsProgress)
                {
                    return m_totalSize;
                }
                else
                {
                    throw new InvalidOperationException("This FileDownloader that it doesn't support progress. Modify SupportsProgress to state that it does support progress to get the total size.");
                }
            }
        }

        /// <summary>Gets the total amount of bytes downloaded</summary>
        public long TotalProgress
        {
            get
            {
                return m_totalProgress;
            }
        }

        /// <summary>Gets the amount of bytes downloaded of the current file</summary>
        public long CurrentFileProgress
        {
            get
            {
                return m_currentFileProgress;
            }
        }

        /// <summary>Gets the total download percentage. Only avaible when the FileDownloader suports progress</summary>
        public double get_TotalPercentage(int decimals = 0)
        {
            if (SupportsProgress)
            {
                if (TotalSize <= 0L)
                    return TotalProgress > 0L ? 100.0d : 0.0d;
                double Perc = TotalProgress / (double)TotalSize * 100d;
                if (Perc > 100d)
                    Perc = 100d;
                return Math.Round(Perc, decimals);
            }
            else
            {
                throw new InvalidOperationException("This FileDownloader that it doesn't support progress. Modify SupportsProgress to state that it does support progress.");
            }
        }

        /// <summary>Gets the percentage of the current file progress</summary>
        public double get_CurrentFilePercentage(int decimals = 0)
        {
            if (CurrentFileSize <= 0L)
                return CurrentFileProgress > 0L ? 100.0d : 0.0d;
            double Perc = CurrentFileProgress / (double)CurrentFileSize * 100d;
            if (Perc > 100d)
                Perc = 100d;
            return Math.Round(Perc, decimals);
        }

        /// <summary>Gets the current download speed in bytes</summary>
        public int DownloadSpeed
        {
            get
            {
                lock (Mutex)
                {
                    int Speed = 0;
                    foreach (int value in m_currentSpeed.Values)
                        Speed += value;
                    return Speed;
                }

            }
        }

        // ''' <summary>Gets the FileInfo object representing the current file</summary>
        // Public ReadOnly Property CurrentFile() As FileInfo
        // Get
        // Return Me.File
        // End Get
        // End Property

        /// <summary>Gets the size of the current file in bytes</summary>
        public long CurrentFileSize
        {
            get
            {
                return m_currentFileSize;
            }
        }

        #endregion

    }
    #endregion

    /// <summary>Carries the final on-disk file name chosen by FileDownloader.RenamePartToReal.</summary>
    public class FileRenamedEventArgs : EventArgs
    {
        public FileRenamedEventArgs(string fileName)
        {
            FileName = fileName;
        }
        public readonly string FileName;
    }
}