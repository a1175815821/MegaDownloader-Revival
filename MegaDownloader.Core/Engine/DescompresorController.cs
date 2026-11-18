using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualBasic.CompilerServices;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers.Rar;

namespace MegaDownloader
{


    public class DescompresorController
    {

        private class QueueItem
        {
            public string Key;
            public string Source;
            public string Password;
            public bool CreateDirectory;
            public readonly HashSet<string> Codes = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, Action<bool, bool, string>> TaskCallbacks = new Dictionary<string, Action<bool, bool, string>>(StringComparer.Ordinal);
            public DateTime RetryUtc;
            public DateTime DeadlineUtc;
            public int VolumeCount;
        }

        private const int VolumeRetryIntervalSeconds = 20;
        private const int VolumeWaitWindowMinutes = 30;

        #region Región Shared
        internal static object Mutex = new object();

        private static DescompresorController _Controller;
        public static DescompresorController GetController()
        {
            lock (Mutex)
            {
                if (_Controller is null)
                {
                    _Controller = new DescompresorController();
                }
            }
            return _Controller;
        }

        #endregion

        #region Región privada

        private readonly Dictionary<string, QueueItem> _colaElementos = new Dictionary<string, QueueItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, QueueItem> _esperandoVolumenes = new Dictionary<string, QueueItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _rutasCompletadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private QueueItem _current;
        private string _currentPath;
        private bool _cancelRequested;
        private const int MaxCompletedArchives = 1024;
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".7z", ".rar", ".tar", ".zip" };

        // En estas variables (_TamanoTotalExtraido, etc) se guardará el progreso actual

        internal long? _TamanoTotal; // Tamaño total de los ficheros cuando se descompriman
        internal long? _TamanoTotalExtraido; // Tamaño total de los ficheros ya descomprimidos completamente
        internal long? _FicActTamanoTotal; // Tamaño total del fichero que se está descomprimiendo
        internal long? _FicActExtraido; // Bytes extraidos del fichero que se está descomprimiendo
        internal string _FicActNombre; // Nombre del fichero que se está descomprimiendo



        private DescompresorController()
        {
        }

