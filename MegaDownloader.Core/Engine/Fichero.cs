using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public class Fichero : IDescarga, IDisposable
    {


        public const string HIDDEN_LINK = "{HIDDEN}";
        public const string HIDDEN_LINK_DESC = "** LINK NOT VISIBLE **";

        public SecureString _FileID; // Codificado!!

        public SecureString _FileKey; // Codificado!!

        public SecureString _URL; // Codificado!!

        public SecureString _URLFichero; // Codificado!!

        public string NombreFichero; // Solo el nombre del fichero, sin ruta

        /// <summary>Selected MEGA account for this task; empty means anonymous.</summary>
        public string MegaAccountEmail = string.Empty;

        public string MegaAccountTransferEmail = string.Empty;
        public ulong? MegaDriveNodeHandle;
        public bool MegaDriveWaitingForQuota;
        public int? MegaAccountErrorCode;

        /// <summary>
        /// Folder link this task came from when it was queued by a public-folder
        /// subscription. Empty for everything else. It lets the anonymous completion
        /// path record the delivery so the next check sees the file as downloaded.
        /// </summary>
        public string PublicFolderId = string.Empty;

        /// <summary>Handle of the folder node for a public-folder task.</summary>
        public ulong? PublicFolderHandle;

        public string EffectiveMegaAccountEmail =>
            MegaDriveNodeHandle.HasValue || string.IsNullOrEmpty(MegaAccountTransferEmail)
                ? MegaAccountEmail
                : MegaAccountTransferEmail;

        internal bool IsMegaAccountConnecting =>
            UsesMegaAccount && MegaAccountService.Current.GetStatus(EffectiveMegaAccountEmail).Connecting;

        internal bool HasRecordedMegaAccountOutput =>
            UsesMegaAccount && Porcentaje == 100m && !string.IsNullOrEmpty(completedFileName);

        public bool UsesMegaAccount => MegaDriveNodeHandle.HasValue || !string.IsNullOrEmpty(MegaAccountEmail);

        // Accept only direct public files; never turn a folder child or encoded link into one.
        private static readonly Regex AccountFilePattern = new Regex(
            "\\A(?:(?:https?://)?(?:www\\.)?mega\\.(?:co\\.)?nz/(?:file/(?<id>[A-Za-z0-9_-]{8})#|#!(?<id>[A-Za-z0-9_-]{8})!)|mega:(?:/{2,3})?#?!(?<id>[A-Za-z0-9_-]{8})!)(?<key>[A-Za-z0-9_-]{43})=?\\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string GetMegaAccountLink(string link)
        {
            Match match = AccountFilePattern.Match(link ?? string.Empty);
            if (!match.Success)
                return null;
            return "https://mega.nz/file/" + match.Groups["id"].Value + "#" + match.Groups["key"].Value;
        }

        public static bool IsPermanentMegaAccountError(int code) =>
            code == -9 || code == -11 || code == -14 || code == -16;

        // SDK internal/network, rate/connection limits, incomplete and temporary failures.
        private static readonly int[] TransientMegaAccountErrors = { -1, -3, -4, -6, -13, -18, -19 };

        public static bool IsTransientMegaAccountError(int code) =>
            Array.IndexOf(TransientMegaAccountErrors, code) >= 0;

        public string PartialFileName = FileDownloader.CreatePartialFileName();

        // Empty means no delivered output; Nothing identifies an older queue entry.
        private string completedFileName = "";

        /// <summary>B3: 本会话内已由下载器校验并交付的成品。删除时按"路径+大小+写入时间"
    /// 复核未被外部替换即可复用,不必对 GB 级文件重算一次全文件 MetaMAC。</summary>
        private string _verifiedOutputPath = null;
        private long _verifiedOutputLength = -1;
        private long _verifiedOutputWriteUtc = 0L;

        /// <summary>B4: 16 字节(4 词)公链 key 没有内嵌 MetaMAC。预热时标记出来,
    /// 由界面一次性请求用户显式确认,而不是静默把成品留在磁盘上。</summary>
        public bool OutputNeedsConfirmation = false;
        public string OutputConfirmationPath = null;
        public bool ConfirmUnverifiedOutputDeletion = false;

        /// <summary>B7: CancellationComplete 的一次性门闩,以及 Main.DisposeFichero 的幂等标记。</summary>
        private int _cancellationNotified = 0;
        public int DisposalHandled = 0;

        public string RutaLocal;

        public string RutaRelativa;

        public long TamanoBytes;

        public int NumeroConexionesAbiertas;

        public int NumeroChunksAsignados;

        public string MD5;

        public bool LinkVisible;

        public string DescripcionError;

        /// <summary>v2.5 beta: 永久失败(链接失效/key错/无权限/被封),自愈跳过,需手动处理。</summary>
        public bool EsErrorPermanente = false;

        /// <summary>v2.5 RC: 配额失败,配额期内自愈跳过,到期由配额恢复统一唤醒。RC起持久化,重启后立即重试仍可捞回。</summary>
        public bool FailedByQuota = false;

        public DateTime? FechaUltimoError;

        public decimal Porcentaje;

        public bool DescargaComenzada; // Variable auxiliar que indica si la descarga ya ha comenzado (y no se puede cambiar su configuración de ruta y demás)

        public bool DescargaProcesada; // Indica si la descarga ya ha sido procesada (se ha verificado la URL del fichero, su tamaño, etc)

        public bool ExtraccionFicheroAutomatica;

        public string ExtraccionFicheroPassword;

        public int Prioridad;

        public decimal VelocidadKBs;

        public long BytesDescargados;

        public Estado EstadoDescarga;

        public bool MarcadoParaBorrarFicheroLocal;

        /// <summary>删盘时顺带清理空目录用的包上下文(Eliminar 在 Stop 前写入,
	/// 	''' DisposeFichero 在异步删盘后消费一次即清)。平时始终为 Nothing。</summary>
        public Paquete LimpiezaPaquete = null;

        private FileDownloader _Downloader;

        private FileDownloader Downloader
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _Downloader;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                _Downloader = value;
            }
        }

        public event EventHandler CancellationComplete;

        public string TiempoEstimadoDescarga;

        public bool PausaIndividual;

        public bool DescargaIndividual;

        public FileDownloader.DataPart DatosPartes;

        private int NumErroresChunk;
        private DateTime? FechaUltimoErrorChunk;

        public int LimiteVelocidad;

        private Exception UltimoErrorChunk;

        private int Version;

        private const int NUM_MAX_ERRORES_CHUNK = 30;
        // 必须显著大于分块退避上限(约 16.5 秒),否则真正持续失败的重试会被误判为"已恢复",
        // 保护永不触发。取 120 秒:持续失败约 8 分钟内累计到上限,几小时里的零星抖动不累积。
        private const int CHUNK_ERROR_COOLDOWN_SECONDS = 120;
        // 分块失败回调可能来自线程池(退避后新建的 worker 无 UI 同步上下文),
        // 冷却判定与计数递增必须成对原子,否则并发回调会互相覆盖。
        private readonly object _errorChunkLock = new object();
        private readonly string _extractionCode = Guid.NewGuid().ToString("N");

        public Fichero() : this("")
        {
        }

        public Fichero(string Url)
        {
            Version = 2;
            LinkVisible = true;
            URL = Url;
            URLFichero = "";
            FileID = "";
            FileKey = "";
            NombreFichero = "";
            RutaLocal = "";
            RutaRelativa = "";
            TamanoBytes = 0L;
            MD5 = "";
            Porcentaje = 0m;
            DescargaComenzada = false;
            DescargaProcesada = false;
            ExtraccionFicheroAutomatica = false;
            ExtraccionFicheroPassword = null;
            Prioridad = 0;
            VelocidadKBs = 0m;
            EstadoDescarga = Estado.EnCola;
            MarcadoParaBorrarFicheroLocal = false;
            TamanoBytes = 0L;
            BytesDescargados = 0L;
            NumErroresChunk = 0;
            PausaIndividual = false;
            DescargaIndividual = false;

        }


        private const string keyUrl = "xmpcphVkCTJ2unykcwRMBecz3jEnTX93Nv5KZGrtYK6LE3WE";
        public string URL
        {
            get
            {
                string str = Criptografia.ToInsecureString(_URL);
                if (string.IsNullOrEmpty(str) | Version > 1)
                {
                    return str;
                }
                else
                {
                    return Criptografia.AES_DecryptString(str, keyUrl);
                }
            }
            set
            {
                if (Version > 1)
                {
                    _URL = Criptografia.ToSecureString(value);
                }
                else
                {
                    _URL = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl));
                }
                CacheSecureData = null;
            }
        }

        public string URLFichero
        {
            get
            {
                string str = Criptografia.ToInsecureString(_URLFichero);
                if (string.IsNullOrEmpty(str) | Version > 1)
                {
                    return str;
                }
                else
                {
                    return Criptografia.AES_DecryptString(str, keyUrl);
                }
            }
            set
            {
                if (Version > 1)
                {
                    _URLFichero = Criptografia.ToSecureString(value);
                }
                else
                {
                    _URLFichero = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl));
                }
                CacheSecureData = null;
            }
        }


        public string FileID
        {
            get
            {
                string str = Criptografia.ToInsecureString(_FileID);
                if (string.IsNullOrEmpty(str) | Version > 1)
                {
                    return str;
                }
                else
                {
                    return Criptografia.AES_DecryptString(str, keyUrl);
                }
            }
            set
            {
                if (Version > 1)
                {
                    _FileID = Criptografia.ToSecureString(value);
                }
                else
                {
                    _FileID = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl));
                }
                CacheSecureData = null;
            }
        }


        public string FileKey
        {
            get
            {
                string str = Criptografia.ToInsecureString(_FileKey);
                if (string.IsNullOrEmpty(str) | Version > 1)
                {
                    return str;
                }
                else
                {
                    return Criptografia.AES_DecryptString(str, keyUrl);
                }
            }
            set
            {
                if (Version > 1)
                {
                    _FileKey = Criptografia.ToSecureString(value);
                }
                else
                {
                    _FileKey = Criptografia.ToSecureString(Criptografia.AES_EncryptString(value, keyUrl));
                }
                CacheSecureData = null;
            }
        }

        private class Cache
        {
            public string FileKey;
            public string FileID;
            public string URLFichero;
            public string URL;
            public SecureString FileIDSource;
            public SecureString FileKeySource;
            public SecureString URLSource;
            public SecureString URLFicheroSource;
            public bool LinkVisible;

            public bool Matches(Fichero @file)
            {
                return ReferenceEquals(FileIDSource, @file._FileID) && ReferenceEquals(FileKeySource, @file._FileKey) && ReferenceEquals(URLSource, @file._URL) && ReferenceEquals(URLFicheroSource, @file._URLFichero) && LinkVisible == @file.LinkVisible;
            }
        }
        private Cache CacheSecureData = null;

        private void CaptureCacheSources(Cache cache)
        {
            cache.FileIDSource = _FileID;
            cache.FileKeySource = _FileKey;
            cache.URLSource = _URL;
            cache.URLFicheroSource = _URLFichero;
            cache.LinkVisible = LinkVisible;
        }
        private void RegenerateCacheSecureData()
        {
            var c = new Cache();
            c.FileID = Criptografia.EncryptString_DPAPI(_FileID);
            c.FileKey = Criptografia.EncryptString_DPAPI(_FileKey);
            using (var secureUrl = Criptografia.ToSecureString((LinkVisible ? "" : HIDDEN_LINK) + Criptografia.ToInsecureString(_URL)))
            {
                c.URL = Criptografia.EncryptString_DPAPI(secureUrl);
            }
            c.URLFichero = Criptografia.EncryptString_DPAPI(_URLFichero);
            CaptureCacheSources(c);
            CacheSecureData = c;
        }

        public string DescargaNombre()
        {
            if (string.IsNullOrEmpty(NombreFichero))
            {
                return FileID;
            }
            else
            {
                return NombreFichero;
            }
        }

        public decimal DescargaPorcentaje()
        {
            return Porcentaje;
        }

        public long DescargaTamanoBytes()
        {
            return TamanoBytes;
        }

        public decimal DescargaVelocidadKBs()
        {
            return VelocidadKBs;
        }

        public Estado DescargaEstado()
        {
            return EstadoDescarga;
        }

        public Estado SetDescargaEstado
        {
            set
            {
                EstadoDescarga = value;
            }
        }


        public int DescargaPrioridad()
        {
            return Prioridad;
        }

        public int SetDescargaPrioridad
        {
            set
            {
                Prioridad = value;
            }
        }

        public bool DescargaExtraccionAutomatica()
        {
            return ExtraccionFicheroAutomatica;
        }

        public string DescargaExtraccionPassword()
        {
            return ExtraccionFicheroPassword;
        }

        public void set_SetDescargaExtraccionAutomatica(string Password, bool value)
        {
            ExtraccionFicheroAutomatica = value;
            ExtraccionFicheroPassword = Password;
        }

        public string DescargaTiempoEstimadoDescarga()
        {
            return TiempoEstimadoDescarga;
        }

        private bool _Actualizando = false;
        public void ActualizarInformacionFichero(Configuracion Config, ref Conexion.TipoError ErrorObtenido, bool ComprobacionAntesDescarga, System.Threading.CancellationToken ct = default)
        {
            bool usesAccount;
            Estado previous;
            lock (Mutex.FicheroDownloader)
            {
                if (_Actualizando || disposedValue || _accountDownload is not null)
                    return;
                usesAccount = UsesMegaAccount;
                _Actualizando = true;
                previous = EstadoDescarga;
                EstadoDescarga = Estado.Verificando;
            }

            try
            {
                if (usesAccount)
                {
                    VerifyMegaAccountFile(ref ErrorObtenido, ct);
                    return;
                }

                var Info = Conexion.ObtenerInformacionFichero(Config, FileID, FileKey, ComprobacionAntesDescarga, ct);
                // B2-⑩:字段批量回写必须持 FicheroDownloader 锁,与 GuardarXML/ActualizarDatosDescarga
                // 同锁互斥,否则关机存盘会读到半新半旧的撕裂对象(新 Key+旧 Size+旧分片表)。
                // 网络 IO 已在锁外完成,此处仅内存赋值,持锁极短无性能影响。
                lock (Mutex.FicheroDownloader)
                {
                    if (Info is not null && Info.Err == Conexion.TipoError.SinErrores)
                    {
                        if (!string.IsNullOrEmpty(Info.URL))
                            URLFichero = Info.URL;
                        // F5: a verified 0-byte file must store 0, not keep a stale size.
                        TamanoBytes = Math.Max(0L, Info.Tamano);
                        if (!string.IsNullOrEmpty(Info.Nombre) && (DatosPartes is null || !DatosPartes.AllFinished || string.IsNullOrEmpty(NombreFichero)))
                        {
                            // A completed task may already use a collision suffix on disk.
                            // Keep that name while recovering extraction/verification.
                            NombreFichero = PathGuard.SanitizeFileName(Info.Nombre, "file");
                        }
                        MD5 = Info.MD5;
                        DescargaProcesada = true;
                        SetDescargaEstado = previous;
                        FileKey = Info.FileKey;
                        FileID = Info.FileID;
                    }
                    else if (Info is not null && Info.Err != Conexion.TipoError.SinErrores)
                    {
                        ErrorObtenido = Info.Err;
                        if (Info.Err == Conexion.TipoError.QuotaExceeded)
                        {
                            EstablecerError("MEGA transfer quota exceeded (EOVERQUOTA / HTTP 509)." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Internal info: " + Info.Errtxt, false, true);

                        }
                        else
                        {
                            EstablecerError("The file could not be verified." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Error type: " + Info.Err.ToString() + Constants.vbNewLine + " * Internal info: " + Info.Errtxt);


                        }
                    }
                }
            }
            finally
            {
                lock (Mutex.FicheroDownloader)
                {
                    if (EstadoDescarga == Estado.Verificando)
                        EstadoDescarga = previous;
                    _Actualizando = false;
                }
            }
        }



        public static string ExtraerFileKey(string URL)
        {
            return URLExtractor.ExtraerFileKey(URL);
        }

        public static string ExtraerFileID(string URL)
        {
            return URLExtractor.ExtraerFileID(URL);
        }

        public void BorrarFicheroLocal()
        {
            if (!MarcadoParaBorrarFicheroLocal)
                return;
            string Ruta = string.Empty;
            Exception errorException = null;
            try
            {
                if (string.IsNullOrWhiteSpace(RutaLocal) || string.IsNullOrWhiteSpace(NombreFichero))
                {
                    throw new InvalidOperationException("Local path or file name is missing.");
                }
                // B3: 归属判定可能要读整个文件算 MetaMAC(GB 级可达数十秒),必须放在
                // Mutex.DeletingFiles 之外,否则一次删除会阻塞所有其它删除。
                // 界面入口会先在后台线程 PrepareOutputOwnership() 预热,这里通常命中缓存。
                string completado = GetCompletedFileForDeletion();
                Ruta = completado ?? string.Empty;
                string parcial = UsesMegaAccount && string.IsNullOrEmpty(PartialFileName)
                    ? null
                    : FileDownloader.GetPartialFilePath(RutaLocal, NombreFichero, PartialFileName);
                lock (Mutex.DeletingFiles)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(completado))
                        {
                            if (System.IO.File.Exists(completado))
                                System.IO.File.Delete(completado);
                            completedFileName = "";
                        }
                        if (System.IO.File.Exists(parcial))
                        {
                            Ruta = parcial;
                            System.IO.File.Delete(parcial);
                        }
                    }
                    catch (Exception e)
                    {
                        errorException = e;
                    }
                }
                if (errorException is not null)
                {
                    Log.WriteError("Error deleting local files - Path: " + Ruta + " - Error: " + errorException.ToString());
                    UserNotification.ShowError(Language.GetText("Error deleting local files - Path: %PATH% - Error: %ERROR%").Replace("%PATH%", Ruta).Replace("%ERROR%", errorException.Message), Language.GetText("Error"));
                }
            }
            catch (Exception e)
            {
                Log.WriteError("Error deleting local files - Path: " + Ruta + " - Error: " + e.ToString());
            }
        }

        private string CompletedOutputPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || (System.IO.Path.GetFileName(fileName) ?? "") != (fileName ?? "") || fileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Invalid completed file name.");
            }
            // Do not sanitize an actual collision-suffixed name into a different path.
            string result = System.IO.Path.GetFullPath(System.IO.Path.Combine(RutaLocal, fileName));
            PathGuard.EnsurePathUnderRoot(RutaLocal, result, allowRoot: false);
            return result;
        }

        private string GetCompletedFileForDeletion()
        {
            if (!string.IsNullOrEmpty(completedFileName))
                return CompletedOutputPath(completedFileName);

            // B3: 本会话内下载器已完成校验的成品,只要路径/大小/写入时间未变就直接复用,
            // 不再对同一个 GB 级文件重算一次全文件 MetaMAC。必须放在旧队列守卫之前:
            // 新建任务的 completedFileName 是 ""(非 Nothing),否则会被提前拦掉。
            if (!string.IsNullOrEmpty(_verifiedOutputPath))
            {
                try
                {
                    string verifiedPath = CompletedOutputPath(System.IO.Path.GetFileName(_verifiedOutputPath));
                    if (string.Equals(verifiedPath, _verifiedOutputPath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(verifiedPath))
                    {
                        var verifiedInfo = new System.IO.FileInfo(verifiedPath);
                        if (verifiedInfo.Length == _verifiedOutputLength && verifiedInfo.LastWriteTimeUtc.Ticks == _verifiedOutputWriteUtc)
                        {
                            completedFileName = System.IO.Path.GetFileName(verifiedPath);
                            return verifiedPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Verified output can no longer be reused for " + FileID + ": " + Log.SafeException(ex));
                }
            }

            if (UsesMegaAccount || completedFileName is not null || !DescargaComenzada)
                return null;
            // Older queues have no output marker. Require a real MetaMAC match before
            // treating a same-named existing file as their completed output.
            if (EstadoDescarga != Estado.Completado && EstadoDescarga != Estado.Descomprimiendo && EstadoDescarga != Estado.EsperandoVolumenes && EstadoDescarga != Estado.ComprobandoMD5 && (DatosPartes is null || !DatosPartes.AllFinished))
                return null;

            OutputNeedsConfirmation = false;
            OutputConfirmationPath = null;
            try
            {
                string candidate = CompletedOutputPath(NombreFichero);
                if (!System.IO.File.Exists(candidate) || new System.IO.FileInfo(candidate).Length != TamanoBytes)
                    return null;
                string key = FileKey;
                if (key.Contains("=###n="))
                    key = key.Substring(0, key.IndexOf("=###n="));
                if (Criptografia.base64urldecodeBytes(key).Length != 32)
                {
                    // B4: 4 词公链 key(16 字节)没有内嵌 MetaMAC,原实现在此静默返回 Nothing,
                    // 用户以为删掉了成品,文件却一直留在磁盘上。前置条件已经限定
                    // "任务确实完成 + 同名 + 大小精确一致 + 下载已开始",交由用户显式确认。
                    if (!ConfirmUnverifiedOutputDeletion)
                    {
                        OutputNeedsConfirmation = true;
                        OutputConfirmationPath = candidate;
                        Log.WriteWarning("Legacy link has no MetaMAC; keeping output pending explicit confirmation: " + candidate);
                        return null;
                    }
                    completedFileName = NombreFichero;
                    return candidate;
                }
                if (!Criptografia.VerifyMegaMetaMac(candidate, key))
                    return null;
                completedFileName = NombreFichero;
                return candidate;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Keeping unverified legacy output for " + FileID + ": " + Log.SafeException(ex));
                return null;
            }
        }

        /// <summary>B3: 界面在执行删除前,先在后台线程预热归属判定(可能整文件读),
    /// 校验结果写入 completedFileName 缓存,随后 UI 线程的删除就是 O(1)。</summary>
        public void PrepareOutputOwnership()
        {
            try
            {
                GetCompletedFileForDeletion();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Output ownership precheck failed for " + FileID + ": " + Log.SafeException(ex));
            }
        }

        /// <summary>B3/B6: 记录下载器已验证并交付的成品元数据,供删除时复用。</summary>
        public void RecordVerifiedOutput(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return;
                string full = System.IO.Path.GetFullPath(path);
                var info = new System.IO.FileInfo(full);
                _verifiedOutputPath = full;
                _verifiedOutputLength = info.Length;
                _verifiedOutputWriteUtc = info.LastWriteTimeUtc.Ticks;
            }
            catch (Exception ex)
            {
                _verifiedOutputPath = null;
                Log.WriteWarning("Could not cache verified output: " + Log.SafeException(ex));
            }
        }

        public string ObtenerNombreSinExtension()
        {

            if (!string.IsNullOrEmpty(NombreFichero) && NombreFichero.Contains("."))
            {
                string str = NombreFichero.Substring(0, NombreFichero.LastIndexOf("."));
                if (str.LastIndexOf(".part") >= 0 && Information.IsNumeric(str.Substring(str.LastIndexOf(".part") + 5)))
                {
                    // Ficheros .part01.rar, .part02.rar, etc
                    str = str.Substring(0, str.LastIndexOf(".part"));
                }
                return str;
            }
            else
            {
                return NombreFichero;
            }

        }

        #region Downloader

        private CancellationTokenSource _accountDownload;
        private Estado? _accountCancelTarget;
        private bool _discardPrivateCheckpointOnStop;

        public bool CanChangeDownloadIdentity() =>
            !disposedValue && !_Actualizando && _accountDownload is null && bgArranque is null &&
            (Downloader is null || Downloader.CanStart) &&
            (EstadoDescarga == Estado.EnCola || EstadoDescarga == Estado.Pausado || EstadoDescarga == Estado.Erroneo) &&
            string.IsNullOrEmpty(completedFileName);

        /// <summary>The caller holds the downloader lock across the selected tasks.</summary>
        public void ChangeDownloadIdentity(string email)
        {
            if (string.Equals(MegaAccountEmail, email, StringComparison.OrdinalIgnoreCase) && MegaAccountTransferEmail.Length == 0)
                return;
            ResetearDescarga(preserveProgress: true);
            bool changeBackend = UsesMegaAccount != !string.IsNullOrEmpty(email);
            MegaAccountEmail = email ?? string.Empty;
            MegaAccountTransferEmail = string.Empty;
            if (changeBackend)
            {
                DescargaProcesada = false;
                if (UsesMegaAccount)
                    ResetMegaAccountProgress();
                else
                    ThrottledStreamController.GetController().SetMaxSpeed(FileID, LimiteVelocidad * 1024L);
            }
            if (EstadoDescarga == Estado.Erroneo || (changeBackend && EstadoDescarga == Estado.Pausado))
                EstadoDescarga = Estado.EnCola;
        }

        private int ReadMegaAccountInfo(string email, out string name, out long size)
        {
            string link = MegaDriveNodeHandle.HasValue ? null : GetMegaAccountLink(URL);
            if (link is null && !MegaDriveNodeHandle.HasValue)
            {
                name = null;
                size = 0;
                return -2;
            }
            int code = MegaAccountService.Current.Inspect(email, link, out name, out size, MegaDriveNodeHandle);
            return code == 0 && (string.IsNullOrEmpty(name) || size < 0) ? -2 : code;
        }

        public void RecordMegaAccountTransfer(string email) =>
            MegaAccountTransferEmail = string.Equals(email, MegaAccountEmail, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : email;

        private void ApplyMegaAccountInfo(string name, long size)
        {
            if (string.IsNullOrEmpty(completedFileName))
                NombreFichero = PathGuard.SanitizeFileName(name, "file");
            TamanoBytes = size;
            DescargaProcesada = true;
        }

        private void VerifyMegaAccountFile(ref Conexion.TipoError errorResult, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                string email = EffectiveMegaAccountEmail;
                var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int code;
                string name;
                long size;
                do
                {
                    ct.ThrowIfCancellationRequested();
                    tried.Add(email);
                    code = ReadMegaAccountInfo(email, out name, out size);
                    if (code != MegaAccountService.OverQuota || MegaDriveNodeHandle.HasValue)
                        break;
                    string nextEmail = MegaAccountService.Current.NextTransferAccount(email, tried);
                    if (nextEmail.Length == 0)
                        break;
                    email = nextEmail;
                }
                while (true);

                ct.ThrowIfCancellationRequested();
                lock (Mutex.FicheroDownloader)
                {
                    if (disposedValue)
                        return;
                    if (!MegaDriveNodeHandle.HasValue)
                        RecordMegaAccountTransfer(email);
                    if (code == 0)
                    {
                        ApplyMegaAccountInfo(name, size);
                    }
                    else
                    {
                        errorResult = Conexion.TipoError.Otros;
                        SetMegaAccountError(code);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.WriteWarning("MEGA account metadata failed: " + Log.SafeException(ex));
                lock (Mutex.FicheroDownloader)
                {
                    if (disposedValue)
                        return;
                    errorResult = Conexion.TipoError.Otros;
                    SetMegaAccountLocalError();
                }
            }
        }

        private void StartMegaAccountDownload()
        {
            if (!HasRecordedMegaAccountOutput && IsMegaAccountConnecting)
                return;
            lock (Mutex.FicheroDownloader)
            {
                if (disposedValue || _Actualizando || _accountDownload is not null ||
                    (EstadoDescarga != Estado.EnCola && EstadoDescarga != Estado.Pausado))
                {
                    return;
                }
                var source = new CancellationTokenSource();
                _accountDownload = source;
                _accountCancelTarget = null;
                Interlocked.Exchange(ref _cancellationNotified, 0);
                EstadoDescarga = Estado.CreandoLocal;
                DescargaComenzada = true;
                if (!MegaDriveNodeHandle.HasValue && !HasRecordedMegaAccountOutput)
                    ResetMegaAccountProgress();
                MegaDriveWaitingForQuota = false;
                FailedByQuota = false;
                NumeroConexionesAbiertas = 1;
                Task.Run(() => RunMegaAccountDownload(source));
            }
        }

        private void RunMegaAccountDownload(CancellationTokenSource source)
        {
            CancellationToken token = source.Token;
            string staging = null;
            string output = null;
            string cloudName = null;
            int code = 0;
            bool localFailure = false;
            bool privateNode = MegaDriveNodeHandle.HasValue;
            try
            {
                token.ThrowIfCancellationRequested();
                lock (Mutex.FicheroDownloader)
                {
                    if (_accountCancelTarget.HasValue)
                        return;
                    if (HasRecordedMegaAccountOutput)
                    {
                        var candidate = new System.IO.FileInfo(CompletedOutputPath(completedFileName));
                        if (candidate.Exists && candidate.Length == TamanoBytes)
                        {
                            output = candidate.FullName;
                            NombreFichero = completedFileName;
                            BytesDescargados = TamanoBytes;
                            Porcentaje = 100m;
                        }
                        else
                        {
                            completedFileName = string.Empty;
                        }
                    }
                }

                if (output is null)
                {
                    if (privateNode)
                        code = DownloadPrivateMegaFile(token, ref staging, ref cloudName);
                    else
                        code = DownloadPublicMegaAccountFile(token, ref staging);
                    if (code != 0)
                        return;
                    token.ThrowIfCancellationRequested();
                    if (new System.IO.FileInfo(staging).Length != TamanoBytes)
                        throw new System.IO.IOException("Unexpected MEGA SDK output size.");
                    lock (Mutex.FicheroDownloader)
                    {
                        if (_accountCancelTarget.HasValue)
                            return;
                        output = DeliverMegaAccountFile(staging);
                        completedFileName = System.IO.Path.GetFileName(output);
                        NombreFichero = completedFileName;
                        RecordVerifiedOutput(output);
                        BytesDescargados = TamanoBytes;
                        Porcentaje = 100m;
                    }
                    if (privateNode)
                        MegaAccountService.Current.RecordDriveDelivery(MegaAccountEmail, MegaDriveNodeHandle.Value, cloudName, output);
                }
                downloader_Completed(null, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                code = -13;
            }
            catch (Exception ex)
            {
                localFailure = true;
                Log.WriteWarning("MEGA account download failed: " + Log.SafeException(ex));
            }
            finally
            {
                if (staging is not null)
                {
                    try
                    {
                        if (!privateNode)
                            System.IO.File.Delete(staging);
                        else if (output is not null)
                            DeletePrivateMegaCheckpoint(staging);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteWarning("MEGA account staging cleanup failed: " + Log.SafeException(ex));
                    }
                }

                bool notify;
                lock (Mutex.FicheroDownloader)
                {
                    NumeroConexionesAbiertas = 0;
                    VelocidadKBs = 0m;
                    if (privateNode)
                        TiempoEstimadoDescarga = string.Empty;
                    notify = _accountCancelTarget.GetValueOrDefault(Estado.Pausado) == Estado.EnCola;
                    if (output is null)
                    {
                        if (!privateNode || _discardPrivateCheckpointOnStop)
                            ResetMegaAccountProgress();
                        if (_accountCancelTarget.HasValue)
                        {
                            EstadoDescarga = _accountCancelTarget.Value;
                        }
                        else if (localFailure)
                        {
                            SetMegaAccountLocalError();
                        }
                        else if (privateNode && code == MegaAccountService.OverQuota &&
                            MegaAccountService.Current.GetStatus(MegaAccountEmail).QuotaReached)
                        {
                            MegaDriveWaitingForQuota = true;
                            FailedByQuota = true;
                            EstadoDescarga = Estado.EnCola;
                        }
                        else
                        {
                            SetMegaAccountError(code);
                        }
                    }
                    if (_discardPrivateCheckpointOnStop)
                    {
                        TryDeletePrivateMegaCheckpoint();
                        _discardPrivateCheckpointOnStop = false;
                    }
                    _accountDownload = null;
                    _accountCancelTarget = null;
                }
                source.Dispose();
                if (notify)
                    NotifyCancellationComplete();
            }
        }

        private int DownloadPublicMegaAccountFile(CancellationToken token, ref string staging)
        {
            string link = GetMegaAccountLink(URL);
            if (link is null)
                return -2;
            MegaAccountService service = MegaAccountService.Current;
            string email = EffectiveMegaAccountEmail;
            var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            System.IO.Directory.CreateDirectory(RutaLocal);
            do
            {
                token.ThrowIfCancellationRequested();
                tried.Add(email);
                lock (Mutex.FicheroDownloader)
                {
                    RecordMegaAccountTransfer(email);
                    ResetMegaAccountProgress();
                }

                int code = 0;
                if (!DescargaProcesada)
                {
                    code = ReadMegaAccountInfo(email, out string name, out long size);
                    if (code == 0)
                    {
                        lock (Mutex.FicheroDownloader)
                            ApplyMegaAccountInfo(name, size);
                    }
                }
                if (code == 0)
                {
                    staging = PathGuard.GetSafePathUnderRoot(RutaLocal,
                        ".mega-sdk-" + Guid.NewGuid().ToString("N") + ".tmp", allowRoot: false);
                    token.ThrowIfCancellationRequested();
                    EstadoDescarga = Estado.Descargando;
                    code = service.Download(email, link, staging, UpdateMegaAccountProgress, token);
                }
                if (code != MegaAccountService.OverQuota)
                    return code;
                token.ThrowIfCancellationRequested();
                string nextEmail = service.NextTransferAccount(email, tried);
                if (nextEmail.Length == 0)
                    return code;
                if (staging is not null)
                {
                    System.IO.File.Delete(staging);
                    staging = null;
                }
                email = nextEmail;
            }
            while (true);
        }

        private int DownloadPrivateMegaFile(CancellationToken token, ref string staging, ref string cloudName)
        {
            MegaAccountService service = MegaAccountService.Current;
            string email = EffectiveMegaAccountEmail;
            ulong node = MegaDriveNodeHandle.Value;
            int code = service.InspectPrivateDownload(email, node, out cloudName, out long size, out string key);
            if (code != 0)
                return code;
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(cloudName) || size < 0)
                return -2;
            lock (Mutex.FicheroDownloader)
                ApplyMegaAccountInfo(cloudName, size);

            System.IO.Directory.CreateDirectory(RutaLocal);
            staging = PrivateMegaStagingPath();
            string binding = Utils.SecretFingerprint(email.ToUpperInvariant() + "\n" +
                node.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                size.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + key);
            using (System.IO.FileStream file = OpenPrivateMegaCheckpoint(staging, binding, size))
            {
                long offset = file.Position;
                UpdateMegaAccountProgress(offset, size, 0);
                token.ThrowIfCancellationRequested();
                EstadoDescarga = Estado.Descargando;

                byte[] buffer = null;
                long lastFlush = offset;
                Exception writeError = null;
                MegaAccountNative.StreamData append = (data, count, speed) =>
                {
                    // No managed exception can escape through the SDK callback.
                    try
                    {
                        if (token.IsCancellationRequested)
                            return 0;
                        if (count <= 0 || count > size - file.Position)
                            throw new System.IO.IOException("Unexpected MEGA stream length.");
                        if (buffer is null || buffer.Length < count)
                            buffer = new byte[count];
                        Marshal.Copy(data, buffer, 0, count);
                        file.Write(buffer, 0, count);
                        if (file.Position - lastFlush >= 8L * 1024L * 1024L)
                        {
                            file.Flush(flushToDisk: true);
                            lastFlush = file.Position;
                        }
                        UpdateMegaAccountProgress(file.Position, size, speed);
                        return 1;
                    }
                    catch (Exception ex)
                    {
                        writeError = ex;
                        return 0;
                    }
                };
                code = service.StreamPrivateDownload(email, node, key, size, offset, append, token);
                file.Flush(flushToDisk: true);
                lock (Mutex.FicheroDownloader)
                {
                    // Pause can race the last write callback. Report the flushed length,
                    // even when normal progress callbacks are already suppressed.
                    SetMegaAccountProgress(file.Length, size, 0);
                }
                if (writeError is not null)
                    throw new System.IO.IOException("Could not write the MEGA checkpoint.", writeError);
                if (code != 0)
                    return code;
                token.ThrowIfCancellationRequested();
                if (file.Length != size)
                    throw new System.IO.IOException("Incomplete MEGA streaming output.");
            }

            // Streaming decrypts but does not validate the entire file. This includes the
            // previously saved prefix, even if no new network bytes were needed this time.
            token.ThrowIfCancellationRequested();
            if (!Criptografia.VerifyMegaMetaMac(staging, key))
            {
                DeletePrivateMegaCheckpoint(staging);
                lock (Mutex.FicheroDownloader)
                    SetMegaAccountProgress(0, size, 0);
                throw new System.IO.IOException("MEGA private file failed MetaMAC verification.");
            }
            return 0;
        }

        private string PrivateMegaStagingPath()
        {
            string taskFile = FileDownloader.ValidatePartialFileName(PartialFileName);
            return PathGuard.GetSafePathUnderRoot(RutaLocal,
                System.IO.Path.GetFileNameWithoutExtension(taskFile) + ".mega.part", allowRoot: false);
        }

        private string PrivateMegaOwnerPrefix()
        {
            string source = Utils.SecretFingerprint(EffectiveMegaAccountEmail.ToUpperInvariant() + "\n" +
                MegaDriveNodeHandle.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return "MegaDownloader private partial v1 " + PartialFileName + " " + source + " ";
        }

        private static bool PrivateMegaFileExists(string path)
        {
            try
            {
                System.IO.FileAttributes attributes = System.IO.File.GetAttributes(path);
                if ((attributes & (System.IO.FileAttributes.Directory | System.IO.FileAttributes.ReparsePoint)) != 0)
                    throw new System.IO.IOException("Invalid MEGA checkpoint file type.");
                return true;
            }
            catch (System.IO.FileNotFoundException)
            {
                return false;
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                return false;
            }
        }

        private string ReadPrivateMegaBinding(string staging)
        {
            string marker = staging + ".owner";
            if (!PrivateMegaFileExists(marker))
                return null;
            string prefix = PrivateMegaOwnerPrefix();
            using (System.IO.FileStream file = System.IO.File.Open(marker, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (file.Length != prefix.Length + 44)
                    return null;
                using (var reader = new System.IO.StreamReader(file, System.Text.Encoding.ASCII))
                {
                    string text = reader.ReadToEnd();
                    if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length == prefix.Length + 44)
                        return text.Substring(prefix.Length);
                }
            }
            return null;
        }

        private System.IO.FileStream OpenPrivateMegaCheckpoint(string staging, string binding, long size)
        {
            string stored = ReadPrivateMegaBinding(staging);
            if (stored is not null && !string.Equals(stored, binding, StringComparison.Ordinal))
            {
                DeletePrivateMegaCheckpoint(staging);
                stored = null;
            }
            bool exists = PrivateMegaFileExists(staging);
            if (stored is null)
            {
                if (exists)
                    throw new System.IO.IOException("MEGA checkpoint has no task ownership marker.");
                DeleteIncompletePrivateMegaMarker(staging);
                string owner = staging + ".owner";
                string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(staging),
                    ".mega-owner-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (System.IO.FileStream marker = System.IO.File.Open(temp, System.IO.FileMode.CreateNew,
                        System.IO.FileAccess.Write, System.IO.FileShare.None))
                    {
                        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(PrivateMegaOwnerPrefix() + binding);
                        marker.Write(bytes, 0, bytes.Length);
                        marker.Flush(flushToDisk: true);
                    }
                    System.IO.File.Move(temp, owner);
                }
                finally
                {
                    if (System.IO.File.Exists(temp))
                        System.IO.File.Delete(temp);
                }
            }

            System.IO.FileStream file = System.IO.File.Open(staging,
                exists ? System.IO.FileMode.Open : System.IO.FileMode.CreateNew,
                System.IO.FileAccess.Write, System.IO.FileShare.Read);
            try
            {
                // The part is never preallocated. For an incomplete file, start at an AES
                // block boundary, replaying at most the last 15 bytes on the next range read.
                long offset = file.Length;
                if (offset > size)
                    offset = 0;
                if (offset < size)
                    offset -= offset % 16L;
                if (offset != file.Length)
                    file.SetLength(offset);
                file.Position = offset;
                return file;
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        private void RestorePrivateMegaProgress()
        {
            ResetMegaAccountProgress();
            if (string.IsNullOrWhiteSpace(RutaLocal))
                return;
            try
            {
                string staging = PrivateMegaStagingPath();
                if (ReadPrivateMegaBinding(staging) is null || !PrivateMegaFileExists(staging))
                    return;
                long length = new System.IO.FileInfo(staging).Length;
                if (length <= TamanoBytes)
                    UpdateMegaAccountProgress(length, TamanoBytes, 0);
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Could not recover MEGA private checkpoint progress: " + Log.SafeException(ex));
            }
        }

        // Older versions created the owner file before opening the partial. A torn
        // marker can be reclaimed if the partial was never created and the prefix matches.
        private void DeleteIncompletePrivateMegaMarker(string staging)
        {
            string owner = staging + ".owner";
            if (!PrivateMegaFileExists(owner))
                return;
            string prefix = PrivateMegaOwnerPrefix();
            using (System.IO.FileStream file = System.IO.File.Open(owner, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (file.Length >= prefix.Length + 44)
                    return;
                using (var reader = new System.IO.StreamReader(file, System.Text.Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false))
                {
                    string text = reader.ReadToEnd();
                    if (!prefix.StartsWith(text, StringComparison.Ordinal) &&
                        !text.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }
            System.IO.File.Delete(owner);
        }

        private void DeletePrivateMegaCheckpoint(string staging)
        {
            if (ReadPrivateMegaBinding(staging) is null)
            {
                if (!PrivateMegaFileExists(staging))
                    DeleteIncompletePrivateMegaMarker(staging);
                return;
            }
            if (PrivateMegaFileExists(staging))
                System.IO.File.Delete(staging);
            System.IO.File.Delete(staging + ".owner");
        }

        private void TryDeletePrivateMegaCheckpoint()
        {
            if (string.IsNullOrEmpty(PartialFileName) || string.IsNullOrWhiteSpace(RutaLocal))
                return;
            try
            {
                DeletePrivateMegaCheckpoint(PrivateMegaStagingPath());
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Could not delete MEGA private checkpoint: " + Log.SafeException(ex));
            }
        }

        // Task removal and full reset discard owned staging; ordinary pause/stop/Dispose do not.
        public void DiscardPrivateMegaCheckpoint()
        {
            if (!MegaDriveNodeHandle.HasValue)
                return;
            lock (Mutex.FicheroDownloader)
            {
                MegaDriveWaitingForQuota = false;
                if (_accountDownload is null)
                    TryDeletePrivateMegaCheckpoint();
                else
                {
                    _discardPrivateCheckpointOnStop = true;
                    CancelMegaAccountDownload(Estado.EnCola);
                }
            }
        }

        private void UpdateMegaAccountProgress(long received, long total, long speed)
        {
            lock (Mutex.FicheroDownloader)
            {
                if (_accountCancelTarget.HasValue)
                    return;
                SetMegaAccountProgress(received, total, speed);
            }
        }

        private void SetMegaAccountProgress(long received, long total, long speed)
        {
            BytesDescargados = Math.Max(0L, Math.Min(received, total));
            Porcentaje = total > 0 ? (decimal)BytesDescargados * 100m / total : 0m;
            VelocidadKBs = (decimal)Math.Max(0L, speed) / 1024m;
        }

        public static string MegaAccountCollisionLeaf(string leaf, int suffix)
        {
            int dot = leaf.LastIndexOf('.');
            string ext = dot > 0 ? leaf.Substring(dot) : string.Empty;
            string stem = dot > 0 ? leaf.Substring(0, dot) : leaf;
            string tail = " (" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")" + ext;
            int keep = 240 - tail.Length;
            return keep < 1 ? null : stem.Substring(0, Math.Min(stem.Length, keep)) + tail;
        }

        private string DeliverMegaAccountFile(string staging)
        {
            string leaf = PathGuard.SanitizeFileName(NombreFichero, "file");
            string candidate = CompletedOutputPath(leaf);
            int suffix = 1;
            do
            {
                if (!string.Equals(candidate, staging, StringComparison.OrdinalIgnoreCase) &&
                    !System.IO.Directory.Exists(candidate))
                {
                    try
                    {
                        // File.Move never replaces an existing file, including one just delivered by the anonymous engine.
                        System.IO.File.Move(staging, candidate);
                        return candidate;
                    }
                    catch (System.IO.IOException) when (System.IO.File.Exists(candidate) || System.IO.Directory.Exists(candidate))
                    {
                    }
                }
                suffix++;
                candidate = CompletedOutputPath(MegaAccountCollisionLeaf(leaf, suffix) ?? Guid.NewGuid().ToString("N"));
            }
            while (true);
        }

        private void ResetMegaAccountProgress()
        {
            BytesDescargados = 0L;
            Porcentaje = 0m;
            VelocidadKBs = 0m;
            TiempoEstimadoDescarga = string.Empty;
        }

        private void SetMegaAccountError(int code)
        {
            string message = MegaAccountService.ErrorText(code, connecting: true);
            if (MegaDriveNodeHandle.HasValue)
            {
                if (code == -9 || code == -11 || code == -14)
                    message = Language.GetText("MegaDrive_FileUnavailable");
                if (code == MegaAccountService.BackendMissing)
                    message = Language.GetText("MegaDrive_BackendMissing");
            }
            EstablecerError(message, IsPermanentMegaAccountError(code), code == MegaAccountService.OverQuota, code);
        }

        private void SetMegaAccountLocalError() =>
            EstablecerError(Language.GetText("MegaAccount_LocalFileError"), false, false);

        private void CancelMegaAccountDownload(Estado target, bool disposing = false)
        {
            bool notify = false;
            lock (Mutex.FicheroDownloader)
            {
                if (disposing)
                    disposedValue = true;
                if (_accountDownload is not null)
                {
                    if (target == Estado.EnCola || !_accountCancelTarget.HasValue)
                        _accountCancelTarget = target;
                    // The SDK polls the token; staging cleanup and cancellation notification happen on its worker.
                    _accountDownload.Cancel();
                }
                else if (!disposing && target == Estado.EnCola)
                {
                    EstadoDescarga = Estado.EnCola;
                    notify = true;
                }
            }
            if (notify)
                NotifyCancellationComplete();
        }

        private class DownloadWorker : BackgroundWorker
        {


            public DownloadWorker(ref Configuracion Configuration, int NumeroConexionesDisponibles)
            {
                NumConexionesDisponibles = NumeroConexionesDisponibles;
                Config = Configuration;
            }
            public Configuracion Config;
            public int NumConexionesDisponibles;
            public Paquete Package;
            public bool FirstStart;
        }

        // Usamos un worker para iniciar la descarga en segundo plano, ya que es necesario llamar a ActualizarInformacionFichero pero esta función
        // puede tardar mucho y así no bloqueamos el UI
        private DownloadWorker _bgArranque;

        private DownloadWorker bgArranque
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _bgArranque;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_bgArranque != null)
                {
                    _bgArranque.DoWork -= bgArranque_DoWork;
                    _bgArranque.RunWorkerCompleted -= bgArranque_RunWorkerCompleted;
                }

                _bgArranque = value;
                if (_bgArranque != null)
                {
                    _bgArranque.DoWork += bgArranque_DoWork;
                    _bgArranque.RunWorkerCompleted += bgArranque_RunWorkerCompleted;
                }
            }
        }
        // B1-④:验证阶段取消标志。bgArranque.WorkerSupportsCancellation=False,Stop/Dispose
        // 拦不住 threadpool 上的验证;无此标志时 Stop 后 DoWork 照样 New FileDownloader+Start() 复活。
        // Boolean 读写原子,Stop 还有 5s 轮询,可见性足够,无需加锁。
        private bool _StartupCancelled = false;
        private void bgArranque_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {


                DownloadWorker worker = (DownloadWorker)sender;
                CaptureDownloaderProgress();

                var ErrAct = Conexion.TipoError.SinErrores;
                ActualizarInformacionFichero(worker.Config, ref ErrAct, true);

                if (ErrAct != Conexion.TipoError.SinErrores)
                {
                    // La función ActualizarInformacionFichero ya ha actualizado el estado de la descarga, no hacemos nada
                    return;
                }

                // B1-④:验证耗时(60s API 超时/大文件夹解密)期间用户已 Stop/关闭:直接退出,
                // 不得再建下载器(此前必复活)。
                if (_StartupCancelled)
                {
                    Log.WriteWarning("Startup cancelled during file info verification, not starting downloader for " + FileID);
                    EstadoDescarga = Estado.EnCola;
                    return;
                }

                worker.Package?.ApplyVerifiedName(this, worker.FirstStart);
                NumErroresChunk = 0;
                // B8: "清空 + 装配"必须在同一把锁下完成,后续全程使用局部引用 dl。
                // downloader_Canceled 会在 Mutex.FicheroDownloader 内把 Me.Downloader 置 Nothing;
                // 若上一轮 Canceled 恰好落在本轮 ActualizarInformacionFichero(最长 60s)之后、
                // 装配之前,旧代码从 :478 起就会 NullReferenceException 并被引擎转成 Erroneo。
                FileDownloader dl = null;
                lock (Mutex.FicheroDownloader)
                {
                    dl = Downloader;
                    if (dl is null)
                    {
                        dl = new FileDownloader(true);
                        dl.Resumed += downloader_Started;
                        dl.Started += downloader_Started;
                        dl.Paused += downloader_Paused;
                        dl.CancelRequested += downloader_CancelRequested;
                        dl.Canceled += downloader_Canceled;
                        dl.Completed += downloader_Completed;
                        dl.FileDownloadSucceeded += downloader_FileDownloadSucceeded;
                        dl.FileDownloadFailed += downloader_FileDownloadFailed;
                        dl.ChunkDownloadFailed += downloader_ChunkDownloadFailed;
                        dl.FileLocalCreated += downloader_FileLocalCreated;
                        dl.FileRenamed += downloader_FileRenamed;
                        Downloader = dl;
                    }
                }

                dl.setBufferAndPackageSize(worker.Config.TamanoBufferKB * 1024, worker.Config.TamanoPaqueteKB * 1024);

                // dl.PackageSize = worker.Config.TamanoPaqueteKB * 1024
                // dl.BufferSize = worker.Config.TamanoBufferKB * 1024
                dl.StopWatchCyclesAmount = 30;
                dl.LocalDirectory = RutaLocal;
                dl.DeleteFilesAfterCancel = false;
                dl.DeleteCompletedFilesAfterCancel = false;
                dl.PartsPerFile = worker.Config.ConexionesPorFichero;
                dl.NumConnections = worker.Config.ConexionesPorFichero < worker.NumConexionesDisponibles ? worker.Config.ConexionesPorFichero : worker.NumConexionesDisponibles;
                dl.AddFileInfo(FileID, FileKey, URLFichero, DescargaNombre(), DatosPartes, PartialFileName);
                PartialFileName = dl.File.PartialFileName;

                // B1-④:装配完成到 Start 之间的取消窗口同样要拦,否则建完即启动,前功尽弃。
                if (_StartupCancelled)
                {
                    Log.WriteWarning("Startup cancelled just before downloader start, disposing it for " + FileID);
                    try
                    {
                        dl.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error disposing cancelled startup downloader: " + Log.SafeException(ex));
                    }
                    // B8: 只清自己装配的实例,别把取消窗口里刚换上的新 downloader 抹掉。
                    lock (Mutex.FicheroDownloader)
                    {
                        if (ReferenceEquals(Downloader, dl))
                            Downloader = null;
                    }
                    EstadoDescarga = Estado.EnCola;
                    return;
                }
                if (dl.CanStart)
                {
                    dl.Start();
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Error on bgArranque_DoWork: " + Log.SafeException(ex));
                EstablecerError("The download could not be started." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Internal info: " + Log.SafeException(ex));

            }
        }
        private void bgArranque_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error is not null)
            {
                Log.WriteError("bgArranque completed with error: " + Log.SafeException(e.Error));
                if (EstadoDescarga != Estado.Erroneo)
                {
                    EstablecerError("The download could not be started." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Internal info: " + Log.SafeException(e.Error));

                }
            }
            DownloadWorker worker = sender as DownloadWorker;
            if (worker is not null)
                worker.Dispose();
            if (ReferenceEquals(bgArranque, worker))
                bgArranque = null;
        }


        public void Start(ref Configuracion Config, int NumConexionesDisponibles, Paquete package = null)
        {
            lock (Mutex.FicheroDownloader)
            {
                if (UsesMegaAccount)
                {
                    StartMegaAccountDownload();
                    return;
                }
                if (disposedValue || Estado.Erroneo == DescargaEstado())
                    return;

                if (bgArranque is null && (Downloader is null || Downloader.CanStart))
                {
                    bool firstStart = !DescargaComenzada;
                    EstadoDescarga = Estado.CreandoLocal;
                    DescargaComenzada = true;
                    _StartupCancelled = false;
                    // B7: 新一轮下载开始,允许后续取消再通知一次删除回调。
                    System.Threading.Interlocked.Exchange(ref _cancellationNotified, 0);
                    bgArranque = new DownloadWorker(ref Config, NumConexionesDisponibles) { Package = package, FirstStart = firstStart };
                    bgArranque.WorkerSupportsCancellation = false;
                    bgArranque.WorkerReportsProgress = false;
                    bgArranque.RunWorkerAsync();
                }
            }
        }

        private void CaptureDownloaderProgress()
        {
            lock (Mutex.FicheroDownloader)
            {
                if (Downloader is null || Downloader.File is null)
                    return;
                var @file = Downloader.File;
                if (@file.DataPartInitialized && @file.Size > 0L)
                    DatosPartes = @file.GetDataPart;
                PartialFileName = @file.PartialFileName;
            }
        }

        public void Resume()
        {
            if (UsesMegaAccount)
            {
                StartMegaAccountDownload();
                return;
            }
            if (Downloader is not null && Downloader.CanResume)
            {
                ThrottledStreamController.GetController().Continuar(FileID);
                Downloader.Resume();
            }
        }

        public void Pause()
        {
            if (UsesMegaAccount)
            {
                CancelMegaAccountDownload(Estado.Pausado);
                return;
            }
            if (Downloader is not null && Downloader.CanPause)
            {
                Downloader.Pause();
            }
        }

        public void Stop()
        {
            if (UsesMegaAccount)
            {
                Interlocked.Exchange(ref _cancellationNotified, 0);
                CancelMegaAccountDownload(Estado.EnCola);
                return;
            }
            // B1-④:先置验证取消标志,再等。bgArranque 不可 CancelAsync,只能靠标志自停。
            _StartupCancelled = true;
            // B7: 每次显式 Stop 都开启一个新的通知周期。同一次取消里随后到达的
            // downloader_Canceled 会被门闩挡住,但下一次 Stop(例如删除任务)仍能通知到。
            System.Threading.Interlocked.Exchange(ref _cancellationNotified, 0);
            int CiclosMax = 100; // 100*50 = 5 segundos
            int Ciclos = 0;
            while (_Actualizando & Ciclos < CiclosMax)
            {
                System.Threading.Thread.Sleep(50);
                Ciclos += 1;
            }
            if (Downloader is null || Downloader.CanStart)
            {
                EstadoDescarga = Estado.EnCola;
                NotifyCancellationComplete();
            }
            else if (Downloader.CanStop)
            {
                Downloader.Stop();
            }
        }

        /// <summary>Deliver at most one completion notification per cancellation cycle.</summary>
        private void NotifyCancellationComplete()
        {
            if (System.Threading.Interlocked.Exchange(ref _cancellationNotified, 1) == 0)
            {
                CancellationComplete?.Invoke(this, new EventArgs());
            }
        }

        public void DescompresionFinalizada(bool success = true, string errorMessage = "")
        {
            if (EstadoDescarga == Estado.Descomprimiendo || EstadoDescarga == Estado.EsperandoVolumenes)
            {
                if (success)
                {
                    EstadoDescarga = Estado.Completado;
                }
                else
                {
                    string detail = string.IsNullOrEmpty(errorMessage) ? "" : Constants.vbNewLine + " * Reason: " + errorMessage;
                    EstablecerError("Automatic extraction failed or was cancelled." + Constants.vbNewLine + " * File code: " + FileID + detail);
                }
            }
        }

        /// <summary>分卷尚未到齐:解压控制器已登记等待并会自行重试。
	/// 	''' 这不是失败,也不能留在 Descomprimiendo(关闭/自动关机会误判)。</summary>
        public void DescompresionDiferida()
        {
            if (EstadoDescarga == Estado.Descomprimiendo || EstadoDescarga == Estado.EsperandoVolumenes)
            {
                EstadoDescarga = Estado.EsperandoVolumenes;
            }
        }



        private void ExtractionStateChanged(bool deferred, bool success, string message)
        {
            if (disposedValue)
                return;
            if (deferred)
            {
                DescompresionDiferida();
            }
            else
            {
                DescompresionFinalizada(success, message);
            }
        }

        internal void RemovePendingExtraction()
        {
            DescompresorController.GetController().RemoveDownload(_extractionCode);
        }

        private void downloader_Started(object sender, EventArgs e)
        {
            EstadoDescarga = Estado.Descargando;
            DescargaComenzada = true;
        }

        private void downloader_Paused(object sender, EventArgs e)
        {
            EstadoDescarga = Estado.Pausado;
        }

        private void downloader_CancelRequested(object sender, EventArgs e)
        {
            EstadoDescarga = Estado.Pausado;
        }

        private void downloader_FileLocalCreated(object sender, EventArgs e)
        {
            EstadoDescarga = Estado.Descargando;
        }

        /// <summary>下载器落盘终名回写(重名时为 "name (2).ext" 这类真实名)。
	/// 	''' 故意不取任何锁:事件在 FileDownloader.MutexFile 锁外同步触发,而
	/// 	''' ActualizarDatosDescarga 的锁序是反方向(FicheroDownloader -&gt; 下载器内部锁),
	/// 	''' 取锁即 AB-BA 死锁;字符串引用赋值原子,与其它 handler 直接赋值同规矩。</summary>
        private void downloader_FileRenamed(object sender, FileRenamedEventArgs e)
        {
            try
            {
                if (e is null || string.IsNullOrEmpty(e.FileName))
                    return;
                CompletedOutputPath(e.FileName);
                completedFileName = e.FileName;
                if (!string.Equals(NombreFichero, e.FileName, StringComparison.Ordinal))
                {
                    Log.WriteInfo("Final file name for " + FileID + ": " + e.FileName);
                    NombreFichero = e.FileName;
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("downloader_FileRenamed failed: " + Log.SafeException(ex));
            }
        }

        private void downloader_FileDownloadFailed(object sender, Exception e)
        {
            // 过期失败免疫：成功与失败经不同 worker 编组，到达顺序不定；文件已交付成功后，
            // 任何后到的失败都是过期事件，直接丢弃。注意与成功侧对称：成功守卫 <>Erroneo
            // （失败优先），失败侧守卫 <>Completado（成功终态优先）——两者都不翻转对方的终态。
            if (EstadoDescarga == Estado.Completado)
            {
                Log.WriteWarning("downloader_FileDownloadFailed: stale failure after success ignored for " + FileID + ": " + Log.SafeException(e));
                return;
            }
            EstadoDescarga = Estado.Erroneo;
            string Mensaje = "";
            try
            {
                // Si es un webexception intentamos sacar lo que ha devuelto el servidor...
                if (e is System.Net.WebException && ((System.Net.WebException)e).Response is not null)
                {
                    using (var Stream = ((System.Net.WebException)e).Response.GetResponseStream())
                    {
                        using (var reader = new System.IO.StreamReader(Stream))
                        {
                            Mensaje = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("downloader_FileDownloadFailed: could not read server error response: " + Log.SafeException(ex));
            }
            bool esQuota = e is MegaQuotaExceededException || FileDownloader.ReportQuotaIfMatch(e);
            if (esQuota)
            {
                EstablecerError("MEGA transfer quota exceeded (EOVERQUOTA / HTTP 509)." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Server response: " + Mensaje + Constants.vbNewLine + " * Internal info: " + e.ToString(), false, true);


            }
            else
            {
                EstablecerError("File download failed." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Error type: An error occurred while trying to download the file." + Constants.vbNewLine + " * Server response: " + Mensaje + Constants.vbNewLine + " * Internal info: " + e.ToString());



            }
            Log.WriteError(DescripcionError);
        }

        private void downloader_ChunkDownloadFailed(object sender, Exception e)
        {
            string Mensaje = "";
            try
            {
                // Si es un webexception intentamos sacar lo que ha devuelto el servidor...
                if (e is System.Net.WebException && ((System.Net.WebException)e).Response is not null)
                {
                    using (var Stream = ((System.Net.WebException)e).Response.GetResponseStream())
                    {
                        using (var reader = new System.IO.StreamReader(Stream))
                        {
                            Mensaje = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("downloader_ChunkDownloadFailed: could not read server error response: " + Log.SafeException(ex));
            }
            string descError = "Chunk download failed. Reconnecting. " + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Error type: An error occurred while trying to download the file." + Constants.vbNewLine + " * Server response: " + Mensaje + Constants.vbNewLine + " * Internal info: " + e.ToString();



            Log.WriteError(descError);
            UltimoErrorChunk = e;
            // No hacemos nada, dejamos que vuelva a reconectarse, excepto si ha habido demasiados errores
            // 终身计数器会把几小时里 31 次各自都自动重连成功的瞬时抖动判成"错误过多"而停掉任务。
            // 改为连续失败语义:距上次分块错误超过冷却窗口即视为已恢复,重新计数。
            bool detenerPorErrores = false;
            lock (_errorChunkLock)
            {
                if (FechaUltimoErrorChunk.HasValue && (DateTime.Now - FechaUltimoErrorChunk.Value).TotalSeconds > CHUNK_ERROR_COOLDOWN_SECONDS)
                {
                    NumErroresChunk = 0;
                }
                FechaUltimoErrorChunk = DateTime.Now;
                NumErroresChunk += 1;
                detenerPorErrores = NumErroresChunk > NUM_MAX_ERRORES_CHUNK;
            }
            // Stop 放在锁外:它会取别的锁,持锁调用有死锁风险。
            if (detenerPorErrores)
            {
                Stop();
            }
        }

        private void downloader_FileDownloadSucceeded(object sender, EventArgs e)
        {
            // B6: EnsurePathUnderRoot 会抛 UnauthorizedAccessException,GetFullPath 会抛
            // ArgumentException。原实现整个方法无 Catch,而 RaiseEvent 侧也没有,异常一旦
            // 逸出就会连下面的"完成态赋值"一起跳过 —— 真正下载完成却状态错乱、成品归属丢失。
            // 与兄弟处理器 downloader_FileRenamed 保持一致,在处理器内兜住并只记录日志。
            try
            {
                FileDownloader source = sender as FileDownloader;
                if (source is not null && !string.IsNullOrEmpty(source.CompletedFilePath))
                {
                    PathGuard.EnsurePathUnderRoot(RutaLocal, source.CompletedFilePath, allowRoot: false);
                    completedFileName = System.IO.Path.GetFileName(source.CompletedFilePath);
                    RecordVerifiedOutput(source.CompletedFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Completed output path could not be verified for " + FileID + ": " + Log.SafeException(ex));
            }
            // This event fires as soon as the file is verified and renamed — before the Completed event.
            // Setting Completado here ensures the UI reflects the true state immediately, even if the
            // Completed event is delayed or the background worker's RunWorkerCompleted is slow to fire.
            // A delayed notification must also preserve extraction/verification already in progress.
            if (EstadoDescarga != Estado.Erroneo && EstadoDescarga != Estado.Descomprimiendo && EstadoDescarga != Estado.EsperandoVolumenes && EstadoDescarga != Estado.ComprobandoMD5)
            {
                EstadoDescarga = Estado.Completado;
            }
        }

        private void downloader_Completed(object sender, EventArgs e)
        {
            if (EstadoDescarga != Estado.Erroneo)
            {
                Log.WriteWarning("File " + FileID + " downloaded.");
                if (EstadoDescarga == Estado.EsperandoVolumenes || EstadoDescarga == Estado.Descomprimiendo)
                    return;
                EstadoDescarga = Estado.Completado;

                // A public-folder subscription needs the delivered path to tell the next
                // check that this file is already on disk.
                if (PublicFolderId.Length > 0)
                {
                    try
                    {
                        string delivered = CompletedOutputPath(
                            string.IsNullOrEmpty(completedFileName) ? NombreFichero : completedFileName);
                        MegaAccountService.Current.RecordPublicFolderDelivery(PublicFolderId,
                            PublicFolderHandle ?? 0UL, NombreFichero, delivered);
                    }
                    catch (Exception ex) when (ex is ArgumentException or System.IO.IOException)
                    {
                        Log.WriteWarning("Could not record the public folder delivery: " + Log.SafeException(ex));
                    }
                }

                // Obsolete, MEGA doesn't check MD5
                if (!string.IsNullOrEmpty(MD5))
                {
                    Log.WriteInfo("Verifying MD5...");
                    EstadoDescarga = Estado.ComprobandoMD5;
                    string Hash = "";
                    try
                    {
                        Hash = MD5Utils.MD5CalcFile(PathGuard.GetSafeFilePathUnderRoot(RutaLocal, NombreFichero));
                        if ((Hash.ToUpper() ?? "") == (MD5.ToUpper() ?? ""))
                        {
                            EstadoDescarga = Estado.Completado;
                            Log.WriteInfo("MD5 file " + FileID + " verified.");
                        }
                        else
                        {
                            EstablecerError("File downloaded correctly (aparently), but the MD5 verification has failed." + Constants.vbNewLine + "This mean that the file is corrupted. Try opening and if it fails, download it again. " + "After few tries if it is still corrupted, it could mean that it is corrupted on the server." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Error type: Error verifying MD5" + Constants.vbNewLine + " * Internal info: calculated MD5 [" + Hash + "]; expected MD5 [" + MD5 + "]");




                            Log.WriteInfo("File " + FileID + " checked, MD5 does not match.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error verifying MD5: " + Log.SafeException(ex));
                        EstablecerError("MD5 verification threw an exception; file cannot be marked complete." + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Internal info: " + Log.SafeException(ex));

                    }

                }
            }

            if (EstadoDescarga != Estado.Erroneo)
            {
                // Descomprimimos fichero
                // Crear directorio al descomprimir?? De momento ponemos siempre que sí...
                if (ExtraccionFicheroAutomatica)
                {
                    EstadoDescarga = Estado.Descomprimiendo;
                    try
                    {
                        string archiveName = string.IsNullOrEmpty(completedFileName) ? NombreFichero : completedFileName;
                        // F4: resolve the true completed name (do not re-sanitize: a 240-cap
                        // collision suffix would truncate to a non-existent path).
                        string archive = CompletedOutputPath(archiveName);
                        if (!DescompresorController.GetController().EnqueueArchive(_extractionCode, archive, true, DescargaExtraccionPassword(), ExtractionStateChanged))
                        {
                            EstadoDescarga = Estado.Completado;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Could not enqueue automatic extraction: " + Log.SafeException(ex));
                        DescompresionFinalizada(false, ex.Message);
                    }
                }
            }

        }


        private void downloader_Canceled(object sender, EventArgs e)
        {
            // B8: 上一轮取消的迟到事件不能把新一轮刚装配好的 downloader 清成 Nothing,
            // 也不能把正在进行的下载状态改回 EnCola。
            lock (Mutex.FicheroDownloader)
            {
                if (!ReferenceEquals(Downloader, sender))
                {
                    Log.WriteWarning("Ignored a late cancellation from a replaced downloader for " + FileID);
                    return;
                }
                if (Downloader is not null)
                {

                    Downloader.Resumed -= downloader_Started;
                    Downloader.Started -= downloader_Started;
                    Downloader.Paused -= downloader_Paused;
                    Downloader.CancelRequested -= downloader_CancelRequested;
                    Downloader.Canceled -= downloader_Canceled;
                    Downloader.Completed -= downloader_Completed;
                    Downloader.FileDownloadSucceeded -= downloader_FileDownloadSucceeded;
                    Downloader.FileDownloadFailed -= downloader_FileDownloadFailed;
                    Downloader.ChunkDownloadFailed -= downloader_ChunkDownloadFailed;
                    Downloader.FileLocalCreated -= downloader_FileLocalCreated;
                    Downloader.FileRenamed -= downloader_FileRenamed;

                    Downloader.Dispose();

                }
                Downloader = null;
            }

            if (MarcadoParaBorrarFicheroLocal)
            {
                BorrarFicheroLocal();
            }

            if (NumErroresChunk > NUM_MAX_ERRORES_CHUNK)
            {
                bool lastIsQuota = UltimoErrorChunk is MegaQuotaExceededException || IsQuotaErrorText(UltimoErrorChunk is null ? "" : UltimoErrorChunk.ToString());
                if (lastIsQuota)
                    FileDownloader.ReportQuotaIfMatch(UltimoErrorChunk);
                string descError = "Download stopped because there were too many connection errors (" + NumErroresChunk + "). " + Constants.vbNewLine + "Last error: " + Constants.vbNewLine + " * File code: " + FileID + Constants.vbNewLine + " * Error type: Connection error." + Constants.vbNewLine + " * Internal info: " + (UltimoErrorChunk is null ? "(no error detail recorded)" : UltimoErrorChunk.ToString());



                EstablecerError(descError, false, lastIsQuota);
                Log.WriteError(descError);
            }
            else
            {
                // Publish the queueable state only after the old instance is detached
                // and disposed; a new startup must not reassemble that same instance.
                EstadoDescarga = Estado.EnCola;
            }

            NotifyCancellationComplete();
        }


        public void ActualizarDatosDescarga()
        {
            // B2-⑥:100% 回补的完整流程(MD5 全文件读+解压入队)不得在 FicheroDownloader/
            // ListaDescargas 锁内同步执行,否则 GB 级文件撞线时冻结 UI/调度数秒~数分钟。
            // 这里只在锁内预定(置 ComprobandoMD5 防重复投递),锁外投线程池真正执行。
            bool needDeferredCompletion = false;
            lock (Mutex.FicheroDownloader)
            {
                if (Downloader is not null)
                {

                    NumeroConexionesAbiertas = Downloader.OpenConnections;

                    if (Downloader.File is not null && Downloader.File.Size > 0L)
                    {
                        DatosPartes = Downloader.File.GetDataPart;
                    }

                    VelocidadKBs = (decimal)(Downloader.DownloadSpeed / 1024d);
                    if (Downloader.CurrentFileSize > 0L | TamanoBytes == 0L)
                    {
                        // Si paramos y arrancamos de nuevo, CurrentFileSize tarda en inicializarse.
                        TamanoBytes = Downloader.CurrentFileSize;
                    }
                    if (Downloader.CurrentFileProgress > 0L | BytesDescargados == 0L)
                    {
                        // Si paramos y arrancamos de nuevo, CurrentFileSize tarda en inicializarse.
                        BytesDescargados = Downloader.CurrentFileProgress;
                        if (BytesDescargados > TamanoBytes)
                            BytesDescargados = TamanoBytes; // Evitamos mostrar más de un 100%
                    }
                    if (TamanoBytes > 0L)
                    {
                        Porcentaje = (decimal)(100L * BytesDescargados / (double)TamanoBytes);
                    }

                    // Fallback: if the download is at 100% and AllFinished is set, but the state
                    // is still Descargando (because the Completed/FileDownloadSucceeded event was
                    // delayed or missed), correct it here. This is a safety net for Bug 2.
                    // NOTE: we must run the full completion flow (MD5 verification + automatic
                    // extraction), not just flip the state — otherwise the UI shows "complete"
                    // while integrity is never checked and auto-extract never fires.
                    // B2-⑥:不得在此(双全局锁内)同步跑 MD5,只做预定并延迟到线程池执行。
                    if (EstadoDescarga == Estado.Descargando && TamanoBytes > 0L && BytesDescargados >= TamanoBytes && Downloader.File is not null && Downloader.File.DataPartInitialized && Downloader.File.GetDataPart.AllFinished && !Downloader.IsBusy)
                    {
                        Log.WriteWarning("ActualizarDatosDescarga: download is 100% and AllFinished but state was still Descargando. Queueing deferred completion flow. File: " + FileID);
                        EstadoDescarga = Estado.ComprobandoMD5;
                        needDeferredCompletion = true;
                    }
                }
                if (EstadoDescarga == Estado.Descargando)
                {
                    if (VelocidadKBs == 0m)
                    {
                        TiempoEstimadoDescarga = " --- ";
                    }
                    else
                    {
                        decimal BytesRestantes = TamanoBytes - Porcentaje * TamanoBytes / 100m;
                        double SegundosRestantes = (double)(BytesRestantes / (1024m * VelocidadKBs));
                        var t = TimeSpan.FromSeconds(SegundosRestantes);
                        if (SegundosRestantes > 3600d)
                        {
                            TiempoEstimadoDescarga = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", t.Days * 24 + t.Hours, t.Minutes, t.Seconds);
                        }
                        else
                        {
                            TiempoEstimadoDescarga = string.Format("{0:D2}m:{1:D2}s", t.Minutes, t.Seconds);
                        }
                    }
                }
                else
                {
                    TiempoEstimadoDescarga = "";
                }
            }
            if (needDeferredCompletion)
            {
                try
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(RunDeferredCompletion);
                }
                catch (Exception ex)
                {
                    Log.WriteError("ActualizarDatosDescarga: failed to queue deferred completion: " + Log.SafeException(ex));
                }
            }
        }

        /// <summary>
	/// 	''' B2-⑥:锁外执行的延迟补完流程(对应 ActualizarDatosDescarga 内预定的回补)。
	/// 	''' 在线程池运行,可安全做 MD5 全文件读与解压入队,不阻塞 ListaDescargas 调度与 UI 刷新。
	/// 	''' downloader_Completed 内部自带 Estado 非 Erroneo 守卫,重复投递 harmless(第二次 MD5 命中已 Completado 可接受)。
	/// 	''' </summary>
        private void RunDeferredCompletion(object state)
        {
            try
            {
                downloader_Completed(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.WriteError("RunDeferredCompletion failed: " + Log.SafeException(ex));
            }
        }

        internal void EstablecerError(string msj)
        {
            EstablecerError(msj, IsPermanentErrorText(msj), IsQuotaErrorText(msj));
        }

        private void EstablecerError(string msj, bool esPermanente, bool esQuota, int? accountCode = null)
        {
            EstadoDescarga = Estado.Erroneo;
            DescripcionError = msj;
            FechaUltimoError = DateTime.Now;
            EsErrorPermanente = esPermanente;
            FailedByQuota = esQuota;
            MegaAccountErrorCode = accountCode;
            if (esQuota)
                EsErrorPermanente = false;
        }

        /// <summary>v2.5 beta: 永久失败码 -9 ENOENT / -11 EACCESS / -14 EKEY / -16 EBLOCKED。仅匹配错误码语义,不做泛文本匹配。</summary>
        internal static bool IsPermanentErrorText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            if (s.IndexOf("-9", StringComparison.Ordinal) >= 0 && s.IndexOf("ENOENT", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("-11", StringComparison.Ordinal) >= 0 && s.IndexOf("EACCESS", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("-14", StringComparison.Ordinal) >= 0 && s.IndexOf("EKEY", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("-16", StringComparison.Ordinal) >= 0 && s.IndexOf("EBLOCKED", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // 第11项(唯一的文本匹配例外):MEGA MetaMAC 校验失败(算法修正后 mismatch 即真损坏)
            // 必须按永久失败处理 —— 否则自愈(默认开、15 分钟周期)每轮把整个文件重下一遍、
            // 烧等量 MEGA 配额,而旧提示语承诺的 "从上次有效分块继续" 并不存在(分块被整体清零)。
            // 用户仍可右键 "重试全部失败" 手动重试。
            if (s.IndexOf("MetaMAC verification failed", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        internal static bool IsQuotaErrorText(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            if (s.IndexOf("EOVERQUOTA", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("MegaQuotaExceededException", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (s.IndexOf("transfer quota exceeded", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        /// <summary>
	/// 	''' 重设下载状态：清理 DatosPartes/BytesDescargados/Porcentaje 并删除残留的 .part 文件。
	/// 	''' 用于从错误状态恢复，确保下次能从头下载而不是卡在"100% 但验证失败"的死循环。
	/// 	''' P0-1:preserveProgress=True 时保留分块进度与 .part(自愈/配额唤醒用),只清错误记账,
	/// 	''' 避免"超时→删.part→重下→再超时"无限循环;手动重置仍用默认 False 从头来。
	/// 	''' </summary>
        public void ResetearDescarga(bool preserveProgress = false)
        {
            CaptureDownloaderProgress();
            if (preserveProgress)
            {
                lock (_errorChunkLock)
                {
                    NumErroresChunk = 0;
                    FechaUltimoErrorChunk = default;
                }
                UltimoErrorChunk = null;
                DescripcionError = null;
                FechaUltimoError = default;
                EsErrorPermanente = false;
                FailedByQuota = false;
                MegaAccountErrorCode = null;
                TiempoEstimadoDescarga = "";
                return;
            }
            DiscardPrivateMegaCheckpoint();
            // 重置分块状态：AllFinished=False 且所有 chunk 回到未下载
            if (DatosPartes is not null)
            {
                DatosPartes.AllFinished = false;
                if (DatosPartes.ChunkList is not null)
                {
                    foreach (FileDownloader.DataPart.Chunk c in DatosPartes.ChunkList)
                    {
                        c.Index = 0L;
                        c.SyncedIndex = 0L;
                        c.Available = true;
                    }
                }
            }
            BytesDescargados = 0L;
            Porcentaje = 0m;
            lock (_errorChunkLock)
            {
                NumErroresChunk = 0;
                FechaUltimoErrorChunk = default;
            }
            UltimoErrorChunk = null;
            DescripcionError = null;
            FechaUltimoError = default;
            EsErrorPermanente = false;
            FailedByQuota = false;
            MegaAccountErrorCode = null;
            TiempoEstimadoDescarga = "";

            // 删除可能残留的 .part 文件（可能已损坏或处于不一致状态）
            if (!UsesMegaAccount && !string.IsNullOrWhiteSpace(RutaLocal) && !string.IsNullOrWhiteSpace(NombreFichero))
            {
                try
                {
                    string partPath = FileDownloader.GetPartialFilePath(RutaLocal, NombreFichero, PartialFileName);
                    if (System.IO.File.Exists(partPath))
                    {
                        System.IO.File.Delete(partPath);
                        Log.WriteInfo("Deleted .part file during reset: " + partPath);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Could not delete .part file during reset: " + Log.SafeException(ex));
                }
            }
        }


        #endregion



        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (UsesMegaAccount && disposing)
            {
                CancelMegaAccountDownload(Estado.Pausado, disposing: true);
                return;
            }
            if (!disposedValue)
            {
                // B1-④:释放即取消验证,防关机后复活。
                _StartupCancelled = true;
                if (disposing)
                {
                    try
                    {
                        if (Downloader is not null)
                        {
                            Downloader.Dispose();
                            Downloader = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error disposing downloader: " + Log.SafeException(ex));
                    }
                    // bgArranque is a BackgroundWorker that is only disposed in its
                    // RunWorkerCompleted handler. If the Fichero is disposed while the
                    // startup worker is still running (e.g. app closed during link
                    // resolution), RunWorkerCompleted never fires and the worker leaks.
                    try
                    {
                        if (bgArranque is not null)
                        {
                            // bgArranque.WorkerSupportsCancellation=False,调 CancelAsync 必抛
                            // InvalidOperationException;取消靠入口处的 _StartupCancelled 标志,
                            // 这里只释放不取消。
                            bgArranque.Dispose();
                            bgArranque = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error disposing bgArranque: " + Log.SafeException(ex));
                    }
                }
            }
            disposedValue = true;
        }

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion




        public void CargarXML(XmlNode XML)
        {

            // IsNumeric 放行超出 Int32 的值(如 "2147483648"/"1e10"),CInt 会抛 OverflowException
            // 并让整个下载列表加载失败。与下面日期字段同样按"单个坏字段不拖垮整份队列"处理。
            int versionAttr = 1;
            if (XML.Attributes["v"] is not null && int.TryParse(XML.Attributes["v"].Value, out versionAttr))
            {
                Version = versionAttr;
            }
            else
            {
                Version = 1;
            }

            CacheSecureData = null;
            var loadedCache = new Cache();
            string argPath = "FileID";
            _FileID = Criptografia.DecryptStringWithCacheInfo(LeerNodo(ref XML, ref argPath, ""), ref loadedCache.FileID);
            string argPath1 = "FileKey";
            _FileKey = Criptografia.DecryptStringWithCacheInfo(LeerNodo(ref XML, ref argPath1, ""), ref loadedCache.FileKey);
            string argPath2 = "URL";
            _URL = Criptografia.DecryptStringWithCacheInfo(LeerNodo(ref XML, ref argPath2, ""), ref loadedCache.URL);
            string argPath3 = "URLFichero";
            _URLFichero = Criptografia.DecryptStringWithCacheInfo(LeerNodo(ref XML, ref argPath3, ""), ref loadedCache.URLFichero);

            // Hacemos el link no visible
            string UrlPlain = Criptografia.ToInsecureString(_URL);
            LinkVisible = !UrlPlain.StartsWith(HIDDEN_LINK, StringComparison.Ordinal);
            if (!LinkVisible)
            {
                _URL.Dispose();
                _URL = Criptografia.ToSecureString(UrlPlain.Replace(HIDDEN_LINK, ""));
            }
            // Reuse only successfully decrypted current-format ciphertext. Historical
            // entropy values and failed/missing fields must still be migrated on save.
            if (loadedCache.FileID is not null && loadedCache.FileKey is not null && loadedCache.URL is not null && loadedCache.URLFichero is not null)
            {
                CaptureCacheSources(loadedCache);
                CacheSecureData = loadedCache;
            }


            string argPathMegaAccountEmail = "MegaAccountEmail";
            MegaAccountEmail = LeerNodo(ref XML, ref argPathMegaAccountEmail, "");
            string argPathMegaDriveNodeHandle = "MegaDriveNodeHandle";
            ulong driveHandle;
            MegaDriveNodeHandle = ulong.TryParse(LeerNodo(ref XML, ref argPathMegaDriveNodeHandle, ""),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                out driveHandle) ? driveHandle : (ulong?)null;
            if (MegaDriveNodeHandle.HasValue)
                LinkVisible = false;
            string argPathMegaAccountTransferEmail = "MegaAccountTransferEmail";
            MegaAccountTransferEmail = UsesMegaAccount && !MegaDriveNodeHandle.HasValue
                ? LeerNodo(ref XML, ref argPathMegaAccountTransferEmail, "")
                : string.Empty;

            string argPathPublicFolderId = "PublicFolderId";
            PublicFolderId = LeerNodo(ref XML, ref argPathPublicFolderId, "");
            string argPathPublicFolderHandle = "PublicFolderHandle";
            ulong publicFolderHandle;
            PublicFolderHandle = ulong.TryParse(LeerNodo(ref XML, ref argPathPublicFolderHandle, ""),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                out publicFolderHandle) ? publicFolderHandle : (ulong?)null;

            string argPath4 = "NombreFichero";
            NombreFichero = LeerNodo(ref XML, ref argPath4, "");
            string argPath5 = "PartialFileName";
            PartialFileName = LeerNodo(ref XML, ref argPath5, "");
            if (!string.IsNullOrEmpty(PartialFileName))
            {
                try
                {
                    FileDownloader.ValidatePartialFileName(PartialFileName);
                }
                catch (ArgumentException ex)
                {
                    Log.WriteWarning("Invalid persisted partial file name; assigning a new task file.");
                    PartialFileName = FileDownloader.CreatePartialFileName();
                }
            }
            if (MegaDriveNodeHandle.HasValue && string.IsNullOrEmpty(PartialFileName))
                PartialFileName = FileDownloader.CreatePartialFileName();
            string argPath6 = "RutaRelativa";
            RutaRelativa = LeerNodo(ref XML, ref argPath6, "");
            string argPath7 = "RutaLocal";
            RutaLocal = LeerNodo(ref XML, ref argPath7, "");
            var completedNode = XML.SelectSingleNode("CompletedFileName");
            completedFileName = completedNode is null ? null : completedNode.InnerText;
            if (!string.IsNullOrEmpty(completedFileName))
            {
                try
                {
                    CompletedOutputPath(completedFileName);
                }
                catch (ArgumentException ex)
                {
                    completedFileName = "";
                    Log.WriteWarning("Invalid persisted completed file name; keeping existing files.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    completedFileName = "";
                    Log.WriteWarning("Completed file path is outside the task directory; keeping existing files.");
                }
                catch (System.IO.IOException ex)
                {
                    completedFileName = "";
                    Log.WriteWarning("Unreadable persisted completed file path; keeping existing files.");
                }
            }
            string argPath8 = "TamanoBytes";
            long.TryParse(LeerNodo(ref XML, ref argPath8, "0"), out TamanoBytes);
            string argPath9 = "NumeroConexionesAbiertas";
            int.TryParse(LeerNodo(ref XML, ref argPath9, "0"), out NumeroConexionesAbiertas);
            string argPath10 = "NumeroChunksAsignados";
            int.TryParse(LeerNodo(ref XML, ref argPath10, "0"), out NumeroChunksAsignados);
            string argPath11 = "FechaUltimoError";
            string strFecha = LeerNodo(ref XML, ref argPath11, "");
            if (!string.IsNullOrEmpty(strFecha))
            {
                // CDate 区域性相关且会抛异常:单个损坏的队列文件不应导致整个下载列表加载失败(启动即崩)
                DateTime fecha;
                if (DateTime.TryParse(strFecha, out fecha) || DateTime.TryParse(strFecha, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out fecha))
                {
                    FechaUltimoError = fecha;
                }
                else
                {
                    Log.WriteWarning("Invalid FechaUltimoError in queue XML, ignored: " + strFecha);
                }
            }
            string argPath12 = "Porcentaje";
            decimal.TryParse(LeerNodo(ref XML, ref argPath12, "0"), out Porcentaje);
            string argPath13 = "DescargaComenzada";
            bool.TryParse(LeerNodo(ref XML, ref argPath13, "false"), out DescargaComenzada);
            string argPath14 = "DescargaProcesada";
            bool.TryParse(LeerNodo(ref XML, ref argPath14, "false"), out DescargaProcesada);
            string argPath15 = "ExtraccionFicheroAutomatica";
            bool.TryParse(LeerNodo(ref XML, ref argPath15, "false"), out ExtraccionFicheroAutomatica);
            string argPath16 = "ExtraccionFicheroPassword";
            string tempPass = LeerNodo(ref XML, ref argPath16, "");
            if (!string.IsNullOrEmpty(tempPass))
            {
                // Decrypt with DPAPI (current scheme, machine/user bound).
                string decrypted = "";
                try
                {
                    decrypted = Criptografia.ToInsecureString(Criptografia.DecryptString_DPAPI(tempPass));
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("ExtraccionFicheroPassword: DPAPI decrypt failed, trying legacy AES fallback: " + ex.Message);
                }
                // Legacy fallback for queue files saved before the DPAPI migration.
                // Kept for backward compatibility only; new saves always use DPAPI.
                if (string.IsNullOrEmpty(decrypted))
                {
                    decrypted = Criptografia.AES_DecryptString(tempPass, "passZIP");
                }
                if (!string.IsNullOrEmpty(decrypted))
                {
                    ExtraccionFicheroPassword = decrypted;
                }
            }


            string argPath17 = "Prioridad";
            int.TryParse(LeerNodo(ref XML, ref argPath17, "0"), out Prioridad);



            string argPath18 = "VelocidadKBs";
            decimal.TryParse(LeerNodo(ref XML, ref argPath18, "0"), out VelocidadKBs);
            string argPath19 = "BytesDescargados";
            long.TryParse(LeerNodo(ref XML, ref argPath19, "0"), out BytesDescargados);

            EstadoDescarga = Estado.EnCola;
            string argPath21 = "EstadoDescarga";
            if (Enum.IsDefined(typeof(Estado), LeerNodo(ref XML, ref argPath21, "")))
            {
                string argPath20 = "EstadoDescarga";
                EstadoDescarga = (Estado)Conversions.ToInteger(Enum.Parse(typeof(Estado), LeerNodo(ref XML, ref argPath20, "")));
            }
            if (MegaDriveNodeHandle.HasValue)
            {
                string argPathWaiting = "MegaDriveWaitingForQuota";
                bool.TryParse(LeerNodo(ref XML, ref argPathWaiting, "false"), out MegaDriveWaitingForQuota);
                if (DescargaComenzada && string.IsNullOrEmpty(completedFileName) && EstadoDescarga != Estado.Completado)
                    RestorePrivateMegaProgress();
            }
            // RC:重启后查看错误空白/永久失败诈尸的根因——此前只存 Estado,不存描述与标记。
            string argPath22 = "DescripcionError";
            DescripcionError = LeerNodo(ref XML, ref argPath22, "");
            if (string.IsNullOrEmpty(DescripcionError))
                DescripcionError = null;
            string argPath23 = "EsErrorPermanente";
            bool.TryParse(LeerNodo(ref XML, ref argPath23, "false"), out EsErrorPermanente);
            string argPath24 = "FailedByQuota";
            bool.TryParse(LeerNodo(ref XML, ref argPath24, "false"), out FailedByQuota);
            MegaAccountErrorCode = null;
            if (UsesMegaAccount)
            {
                string argPathAccountCode = "MegaAccountErrorCode";
                if (int.TryParse(LeerNodo(ref XML, ref argPathAccountCode, ""), out int accountCode))
                {
                    MegaAccountErrorCode = accountCode;
                }
                else if (EstadoDescarga == Estado.Erroneo && !string.IsNullOrEmpty(DescripcionError))
                {
                    // Older account tasks stored only the localized description.
                    if (DescripcionError == MegaAccountService.ErrorText(MegaAccountService.OverQuota))
                    {
                        FailedByQuota = true;
                    }
                    else if (DescripcionError == MegaAccountService.ErrorText(MegaAccountService.SessionExpired))
                    {
                        MegaAccountErrorCode = MegaAccountService.SessionExpired;
                    }
                    else
                    {
                        foreach (int value in TransientMegaAccountErrors)
                        {
                            if (DescripcionError == MegaAccountService.ErrorText(value))
                            {
                                MegaAccountErrorCode = value;
                                break;
                            }
                        }
                    }
                }
            }
            // 兼容旧版派生标记:老队列无标记时按描述文本回推,避免重启后永久失败被自愈捞起。
            else if (!EsErrorPermanente && !FailedByQuota && !string.IsNullOrEmpty(DescripcionError))
            {
                EsErrorPermanente = IsPermanentErrorText(DescripcionError);
                if (!EsErrorPermanente)
                    FailedByQuota = IsQuotaErrorText(DescripcionError);
                if (FailedByQuota)
                    EsErrorPermanente = false;
            }
            string argPath25 = "MarcadoParaBorrarFicheroLocal";
            bool.TryParse(LeerNodo(ref XML, ref argPath25, "false"), out MarcadoParaBorrarFicheroLocal);
            string argPath26 = "DescargaIndividual";
            bool.TryParse(LeerNodo(ref XML, ref argPath26, "false"), out DescargaIndividual);
            string argPath27 = "PausaIndividual";
            bool.TryParse(LeerNodo(ref XML, ref argPath27, "false"), out PausaIndividual);
            string argPath28 = "TiempoEstimadoDescarga";
            TiempoEstimadoDescarga = LeerNodo(ref XML, ref argPath28, "");
            string argPath29 = "LimiteVelocidad";
            int.TryParse(LeerNodo(ref XML, ref argPath29, "0"), out LimiteVelocidad);

            if (XML.SelectSingleNode("DatosPartes") is not null)
            {
                var NodoDatosPartes = XML.SelectSingleNode("DatosPartes");
                DatosPartes = new FileDownloader.DataPart();
                string argPath30 = "AllFinished";
                bool.TryParse(LeerNodo(ref NodoDatosPartes, ref argPath30, "false"), out DatosPartes.AllFinished);
                if (NodoDatosPartes.SelectSingleNode("ChunkList") is not null)
                {
                    var NodoChunkList = NodoDatosPartes.SelectSingleNode("ChunkList");
                    DatosPartes.ChunkList = new List<FileDownloader.DataPart.Chunk>();
                    foreach (XmlNode nodoChunkItem in NodoChunkList.SelectNodes("Chunk"))
                    {
                        XmlNode NodoChunk = nodoChunkItem;
                        var Chunk = new FileDownloader.DataPart.Chunk();
                        string argPath31 = "Index";
                        long.TryParse(LeerNodo(ref NodoChunk, ref argPath31, "0"), out Chunk.Index);
                        string argPath32 = "Size";
                        long.TryParse(LeerNodo(ref NodoChunk, ref argPath32, "0"), out Chunk.Size);
                        string argPath33 = "StartIndex";
                        long.TryParse(LeerNodo(ref NodoChunk, ref argPath33, "0"), out Chunk.StartIndex);
                        string argPath34 = "Available";
                        bool.TryParse(LeerNodo(ref NodoChunk, ref argPath34, "false"), out Chunk.Available);
                        // 方案B：旧版本 XML 无 SyncedIndex 记账，但历史进度在旧版每次落盘都 fsync，
                        // 直接继承为已同步边界，避免升级后重启把存量进度钳到 0 重下。
                        Chunk.SyncedIndex = Chunk.Index;
                        DatosPartes.ChunkList.Add(Chunk);
                    }
                }
                // Do not trust AllFinished without a consistent layout / full progress
                if (DatosPartes.ChunkList is null || DatosPartes.ChunkList.Count == 0)
                {
                    DatosPartes.AllFinished = false;
                }
                else if (TamanoBytes > 0L)
                {
                    DatosPartes.ValidateAndNormalize(TamanoBytes);
                }
                else
                {
                    bool anyIncomplete = false;
                    foreach (FileDownloader.DataPart.Chunk c in DatosPartes.ChunkList)
                    {
                        if (c.Index != c.Size)
                            anyIncomplete = true;
                    }
                    if (anyIncomplete)
                        DatosPartes.AllFinished = false;
                }
            }

        }



        public XmlNode GuardarXML(XmlDocument XML, bool IncluirDatosCifrados)
        {
            // B2-⑩:序列化与 ActualizarInformacionFichero 回写同锁,防关机撕裂(半新半旧队列)。
            // 锁序与 ActualizarDatosDescarga 一致(ListaDescargas->FicheroDownloader),无反转死锁。
            lock (Mutex.FicheroDownloader)
            {
                XmlNode NodoFic = XML.CreateElement("Fichero");
                NodoFic.Attributes.Append(XML.CreateAttribute("v")).Value = Version.ToString();

                if (IncluirDatosCifrados)
                {
                    if (CacheSecureData is null || !CacheSecureData.Matches(this))
                        RegenerateCacheSecureData();

                    NodoFic.AppendChild(XML.CreateElement("FileID")).InnerText = CacheSecureData.FileID;
                    NodoFic.AppendChild(XML.CreateElement("FileKey")).InnerText = CacheSecureData.FileKey;
                    NodoFic.AppendChild(XML.CreateElement("URL")).InnerText = CacheSecureData.URL;
                    NodoFic.AppendChild(XML.CreateElement("URLFichero")).InnerText = CacheSecureData.URLFichero;
                }
                else
                {
                    NodoFic.AppendChild(XML.CreateElement("FileID")).InnerText = Criptografia.ToInsecureString(_FileID);
                }
                if (UsesMegaAccount)
                {
                    NodoFic.AppendChild(XML.CreateElement("MegaAccountEmail")).InnerText = MegaAccountEmail;
                    if (MegaDriveNodeHandle.HasValue)
                    {
                        NodoFic.AppendChild(XML.CreateElement("MegaDriveNodeHandle")).InnerText =
                            MegaDriveNodeHandle.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (MegaDriveWaitingForQuota)
                            NodoFic.AppendChild(XML.CreateElement("MegaDriveWaitingForQuota")).InnerText = "true";
                    }
                    else if (MegaAccountTransferEmail.Length > 0)
                    {
                        NodoFic.AppendChild(XML.CreateElement("MegaAccountTransferEmail")).InnerText = MegaAccountTransferEmail;
                    }
                }
                if (PublicFolderId.Length > 0)
                {
                    NodoFic.AppendChild(XML.CreateElement("PublicFolderId")).InnerText = PublicFolderId;
                    if (PublicFolderHandle.HasValue)
                    {
                        NodoFic.AppendChild(XML.CreateElement("PublicFolderHandle")).InnerText =
                            PublicFolderHandle.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                NodoFic.AppendChild(XML.CreateElement("NombreFichero")).InnerText = NombreFichero;
                NodoFic.AppendChild(XML.CreateElement("PartialFileName")).InnerText = PartialFileName;
                if (completedFileName is not null)
                    NodoFic.AppendChild(XML.CreateElement("CompletedFileName")).InnerText = completedFileName;
                NodoFic.AppendChild(XML.CreateElement("RutaLocal")).InnerText = RutaLocal;
                NodoFic.AppendChild(XML.CreateElement("RutaRelativa")).InnerText = RutaRelativa;
                NodoFic.AppendChild(XML.CreateElement("TamanoBytes")).InnerText = TamanoBytes.ToString();
                NodoFic.AppendChild(XML.CreateElement("NumeroConexionesAbiertas")).InnerText = NumeroConexionesAbiertas.ToString();
                NodoFic.AppendChild(XML.CreateElement("NumeroChunksAsignados")).InnerText = NumeroChunksAsignados.ToString();
                if (FechaUltimoError.HasValue)
                {
                    NodoFic.AppendChild(XML.CreateElement("FechaUltimoError")).InnerText = FechaUltimoError.Value.ToString("s");
                }
                NodoFic.AppendChild(XML.CreateElement("Porcentaje")).InnerText = Porcentaje.ToString();
                NodoFic.AppendChild(XML.CreateElement("DescargaComenzada")).InnerText = DescargaComenzada.ToString();
                NodoFic.AppendChild(XML.CreateElement("DescargaProcesada")).InnerText = DescargaProcesada.ToString();
                NodoFic.AppendChild(XML.CreateElement("ExtraccionFicheroAutomatica")).InnerText = ExtraccionFicheroAutomatica.ToString();

                // DPAPI 密文(随机盐,每次不同)只写真盘、不进比对快照,理由同 Paquete.GuardarXML。
                if (IncluirDatosCifrados && !string.IsNullOrEmpty(ExtraccionFicheroPassword))
                {
                    NodoFic.AppendChild(XML.CreateElement("ExtraccionFicheroPassword")).InnerText = Criptografia.EncryptString_DPAPI(Criptografia.ToSecureString(ExtraccionFicheroPassword));
                }
                if (!IncluirDatosCifrados)
                {
                    NodoFic.AppendChild(XML.CreateElement("ExtraccionFicheroPasswordHash")).InnerText = Utils.SecretFingerprint(ExtraccionFicheroPassword);
                }

                NodoFic.AppendChild(XML.CreateElement("Prioridad")).InnerText = Prioridad.ToString();
                NodoFic.AppendChild(XML.CreateElement("VelocidadKBs")).InnerText = VelocidadKBs.ToString();
                NodoFic.AppendChild(XML.CreateElement("BytesDescargados")).InnerText = BytesDescargados.ToString();

                NodoFic.AppendChild(XML.CreateElement("EstadoDescarga")).InnerText = Enum.GetName(typeof(Estado), EstadoDescarga);
                // RC:持久化错误描述与标记(重启后查看错误不再空白,永久/配额标记不再丢失)。
                // 描述含内部堆栈,截断到 4000 字防队列文件膨胀;标记缺省 false 兼容旧版。
                string descToSave = DescripcionError ?? "";
                if (descToSave.Length > 4000)
                    descToSave = descToSave.Substring(0, 4000);
                NodoFic.AppendChild(XML.CreateElement("DescripcionError")).InnerText = descToSave;
                NodoFic.AppendChild(XML.CreateElement("EsErrorPermanente")).InnerText = EsErrorPermanente.ToString();
                NodoFic.AppendChild(XML.CreateElement("FailedByQuota")).InnerText = FailedByQuota.ToString();
                if (MegaAccountErrorCode.HasValue)
                {
                    NodoFic.AppendChild(XML.CreateElement("MegaAccountErrorCode")).InnerText =
                        MegaAccountErrorCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                NodoFic.AppendChild(XML.CreateElement("MarcadoParaBorrarFicheroLocal")).InnerText = MarcadoParaBorrarFicheroLocal.ToString();
                NodoFic.AppendChild(XML.CreateElement("TiempoEstimadoDescarga")).InnerText = TiempoEstimadoDescarga;
                NodoFic.AppendChild(XML.CreateElement("PausaIndividual")).InnerText = PausaIndividual.ToString();
                NodoFic.AppendChild(XML.CreateElement("DescargaIndividual")).InnerText = DescargaIndividual.ToString();
                NodoFic.AppendChild(XML.CreateElement("LimiteVelocidad")).InnerText = LimiteVelocidad.ToString();

                if (DatosPartes is not null)
                {
                    var NodoDatosPartes = NodoFic.AppendChild(XML.CreateElement("DatosPartes"));
                    NodoDatosPartes.AppendChild(XML.CreateElement("AllFinished")).InnerText = DatosPartes.AllFinished.ToString();
                    if (DatosPartes.ChunkList is not null)
                    {
                        var NodoChunkList = NodoDatosPartes.AppendChild(XML.CreateElement("ChunkList"));
                        foreach (FileDownloader.DataPart.Chunk chunk in DatosPartes.ChunkList)
                        {
                            var NodoChunk = NodoChunkList.AppendChild(XML.CreateElement("Chunk"));
                            NodoChunk.AppendChild(XML.CreateElement("StartIndex")).InnerText = chunk.StartIndex.ToString();
                            NodoChunk.AppendChild(XML.CreateElement("Size")).InnerText = chunk.Size.ToString();
                            // 方案B 红线：只持久化已 fsync 的边界。内存 Index 可能超前（节流窗口内），
                            // 取 Min 后断电重启最多重下窗口内数据，永不出现空洞。
                            long persistedIndex = Math.Min(chunk.Index, chunk.SyncedIndex);
                            if (persistedIndex < 0L)
                                persistedIndex = 0L;
                            NodoChunk.AppendChild(XML.CreateElement("Index")).InnerText = persistedIndex.ToString();
                            NodoChunk.AppendChild(XML.CreateElement("Available")).InnerText = chunk.Available.ToString();
                        }
                    }

                }

                return NodoFic;
            }

        }

        private static string LeerNodo(ref XmlNode NodoXML, ref string Path, string ValorDefecto)
        {
            var nodo = NodoXML.SelectSingleNode(Path);
            if (nodo is null)
            {
                return ValorDefecto;
            }
            else
            {
                return nodo.InnerText;
            }
        }

    }
}