        private static readonly Regex rxVolumePath = new Regex(@"^(?<base>.+)\.(?:7z\.(?<sz>[0-9]+)|part(?<rp>[0-9]+)\.rar|(?<rar>rar)|r[0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static string ArchiveKey(string path, Match volume)
        {
            if (!volume.Success)
                return path;
            // Use the group and naming format even when the first volume is absent.
            string suffix = volume.Groups["sz"].Success ? ".7z.*" : volume.Groups["rp"].Success ? ".part*.rar" : ".rar";
            return Path.Combine(Path.GetDirectoryName(path), volume.Groups["base"].Value + suffix);
        }

        private static string FirstVolume(string source, string[] volumes, Match volume)
        {
            if (!volume.Success)
                return source;
            if (!volume.Groups["sz"].Success && !volume.Groups["rp"].Success)
                return ArchiveKey(source, volume);
            string numberGroup = volume.Groups["sz"].Success ? "sz" : "rp";
            return volumes.FirstOrDefault(path =>
                {
                    string number = rxVolumePath.Match(Path.GetFileName(path)).Groups[numberGroup].Value;
                    return number.TrimStart('0') == "1";
                });
        }

        public void RequestCancel()
        {
            lock (Mutex)
                _cancelRequested = true;
        }

        private bool IsCancelRequested()
        {
            lock (Mutex)
                return _cancelRequested;
        }

        private bool PonerElementoAProcesar()
        {
            lock (Mutex)
            {
                if (_cancelRequested || _current is not null || _colaElementos.Count == 0)
                    return false;
                string key = _colaElementos.Keys.ElementAtOrDefault(0);
                _current = _colaElementos[key];
                _currentPath = _current.Source;
                _colaElementos.Remove(key);
                return true;
            }
        }

        private void ProcesarElemento(BackgroundWorker worker)
        {
            QueueItem item;
            lock (Mutex)
                item = _current;
            if (item is null)
                return;
            try
            {
                if (worker.CancellationPending || IsCancelRequested())
                {
                    FinishCurrent(item, false, "Extraction cancelled.");
                    return;
                }

                var volume = rxVolumePath.Match(Path.GetFileName(item.Source));
                string[] volumes = PresentVolumes(item.Source, volume);
                string first = FirstVolume(item.Source, volumes, volume);
                if (first is null || !File.Exists(first))
                {
                    if (volume.Success)
                    {
                        DeferCurrentElement(item, volumes.Length, "First archive volume is missing.");
                    }
                    else
                    {
                        FinishCurrent(item, false, "Invalid archive path (file not found): " + item.Source);
                    }
                    return;
                }

                string inputSeal = ArchiveSeal(volumes);
                lock (Mutex)
                    _currentPath = first;

                string output = Path.GetDirectoryName(first);
                if (item.CreateDirectory)
                {
                    string folderName = volume.Success ? volume.Groups["base"].Value : Path.GetFileNameWithoutExtension(first);
                    output = PathGuard.GetSafePathUnderRoot(output, PathGuard.SanitizeFileName(folderName, "extracted"), allowRoot: false);
                }

                var extractor = new Descompressor(first, output, item.Password);
                var extractionThread = new System.Threading.Thread(extractor.Extract) { Priority = System.Threading.ThreadPriority.BelowNormal, IsBackground = true };
                extractionThread.Start();
                bool cancellationLogged = false;
                while (!extractionThread.Join(250))
                {
                    if (!worker.CancellationPending && !IsCancelRequested())
                        continue;
                    extractor.CancelRequested = true;
                    if (!cancellationLogged)
                    {
                        cancellationLogged = true;
                        Log.WriteInfo("Waiting for extraction to cancel for " + first);
                    }
                }
                if (extractor.Exception is null)
                {
                    FinishCurrent(item, true, "", inputSeal);
                }
                else if (!worker.CancellationPending && !IsCancelRequested() && (extractor.Exception is IncompleteArchiveException || extractor.Exception is SharpCompress.Common.IncompleteArchiveException || volume.Success && IsIncompleteVolumeError(extractor.Exception.Message)))
                {
                    DeferCurrentElement(item, volumes.Length, extractor.Exception.Message);
                }
                else
                {
                    FinishCurrent(item, false, extractor.Exception.Message);
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Unexpected error while extracting " + item.Source + ": " + Log.SafeException(ex));
                FinishCurrent(item, false, "Unexpected extraction error: " + ex.Message);
            }
        }

        private void FinishCurrent(QueueItem item, bool success, string message, string seal = null)
        {
            string[] codes;
            lock (Mutex)
            {
                if (!ReferenceEquals(_current, item))
                    return;
                if (success && seal is not null)
                {
                    if (_rutasCompletadas.Count >= MaxCompletedArchives)
                        _rutasCompletadas.Remove(_rutasCompletadas.Keys.ElementAtOrDefault(0));
                    _rutasCompletadas[item.Key] = seal;
                }
                codes = item.Codes.ToArray();
                _current = null;
                _currentPath = null;
            }
            NotifyDownloads(codes, item.TaskCallbacks, false, success, message);
        }

        private void NotifyDownloads(IEnumerable<string> codes, Dictionary<string, Action<bool, bool, string>> callbacks, bool deferred, bool success, string message)
        {
            foreach (string code in codes)
            {
                Action<bool, bool, string> callback = null;
                if (callbacks is not null)
                    callbacks.TryGetValue(code, out callback);
                NotifyDownload(code, callback, deferred, success, message);
            }
        }

        private void NotifyDownload(string code, Action<bool, bool, string> callback, bool deferred, bool success, string message)
        {
            try
            {
                if (callback is not null)
                {
                    callback(deferred, success, message);
                }
                else if (deferred)
                {
                    DescompresionDiferida?.Invoke(code);
                }
                else
                {
                    DescompresionFinalizada?.Invoke(code, success, message);
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Could not report extraction state for " + code + ": " + Log.SafeException(ex));
            }
        }


        #endregion

        #region Región pública

        public event DescompresionFinalizadaEventHandler DescompresionFinalizada;

        public delegate void DescompresionFinalizadaEventHandler(string Code, bool Success, string ErrorMessage);
        public event DescompresionDiferidaEventHandler DescompresionDiferida;

        public delegate void DescompresionDiferidaEventHandler(string Code);

        // Tamaño total de los ficheros dentro del elemento que se está descomprimiendo
        public long? EleActual_TamanoTotal
        {
            get
            {
                lock (Mutex)
                    return _TamanoTotal;
            }
        }


        // Tamaño total de los ficheros ya descomprimidos completamente dentro del elemento que se está descomprimiendo
        public long? EleActual_TamanoTotalExtraido
        {
            get
            {
                lock (Mutex)
                    return _TamanoTotalExtraido;
            }
        }

        // Ruta del elemento que se está descomprimiendo
        public string EleActual_Ruta
        {
            get
            {
                lock (Mutex)
                    return _currentPath;
            }
        }

        // Bytes extraidos del fichero que se está descomprimiendo
        public long? EleActual_FicActExtraido
        {
            get
            {
                lock (Mutex)
                    return _FicActExtraido;
            }
        }

        // Nombre del fichero que se está descomprimiendo
        public string EleActual_FicActNombre
        {
            get
            {
                lock (Mutex)
                    return _FicActNombre;
            }
        }



        private static string[] PresentVolumes(string path, Match volume)
        {
            if (!volume.Success)
                return File.Exists(path) ? (new string[] { path }) : (new string[] { });

            string baseName = Regex.Escape(volume.Groups["base"].Value);
            string suffix = volume.Groups["sz"].Success ? @"\.7z\.[0-9]+" : volume.Groups["rp"].Success ? @"\.part[0-9]+\.rar" : @"\.(?:rar|r[0-9]+)";
            var @group = new Regex("^" + baseName + suffix + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return Directory.EnumerateFiles(Path.GetDirectoryName(path)).Where(candidate => @group.IsMatch(Path.GetFileName(candidate))).OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string ArchiveSeal(string[] volumes)
        {
            if (volumes.Length == 0)
                return null;
            var seal = new System.Text.StringBuilder();
            var buffer = new byte[8192];
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    foreach (string path in volumes)
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var info = new FileInfo(path);
                            int count = 0;
                            while (count < buffer.Length)
                            {
                                int got = stream.Read(buffer, count, buffer.Length - count);
                                if (got == 0)
                                    break;
                                count += got;
                            }
                            seal.Append(info.Name.ToUpperInvariant()).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('|').Append(Convert.ToBase64String(sha.ComputeHash(buffer, 0, count))).AppendLine();
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                return null;
            }
            return seal.ToString();
        }

        private class IncompleteArchiveException : IOException
        {

            public IncompleteArchiveException(string message) : base(message)
            {
            }
        }

        private static bool IsIncompleteVolumeError(string message)
        {
            return message.IndexOf("Unexpected end of archive", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("Missing volume", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DeferCurrentElement(QueueItem item, int present, string reason)
        {
            bool deferred = false;
            string[] codes;
            Dictionary<string, Action<bool, bool, string>> callbacks;
            lock (Mutex)
            {
                if (!ReferenceEquals(_current, item))
                    return;
                var utc = DateTime.UtcNow;
                if (item.DeadlineUtc == DateTime.MinValue || present > item.VolumeCount)
                {
                    item.VolumeCount = present;
                    item.DeadlineUtc = utc.AddMinutes(VolumeWaitWindowMinutes);
                }
                if (!_cancelRequested && utc < item.DeadlineUtc && item.Codes.Count > 0)
                {
                    item.RetryUtc = utc.AddSeconds(VolumeRetryIntervalSeconds);
                    _esperandoVolumenes[item.Key] = item;
                    deferred = true;
                }
                codes = item.Codes.ToArray();
                callbacks = new Dictionary<string, Action<bool, bool, string>>(item.TaskCallbacks, StringComparer.Ordinal);
                _current = null;
                _currentPath = null;
            }
            if (deferred)
                Log.WriteInfo("Waiting for archive volumes for " + item.Source);
            NotifyDownloads(codes, callbacks, deferred, false, reason);
        }

        private void PromoteWaitingVolumes()
        {
            lock (Mutex)
            {
                var utc = DateTime.UtcNow;
                QueueItem[] due = _esperandoVolumenes.Values.Where(item => item.RetryUtc <= utc).ToArray();
                foreach (QueueItem item in due)
                {
                    _esperandoVolumenes.Remove(item.Key);
                    _colaElementos[item.Key] = item;
                }
            }
        }

        public bool AgregarElemento(string Code, string Path, bool CrearDirectorio, string Password)
        {
            return EnqueueArchive(Code, Path, CrearDirectorio, Password, null);
        }

        internal bool EnqueueArchive(string Code, string Path, bool CrearDirectorio, string Password, Action<bool, bool, string> callback)
        {
            if (string.IsNullOrEmpty(Code) || string.IsNullOrEmpty(Path))
                return false;
            string original = System.IO.Path.GetFullPath(Path);
            if (!File.Exists(original))
                return false;
            var volume = rxVolumePath.Match(System.IO.Path.GetFileName(original));
            if (!volume.Success && !SupportedExtensions.Contains(System.IO.Path.GetExtension(original)))
                return false;
            string key = ArchiveKey(original, volume);

            string seal = null;
            bool checkedSeal = false;
            do
            {
                bool deferred = false;
                bool completed = false;
                lock (Mutex)
                {
                    if (_cancelRequested)
                        return false;
                    QueueItem item = null;
                    if (_current is not null && string.Equals(_current.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        _current.Codes.Add(Code);
                        if (callback is not null)
                            _current.TaskCallbacks[Code] = callback;
                        return true;
                    }
                    if (_colaElementos.TryGetValue(key, out item))
                    {
                        item.Codes.Add(Code);
                        if (callback is not null)
                            item.TaskCallbacks[Code] = callback;
                        return true;
                    }
                    if (_esperandoVolumenes.TryGetValue(key, out item))
                    {
                        if (item.Codes.Add(Code))
                            item.RetryUtc = DateTime.UtcNow;
                        if (callback is not null)
                            item.TaskCallbacks[Code] = callback;
                        deferred = true;
                    }
                    else
                    {
                        string previousSeal = null;
                        if (!_rutasCompletadas.TryGetValue(key, out previousSeal) || checkedSeal)
                        {
                            completed = seal is not null && (seal ?? "") == (previousSeal ?? "");
                            if (!completed)
                            {
                                _rutasCompletadas.Remove(key);
                                item = new QueueItem() { Key = key, Source = original, Password = Password, CreateDirectory = CrearDirectorio };
                                item.Codes.Add(Code);
                                if (callback is not null)
                                    item.TaskCallbacks[Code] = callback;
                                _colaElementos[key] = item;
                                return true;
                            }
                        }
                    }
                }
                if (completed || deferred)
                {
                    NotifyDownload(Code, callback, deferred, completed, "");
                    return true;
                }
                // Compute a complete group stamp outside the controller lock.
                seal = ArchiveSeal(PresentVolumes(original, volume));
                checkedSeal = true;
            }
            while (true);
        }

        internal void RemoveDownload(string code)
        {
            lock (Mutex)
            {
                if (_current is not null)
                {
                    _current.Codes.Remove(code);
                    _current.TaskCallbacks.Remove(code);
                }
                foreach (Dictionary<string, QueueItem> queue in new[] { _colaElementos, _esperandoVolumenes })
                {
                    foreach (QueueItem item in queue.Values.ToArray())
                    {
                        if (item.Codes.Remove(code) && item.Codes.Count == 0)
                            queue.Remove(item.Key);
                        item.TaskCallbacks.Remove(code);
                    }
                }
            }
        }

        /// <summary>
    /// 同步解压单个压缩包（不支持队列/取消/进度事件）：成功返回 ""，失败返回错误描述。
    /// 供回归测试（Tests/DescompresorTests）及无需后台队列的调用方使用。
    /// </summary>
        public static string ExtractArchiveSync(string archivePath, string outputDir, string password)
        {
            try
            {
                var d = new Descompressor(archivePath, outputDir, password);
                d.Extract();
                if (d.Exception is not null)
                    return d.Exception.Message;
                return "";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static void DescompresorController_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            var controller = GetController();
            while (!worker.CancellationPending && !controller.IsCancelRequested())
            {
                controller.PromoteWaitingVolumes();
                if (controller.PonerElementoAProcesar())
                    controller.ProcesarElemento(worker);
                if (!worker.CancellationPending && !controller.IsCancelRequested())
                    System.Threading.Thread.Sleep(600);
            }
            e.Cancel = true;
        }

        public List<string> GetCola()
        {
            lock (Mutex)
                return _colaElementos.Values.Concat(_esperandoVolumenes.Values).Select(item => item.Source).ToList();
        }

        public bool Ocupado()
        {
            lock (Mutex)
                return _current is not null || _colaElementos.Count > 0;
        }

        internal bool IsExtracting
        {
            get
            {
                lock (Mutex)
                    return _current is not null;
            }
        }


        #endregion

        #region Clase descompresora




        private class Descompressor
        {


            public string Password;
            private string PathFichero;
            private string PathExtraccion;
            public Exception Exception;
            private readonly List<KeyValuePair<string, string>> PreservedCopies = new List<KeyValuePair<string, string>>();
            private readonly List<string> CreatedFiles = new List<string>();
            private readonly List<string> CreatedDirectories = new List<string>();
            private bool _cancelRequested;

            public bool CancelRequested
            {
                get
                {
                    return System.Threading.Volatile.Read(ref _cancelRequested);
                }
                set
                {
                    System.Threading.Volatile.Write(ref _cancelRequested, value);
                }
            }

            public Descompressor(string _Fichero, string _PathExtraccion, string _Password)
            {

                PathFichero = _Fichero;
                PathExtraccion = _PathExtraccion;
                Password = _Password;
                CancelRequested = false;

                if (string.IsNullOrEmpty(Password))
                    Password = null; // Evitamos string.empty
            }

            private static SharpCompress.Readers.ReaderOptions ReaderOpts(string Password)
            {
                // SharpCompress 0.48+ made Password an init-only property.
                return new SharpCompress.Readers.ReaderOptions
                {
                    Password = string.IsNullOrEmpty(Password) ? null : Password
                };
            }

            private static IArchive getIArchive(string PathFichero, string Password, List<string> Volumenes)
            {

                if (PathFichero.ToUpper().EndsWith(".RAR"))
                {
                    // P2-3: 分卷 RAR 必须把整组卷一起交给 SharpCompress。此前只开第一卷,
                    // 于是 IsComplete 反映的只是"第一卷在不在",缺卷检测与后续卷的条目
                    // 全都拿不到(不带密码的多卷 RAR 只能解出第一卷的内容)。
                    if (Volumenes is not null && Volumenes.Count > 1)
                    {
                        var archivos = new List<FileInfo>();
                        foreach (string v in Volumenes)
                        {
                            try
                            {
                                archivos.Add(new FileInfo(v));
                            }
                            catch (Exception ex)
                            {
                                Log.WriteWarning("Could not stat RAR volume '" + v + "': " + Log.SafeException(ex));
                            }
                        }
                        if (archivos.Count > 1)
                        {
                            return SharpCompress.Archives.Rar.RarArchive.OpenArchive(archivos, ReaderOpts(Password));
                        }
                    }
                    return SharpCompress.Archives.Rar.RarArchive.OpenArchive(PathFichero, ReaderOpts(Password));
                }
                else if (PathFichero.ToUpper().EndsWith(".ZIP"))
                {
                    return SharpCompress.Archives.Zip.ZipArchive.OpenArchive(PathFichero, ReaderOpts(Password));
                }
                else
                {
                    return SharpCompress.Archives.ArchiveFactory.OpenArchive(PathFichero, ReaderOpts(null));
                }

            }

            /// <summary>
        /// P2-3: 收集 RAR 分卷,返回按卷序排列的完整路径列表(第一卷在最前)。
        /// 此前这里靠文件名后缀反推卷号(只认 part1/part01/part001/part0001.rar),于是:
        /// - .r00/.r01 这类旧式命名算出的 LengthPart=0 → 只读第一卷,解压结果不完整;
        /// - 单卷 RAR 只要文件名碰巧叫 xxx.part1.rar 就会被当成多卷,再叠加原 :687
        ///    那个"在单卷判定之前就抛"的 throw → 带密码的单卷 RAR 直接被拒。
        /// 现在文件名只用来"找兄弟卷",不再用来"判定是不是多卷"(判定交给 archive
        /// 自己的 rar.IsMultipartVolume())。支持两种通行命名:
        /// - 新式 foo.part1.rar / foo.part2.rar ...
        /// - 旧式 foo.rar(第一卷)+ foo.r00 + foo.r01 ...
        /// </summary>
            private static List<string> DescubrirVolumenesRar(string primeraRuta)
            {
                var resultado = new List<string>();
                if (string.IsNullOrWhiteSpace(primeraRuta))
                    return resultado;

                string directorio = Path.GetDirectoryName(primeraRuta);
                if (string.IsNullOrEmpty(directorio))
                    directorio = ".";
                // 注意:必须用完整文件名做判定。Path.GetFileNameWithoutExtension("foo.r00")
                // 会把 ".r00" 当成扩展名剥掉(得到 "foo"),旧式分卷就会被误判并重复入列。
                string nombre = Path.GetFileName(primeraRuta);   // foo.part01.rar / foo.rar / foo.r00

                // --- 新式 foo.partN.rar ---
                var mPart = Regex.Match(nombre, @"^(?<base>.+)\.part(?<num>\d+)\.rar$", RegexOptions.IgnoreCase);
                if (mPart.Success)
                {
                    string baseNombre = mPart.Groups["base"].Value;
                    int digitos = mPart.Groups["num"].Value.Length;
                    int num = int.Parse(mPart.Groups["num"].Value);
                    resultado.Add(primeraRuta);
                    int n = num + 1;
                    do
                    {
                        string cand = Path.Combine(directorio, baseNombre + ".part" + n.ToString().PadLeft(digitos, '0') + ".rar");
                        if (!File.Exists(cand))
                            break;
                        resultado.Add(cand);
                        n += 1;
                    }
                    while (n <= 9999);
                    return resultado;
                }

                // --- 旧式:选中的是 foo.rNN 这一卷,回到 foo.rar 第一卷 ---
                var mOld = Regex.Match(nombre, @"^(?<base>.+)\.r(?<num>\d+)$", RegexOptions.IgnoreCase);
                if (mOld.Success)
                {
                    string baseNombre = mOld.Groups["base"].Value;
                    string cabecera = Path.Combine(directorio, baseNombre + ".rar");
                    if (!File.Exists(cabecera))
                    {
                        // 没有第一卷:交回给 archive 自己报"这不是第一卷"。
                        resultado.Add(primeraRuta);
                        return resultado;
                    }
                    resultado.Add(cabecera);
                    int n = 0;
                    do
                    {
                        string cand = Path.Combine(directorio, baseNombre + ".r" + n.ToString().PadLeft(2, '0'));
                        if (!File.Exists(cand))
                            break;
                        if (!resultado.Contains(cand))
                            resultado.Add(cand);
                        n += 1;
                    }
                    while (n <= 9999);
                    return resultado;
                }

                // --- foo.rar:单卷,或者旧式分卷的第一卷 ---
                if (nombre.ToLowerInvariant().EndsWith(".rar"))
                {
                    string baseNombre = nombre.Substring(0, nombre.Length - 4);
                    resultado.Add(primeraRuta);
                    int n = 0;
                    do
                    {
                        string cand = Path.Combine(directorio, baseNombre + ".r" + n.ToString().PadLeft(2, '0'));
                        if (!File.Exists(cand))
                            break;
                        resultado.Add(cand);
                        n += 1;
                    }
                    while (n <= 9999);
                    return resultado;
                }

                resultado.Add(primeraRuta);
                return resultado;
            }


            /// <summary>
        /// P3-12: 判断是否 7z 容器。只有结尾 ".7z",或 ".7z.NNN"(NNN 纯数字分卷)
        /// 才算 7z。此前用 Contains(".7z."),于是 xxx.7z.zip / xxx.7z.rar 这类
        /// zip/rar 也被送去 7-Zip CLI,而 7zr 打不开 zip/rar → 退出码非 0 →
        /// 报 "7-Zip failed with exit code N",原本能正常解的包反而失败。
        /// </summary>
            private static bool Is7zContainer(string PathFichero)
            {
                return PathFichero.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || rxVolumePath.Match(Path.GetFileName(PathFichero)).Groups["sz"].Success;
            }

            public void Extract()
            {
                try
                {
                    CreateOutputDirectory(Path.GetFullPath(PathExtraccion));
                    if (Is7zContainer(PathFichero))
                    {
                        Extract7z();
                    }
                    else
                    {
                        ExtractManaged();
                    }
                }
                catch (Exception ex)
                {
                    Exception = ex;
                    RestoreOutput();
                }
            }

            private void ExtractManaged()
            {
                List<string> rarVolumes = null;
                if (PathFichero.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                    rarVolumes = DescubrirVolumenesRar(PathFichero);

                using (var archive = getIArchive(PathFichero, Password, rarVolumes))
                {
                    if (!archive.IsComplete)
                    {
                        throw new IncompleteArchiveException(Language.GetText("Archive is incomplete: one or more volumes are missing"));
                    }

                    SharpCompress.Archives.Rar.RarArchive rar = archive as SharpCompress.Archives.Rar.RarArchive;
                    string name = Path.GetFileName(PathFichero);
                    // Older RAR volumes may omit the first-volume flag. For a complete old-style
                    // set, discovery already placed the .rar header before .r00.
                    bool olderVolumeSet = rxVolumePath.Match(name).Groups["rar"].Success && rarVolumes is not null && rarVolumes.Count > 1;
                    if (rar is not null && rar.IsMultipartVolume() && !rar.IsFirstVolume() && !olderVolumeSet)
                    {
                        throw new IncompleteArchiveException(Language.GetText("This file is not the first volume of a multipart RAR archive, so there is nothing to extract from it alone. Add the first volume (part1.rar) instead."));
                    }

                    var entries = new List<IArchiveEntry>();
                    var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    long total = 0L;
                    foreach (IArchiveEntry entry in archive.Entries)
                    {
                        ThrowIfCancelled();
                        if (entry.Size < 0L || entry.Size > MaxExtractTotalBytes - total)
                        {
                            throw new InvalidOperationException("Archive rejected: invalid or excessive uncompressed size.");
                        }
                        total += entry.Size;
                        EnsureExtractWithinQuota(total, entries.Count + 1);
                        string dest = PathGuard.GetSafeArchiveEntryPath(PathExtraccion, entry.Key);
                        if (!destinations.Add(dest))
                            throw new IOException("Archive contains ambiguous entry names: " + entry.Key);
                        entries.Add(entry);
                    }

                    var controller = GetController();
                    lock (Mutex)
                    {
                        controller._TamanoTotal = total;
                        controller._FicActTamanoTotal = 0;
                        controller._TamanoTotalExtraido = 0;
                        controller._FicActExtraido = 0;
                        controller._FicActNombre = "";
                    }
                    try
                    {
                        if (rar is not null && (archive.IsSolid || !string.IsNullOrEmpty(Password)))
                        {
                            ExtractRarEntries(rarVolumes);
                        }
                        else
                        {
                            foreach (IArchiveEntry entry in entries)
                                ExtractEntry(entry, entry.OpenEntryStream);
                        }
                    }
                    finally
                    {
                        ClearExtractionProgress(controller);
                    }
                }
            }

            private void ExtractRarEntries(List<string> volumes)
            {
                var streams = new List<Stream>();
                try
                {
                    foreach (string path in volumes)
                        streams.Add(File.OpenRead(path));
                    using (var reader = streams.Count == 1 ? SharpCompress.Readers.Rar.RarReader.OpenReader(streams[0], ReaderOpts(Password)) : SharpCompress.Readers.Rar.RarReader.OpenReader(streams, ReaderOpts(Password)))
                    {
                        while (reader.MoveToNextEntry())
                            ExtractEntry(reader.Entry, () => reader.OpenEntryStream());
                    }
                }
                catch (Exception ex) when (ex.Message.IndexOf("does not support multi-volume decryption", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("Encrypted multipart RAR archives require an external RAR extractor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new NotSupportedException(Language.GetText("Encrypted multipart RAR archives require an external RAR extractor to decrypt."), ex);
                }
                catch (Exception ex) when (volumes.Count > 1 && !string.IsNullOrEmpty(Password) && !(ex is OperationCanceledException) && !(ex is SharpCompress.Common.IncompleteArchiveException))
                {
                    throw new ApplicationException(Language.GetText("Could not extract the RAR archive. Check that all volumes are present and that the password is correct.") + " (" + ex.Message + ")", ex);
                }
                finally
                {
                    foreach (Stream stream in streams)
                        stream.Dispose();
                }
            }

            private void ExtractEntry(IEntry entry, Func<Stream> openEntry)
            {
                ThrowIfCancelled();
                string dest = PathGuard.GetSafeArchiveEntryPath(PathExtraccion, entry.Key);
                if (!string.IsNullOrEmpty(entry.LinkTarget))
                    throw new IOException("Archive links are not supported.");
                if (entry.IsDirectory)
                {
                    CreateOutputDirectory(dest);
                    return;
                }

                var controller = GetController();
                lock (Mutex)
                {
                    controller._FicActNombre = entry.Key;
                    controller._TamanoTotalExtraido += controller._FicActTamanoTotal;
                    controller._FicActTamanoTotal = entry.Size;
                    controller._FicActExtraido = 0;
                }
                using (var source = openEntry())
                {
                    WriteEntry(source, dest, entry.LastModifiedTime);
                }
                lock (Mutex)
                    controller._FicActExtraido = entry.Size;
                DescompresionThrottle.ApplyThrottle();
            }

            private static void ClearExtractionProgress(DescompresorController controller)
            {
                lock (Mutex)
                {
                    controller._TamanoTotal = default;
                    controller._FicActTamanoTotal = default;
                    controller._TamanoTotalExtraido = default;
                    controller._FicActExtraido = default;
                    controller._FicActNombre = null;
                }
            }

            private const long MaxExtractTotalBytes = 50L * 1024L * 1024L * 1024L; // 50 GiB hard cap
            private const int MaxExtractEntries = 100000;

            #region 7z CLI support

            /// <summary>
        /// Extracts a .7z archive (single or multipart .7z.001) via the 7-Zip command
        /// line. A system-installed 7-Zip is preferred; otherwise the embedded
        /// 7zr.exe (public domain, 7z format only) is dropped next to the user's
        /// application data and used. Entries are validated against path traversal
        /// BEFORE extraction using the same PathGuard rules as the SharpCompress path.
        /// </summary>
            private void Extract7z()
            {
                string cli = FindSystemSevenZip();
                if (cli is null)
                    cli = EnsureEmbedded7zr();
                if (cli is null)
                {
                    throw new ApplicationException("No 7-Zip executable available to extract .7z archives.");
                }

                // Both passes need the password: a header-encrypted archive (-mhe=on) cannot
                // even be listed without it, so Pass 1 used to fail with exit code 255 no
                // matter how correct the configured password was.
                // Quoted because Windows argv splitting would otherwise break a password
                // containing spaces into several arguments.
                string passwordArg = "";
                if (!string.IsNullOrEmpty(Password))
                {
                    // P2-1: 7-Zip's own command-line parser cannot express a literal double
                    // quote inside -p. Verified empirically with the bundled 7zr 26.02 via
                    // CreateProcessW (exact .NET argument passing): neither backslash escaping
                    // (-p"pa\"ss") nor doubled quotes (-p"pa""ss") reaches the password — both
                    // corrupt the argument and fail. Reject with an actionable, localized
                    // message instead of a raw exception. Trailing backslashes, on the other
                    // hand, ARE delivered correctly with plain quoting (same test: a trailing
                    // backslash changed the password as expected), so the old backslash
                    // rejection was unnecessary and has been removed.
                    if (Password.Contains("\""))
                    {
                        Log.WriteWarning("Extract7z: password contains a double quote, which the 7-Zip CLI cannot express; rejecting.");
                        throw new ApplicationException(Language.GetText("The extraction password contains a double quote, which the built-in 7-Zip extractor cannot pass on its command line. Extract this archive with the full 7-Zip application, or use an archive whose password has no double quotes."));
                    }
                    passwordArg = " -p\"" + Password + "\"";
                }

                string listing = RunSevenZip(cli, "l -ba -slt -sccUTF-8" + passwordArg + " -- \"" + PathFichero + "\"", checkCancel: true);
                var entryKeys = new List<string>();
                long totalUncompressed = 0L;
                bool isEntry = false;
                foreach (string line in listing.Split(new string[] { Microsoft.VisualBasic.Constants.vbCrLf, Microsoft.VisualBasic.Constants.vbLf, Microsoft.VisualBasic.Constants.vbCr }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("Path = ", StringComparison.Ordinal))
                    {
                        string entryPath = line.Substring(7);
                        // Some CLI builds include the archive header before the entries.
                        isEntry = !string.Equals(entryPath, PathFichero, StringComparison.OrdinalIgnoreCase);
                        if (isEntry)
                            entryKeys.Add(entryPath);
                    }
                    else if (isEntry && line.StartsWith("Size = ", StringComparison.Ordinal))
                    {
                        long size;
                        if (!long.TryParse(line.Substring(7), out size) || size < 0L || size > MaxExtractTotalBytes - totalUncompressed)
                        {
                            throw new InvalidOperationException("Archive rejected: invalid or excessive uncompressed size.");
                        }
                        totalUncompressed += size;
                    }
                    EnsureExtractWithinQuota(totalUncompressed, entryKeys.Count);
                }

                var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string staging = Path.Combine(PathExtraccion, ".megadownloader-extract-" + Guid.NewGuid().ToString("N"));
                var paths = new List<KeyValuePair<string, string>>(entryKeys.Count);
                foreach (string key in entryKeys)
                {
                    string source = PathGuard.GetArchiveStagingPath(staging, key);
                    string dest = PathGuard.GetSafeArchiveEntryPath(PathExtraccion, key);
                    if (!destinations.Add(dest))
                        throw new IOException("Archive contains ambiguous entry names: " + key);
                    paths.Add(new KeyValuePair<string, string>(source, dest));
                }

                var c = GetController();
                try
                {
                    Directory.CreateDirectory(staging);
                    lock (Mutex)
                    {
                        c._TamanoTotal = totalUncompressed;
                        c._FicActTamanoTotal = 0;
                        c._TamanoTotalExtraido = 0;
                        c._FicActExtraido = 0;
                        c._FicActNombre = PathFichero;
                    }
                    string args = "x -y -bd -sccUTF-8 -o\"" + staging + "\"" + passwordArg + " -- \"" + PathFichero + "\"";
                    RunSevenZip(cli, args, checkCancel: true);
                    foreach (KeyValuePair<string, string> entry in paths)
                    {
                        string source = PathGuard.ArchiveIoPath(entry.Key);
                        var attributes = File.GetAttributes(source);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            throw new IOException("Archive links are not supported.");
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            CreateOutputDirectory(entry.Value);
                        }
                        else
                        {
                            CreateOutputDirectory(Path.GetDirectoryName(entry.Value));
                            ResolveEntryDestination(entry.Value, requireOriginalName: true);
                            File.Move(source, PathGuard.ArchiveIoPath(entry.Value));
                            CreatedFiles.Add(entry.Value);
                        }
                        ThrowIfCancelled();
                    }
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(staging))
                            Directory.Delete(PathGuard.ArchiveIoPath(staging), true);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Extraction: could not remove staging directory '" + staging + "': " + Log.SafeException(ex));
                    }
                    ClearExtractionProgress(c);
                }
            }

            private void RestoreOutput()
            {
                for (int i = CreatedFiles.Count - 1; i >= 0; i -= 1)
                {
                    try
                    {
                        string output = PathGuard.ArchiveIoPath(CreatedFiles[i]);
                        if (!File.Exists(output))
                            continue;
                        var attributes = File.GetAttributes(output);
                        if ((attributes & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(output, attributes & ~FileAttributes.ReadOnly);
                        File.Delete(output);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Extraction: could not remove incomplete output '" + CreatedFiles[i] + "': " + Log.SafeException(ex));
                    }
                }

                for (int i = PreservedCopies.Count - 1; i >= 0; i -= 1)
                {
                    var pair = PreservedCopies[i];
                    try
                    {
                        File.Move(PathGuard.ArchiveIoPath(pair.Value), PathGuard.ArchiveIoPath(pair.Key));
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Extraction: could not restore '" + pair.Value + "': " + Log.SafeException(ex));
                    }
                }

                for (int i = CreatedDirectories.Count - 1; i >= 0; i -= 1)
                {
                    string path = CreatedDirectories[i];
                    try
                    {
                        string ioPath = PathGuard.ArchiveIoPath(path);
                        if (Directory.Exists(ioPath) && !Directory.EnumerateFileSystemEntries(ioPath).Any())
                            Directory.Delete(ioPath);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteWarning("Extraction: could not remove output directory '" + path + "': " + Log.SafeException(ex));
                    }
                }
            }

            /// <summary>
        /// Locates a system-installed 7-Zip (7z.exe). Checks both registry views
        /// (the process is x86, but 7-Zip is commonly installed as x64) and the
        /// well-known install folders. Returns Nothing when not found.
        /// </summary>
            private static string FindSystemSevenZip()
            {
                try
                {
                    // The registry probe only exists on Windows; elsewhere the
                    // well-known install folders below are checked instead.
                    foreach (Microsoft.Win32.RegistryView view in OperatingSystem.IsWindows()
                        ? new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Default }
                        : Array.Empty<Microsoft.Win32.RegistryView>())
                    {
                        using (var @base = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view))
                        {
                            using (var key = @base.OpenSubKey(@"SOFTWARE\7-Zip"))
                            {
                                if (key is not null)
                                {
                                    var dir = key.GetValue("Path");
                                    if (dir is not null)
                                    {
                                        string exe = Path.Combine(Conversions.ToString(dir), "7z.exe");
                                        if (File.Exists(exe))
                                            return exe;
                                    }
                                    var root = key.GetValue("Root");
                                    if (root is not null)
                                    {
                                        string exe2 = Path.Combine(Conversions.ToString(root), "7z.exe");
                                        if (File.Exists(exe2))
                                            return exe2;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("7z lookup: registry probe failed: " + Log.SafeException(ex));
                }

                foreach (string candidate in new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\7-Zip\7z.exe", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\7-Zip\7z.exe" })
                {
                    if (File.Exists(candidate))
                        return candidate;
                }
                return null;
            }

            /// <summary>
        /// Extracts the embedded 7zr.exe (resource "MegaDownloader.7zr.exe", public
        /// domain) to %LOCALAPPDATA%\MegaDownloader\bin and returns its path. The file
        /// is (re)written whenever the embedded payload differs in size, so upgrades
        /// replace the stale binary automatically.
        /// </summary>
            private static string EnsureEmbedded7zr()
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"MegaDownloader\bin");
                string target = Path.Combine(dir, "7zr.exe");

                try
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    using (var src = asm.GetManifestResourceStream("MegaDownloader.7zr.exe"))
                    {
                        if (src is null)
                        {
                            Log.WriteError("7z extraction: embedded 7zr.exe resource is missing.");
                            return null;
                        }

                        bool rewrite = true;
                        if (File.Exists(target))
                        {
                            try
                            {
                                rewrite = new FileInfo(target).Length != src.Length;
                            }
                            catch
                            {
                                rewrite = true;
                            }
                        }

                        if (rewrite)
                        {
                            Directory.CreateDirectory(dir);
                            using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                src.CopyTo(dst);
                            }
                            Log.WriteInfo("7z extraction: embedded 7zr.exe deployed to " + target);
                        }
                    }
                    return target;
                }
                catch (Exception ex)
                {
                    Log.WriteError("7z extraction: failed to deploy embedded 7zr.exe: " + Log.SafeException(ex));
                    return null;
                }
            }

            /// <summary>
        /// Runs the 7-Zip CLI with the given arguments and returns its stdout.
        /// Exit code 0/1 (OK/warnings) pass; 2 (fatal, e.g. wrong password) and any
        /// other code throw with the tail of stderr/stdout for the error message.
        /// Honors CancelRequested by killing the process.
        /// </summary>
            private string RunSevenZip(string cliPath, string arguments, bool checkCancel)
            {
                if (checkCancel && CancelRequested)
                    throw new OperationCanceledException("Extraction cancelled.");
                var psi = new ProcessStartInfo();
                psi.FileName = cliPath;
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                // Give the CLI an immediate EOF on stdin. A header-encrypted archive with no
                // password configured makes 7-Zip prompt for one; an inherited open stdin
                // would keep it waiting for input that this background job cannot provide.
                psi.RedirectStandardInput = true;
                psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
                psi.StandardErrorEncoding = System.Text.Encoding.UTF8;

                // Never log the raw arguments: they can carry "-p<password>". The quoted
                // alternative must come first so a password containing spaces is redacted
                // whole instead of only up to its first space.
                string safeArgs = Regex.Replace(arguments, @"-p(?:""[^""]*""|\S+)", "-p[redacted]");
                Log.WriteInfo("7z CLI: " + Path.GetFileName(cliPath) + " " + safeArgs);

                using (var proc = new Process())
                {
                    proc.StartInfo = psi;
                    proc.Start();
                    proc.StandardInput.Close();

                    // Drain both pipes while waiting, so output cannot block cancellation.
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    var stderrTask = proc.StandardError.ReadToEndAsync();

                    // Cooperative cancellation while waiting for the CLI to finish.
                    while (!proc.WaitForExit(250))
                    {
                        if (checkCancel && CancelRequested)
                        {
                            try
                            {
                                proc.Kill();
                            }
                            catch
                            {
                            }
                            proc.WaitForExit(5000);
                            try
                            {
                                Task.WaitAll(new Task[] { stdoutTask, stderrTask }, 5000);
                            }
                            catch
                            {
                                // Pipe closure must not hide the cancellation result.
                            }
                            throw new OperationCanceledException("Extraction cancelled.");
                        }
                    }

                    string stdout = stdoutTask.GetAwaiter().GetResult();
                    string stderr = stderrTask.GetAwaiter().GetResult();

                    if (proc.ExitCode != 0 && proc.ExitCode != 1)
                    {
                        string tail = (stderr + " " + stdout).Trim();
                        if (tail.Length > 300)
                            tail = tail.Substring(tail.Length - 300);
                        throw new ApplicationException("7-Zip failed with exit code " + proc.ExitCode + ": " + tail);
                    }
                    return stdout;
                }
            }

            #endregion

            private static void EnsureExtractWithinQuota(long totalUncompressed, int entryCount)
            {
                if (entryCount > MaxExtractEntries)
                {
                    throw new InvalidOperationException("Archive rejected: too many entries (" + entryCount + ").");
                }
                if (totalUncompressed < 0L || totalUncompressed > MaxExtractTotalBytes)
                {
                    throw new InvalidOperationException("Archive rejected: uncompressed size exceeds safety limit.");
                }
            }

            /// <summary>
        /// Preserve existing files under a free suffix, regardless of their length.
        /// Returns the path the entry must be WRITTEN to: the original name when the
        /// existing file was renamed aside (or no file existed), or the free
        /// "name (2).ext"-style path when the rename itself failed and the existing
        /// file must stay untouched.
        /// </summary>
            private string ResolveEntryDestination(string dest, bool requireOriginalName = false)
            {
                if (!File.Exists(PathGuard.ArchiveIoPath(dest)))
                    return dest;

                string kept = GetNonCollidingPath(dest);
                try
                {
                    File.Move(PathGuard.ArchiveIoPath(dest), PathGuard.ArchiveIoPath(kept));
                    PreservedCopies.Add(new KeyValuePair<string, string>(dest, kept));
                    Log.WriteWarning("Extraction: preserving existing file '" + dest + "' as '" + kept + "'.");
                    // The rename freed the original name — that is where the entry goes.
                    // (Returning `kept` here would overwrite the very file we just
                    // preserved.)
                    return dest;
                }
                catch (Exception ex)
                {
                    // Locked / ACLs / ...: do not destroy the file either — extract the
                    // entry to the free name and leave the existing file untouched.
                    if (requireOriginalName)
                        throw new IOException("Could not preserve the existing file before extraction: " + dest, ex);
                    Log.WriteError("Extraction: could not rename '" + dest + "' (" + Log.SafeException(ex) + "); extracting to '" + kept + "' instead.");
                }
                return kept;
            }

            /// <summary>First free "name (2).ext"-style path next to dest (9999 tries, then
        /// a short GUID suffix) — mirrors FileDownloader.RenamePartToReal.</summary>
            private static string GetNonCollidingPath(string dest)
            {
                string dir = Path.GetDirectoryName(dest);
                string name = Path.GetFileName(dest);
                int dot = name.LastIndexOf('.');
                string baseName = dot > 0 ? name.Substring(0, dot) : name;
                string ext = dot > 0 ? name.Substring(dot) : "";
                int i = 1;
                do
                {
                    i += 1;
                    string candidate = Path.Combine(dir, baseName + " (" + i + ")" + ext);
                    string ioPath = PathGuard.ArchiveIoPath(candidate);
                    if (!File.Exists(ioPath) && !Directory.Exists(ioPath))
                        return candidate;
                    if (i >= 9999)
                    {
                        return Path.Combine(dir, baseName + " (" + Guid.NewGuid().ToString("N").Substring(0, 8) + ")" + ext);
                    }
                }
                while (true);
            }

            private void CreateOutputDirectory(string path)
            {
                string ioPath = PathGuard.ArchiveIoPath(path);
                if (Directory.Exists(ioPath))
                    return;
                string parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent))
                    CreateOutputDirectory(parent);
                Directory.CreateDirectory(ioPath);
                CreatedDirectories.Add(path);
            }

            private void ThrowIfCancelled()
            {
                if (CancelRequested)
                    throw new OperationCanceledException("Extraction cancelled.");
            }

            private void WriteEntry(Stream source, string dest, DateTime? modified)
            {
                CreateOutputDirectory(Path.GetDirectoryName(dest));
                dest = ResolveEntryDestination(dest);
                string ioPath = PathGuard.ArchiveIoPath(dest);
                using (var output = new FileStream(ioPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    CreatedFiles.Add(dest);
                    var buffer = new byte[65536];
                    do
                    {
                        ThrowIfCancelled();
                        int count = source.Read(buffer, 0, buffer.Length);
                        if (count == 0)
                            break;
                        output.Write(buffer, 0, count);
                    }
                    while (true);
                }
                if (modified.HasValue)
                    File.SetLastWriteTime(ioPath, modified.Value);
            }
        }


        #endregion

    }
}