using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Xml;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public class Configuracion
    {

        public enum ErrorConfigClass
        {
            SinErrores = 0, // No hay errores
            Fichero_No_Existe = 1, // El fichero no existe
            Fichero_No_Valido = 2, // El fichero existe pero no es un XML vallido
            Usuario_Password_Incorrecto = 3, // El fichero existe pero el usuario y password está mal grabado
            Fichero_No_Creado = 4, // Ha habido algún error creando el directorio o el fichero (permisos?)
            Fichero_Bloqueado = 5 // B1: el fichero está bloqueado por otro proceso; no es un error de disco/permisos
        }


        #region Métodos públicos

        public Configuracion() : this(null)
        {
        }

        internal Configuracion(string configurationPath) : this(configurationPath, AtomicFile.StartupAttempts)
        {
        }

        internal Configuracion(string configurationPath, int initialReadAttempts)
        {
            _initialReadAttempts = Math.Max(1, initialReadAttempts);
            _configurationPath = configurationPath is null ? null : Path.GetFullPath(configurationPath);
            // Cargamos del XML
            ErrorConfig = ErrorConfigClass.SinErrores;
            ConfigUI = new ConfiguracionUI();
            CargaXML();
            Conexion.SetProxy(this);
            MegaQuotaManager.SetBreakthroughMode(QuotaBreakthroughMode);
            // ③:MaximumBytesPerSecond 按字节限速,此处须 KB→B(×1024),否则 1MB/s 实得 1KB/s。
            ThrottledStreamController.GetController().SetMaxGlobalSpeed(LimiteVelocidadKBs * 1024L);
        }

        public ConfiguracionUI ConfigUI;

        public string Idioma;

        public bool CondicionesAceptadas;

        public string VersionConfig;

        public ErrorConfigClass ErrorConfig;

        /// <summary>ErrorConfig mixes two different things: a failure to LOAD the file
	/// 	''' (Fichero_No_Existe / Fichero_No_Valido) and a failure to SAVE it (Fichero_No_Creado).
	/// 	''' P0-2 clears ErrorConfig after a successful save, which also erased the LOAD error —
	/// 	''' and the V26 column migration (Main.vb:420-424) saves successfully BEFORE the check at
	/// 	''' Main.vb:450, so a fresh install (or a corrupt XML) silently lost the mandatory
	/// 	''' first-run configuration dialog. This field reflects the load result only and is never
	/// 	''' cleared; Main.vb:450 reads this one, Configuration.vb keeps reading ErrorConfig.</summary>
        public ErrorConfigClass LoadErrorConfig;

        /// <summary>P0-3:加载失败是 IOException(文件被瞬时占用)而非损坏时为 True。
	/// 	''' 此时磁盘上的配置可能完好,内存里只能跑默认值——所有写盘必须跳过并保留
	/// 	''' 旧文件,否则 GuardarXML 会把默认值落盘并把完好文件挤进 .bak 直至彻底覆盖。</summary>
        public bool LoadTransientError;
        private readonly string _configurationPath;
        private readonly int _initialReadAttempts;
        private string _transientBlockedSource;

        /// <summary>P-21: 仅 CargaXMLInterno 期间有效。启动时 60+ 次 SelectSingleNode
	/// 	''' 全文档查找改为一次遍历建字典后 O(1) 取值;方法结束即释放。</summary>
        private Dictionary<string, string> _loadedNodes = null;

        public string RutaDefecto;

        public bool ExtraerAutomaticamente;

        public bool CrearDirectorioPaquete;

        public bool AnalizarPortapapeles;

        // Public PermitirSkins As Boolean

        public int TamanoPaqueteKB;

        public int TamanoBufferKB;

        public int MaxConexionesGuardadas;

        public int ConexionesPorFichero;

        public int DescargasSimultaneas;

        public bool ResetearErrores;

        /// <summary>配额突破模式(旧版行为):命中 509/-17 不熔断,走普通瞬时错误链持续重试。默认关。</summary>
        public bool QuotaBreakthroughMode;

        /// <summary>v2.5 beta 一次性迁移:老配置的 ResetearErrores=False 翻为 True(15min)。True=已迁移,不再重复覆盖用户后续手动选择。</summary>
        public bool ResetearErroresMigratedV25;

        /// <summary>P0-1 UI 一次性列默认集:开 Progreso%、藏 Descargado、Estado 加宽。True=已迁移,不再覆盖用户后续手动调整。
	/// 	''' 注:V26 是 UI 修订号,不是 App 版本(当前发布线 v2.5),为兼容已迁移用户保留原名,RC 起双写 V25 别名。</summary>
        public bool ColumnUIDefaultsMigratedV26;
        public bool WorkbenchColumnsMigrated;

        public bool UsarProxy;

        public bool ApagarPC;
        public DownloadScheduleSettings DownloadSchedule = new DownloadScheduleSettings();

        public bool CheckUpdates;

        /// <summary>
    /// 6: 定时检查间隔（小时）。0 表示使用默认 1 小时；负数视为无效并回退默认。
    /// 与内部配置 VERSION_PERIODO_REFRESCO_SEG 兼容：用户值 >0 时覆盖回退。
    /// </summary>
        public int UpdateCheckIntervalHours = 1;

        /// <summary>
    /// 6: 仅手动检查。True 时后台 worker 不定时触发更新检查，只能由 Help > Check for updates
    /// 菜单手动触发。与 CheckUpdates=False（完全关闭）正交：手动模式仍允许手动检查。
    /// </summary>
        public bool UpdateManualOnly = false;

        /// <summary>
    /// Versión que el usuario descartó con "no volver a avisar".
    /// Vacío (por defecto) = ninguna versión descartada.
    /// </summary>
        public string UpdateSkipVersion;

        public bool ComenzarDescargando;

        public bool MantenerUltimaConfiguracion;

        public bool HideCollaborateButton;

        public string ProxyIP;

        public string ProxyUser;

        public string ProxyPassword;

        public int ProxyPort;

        public int ResetearErroresPeriodoMinutos;

        public int LimiteVelocidadKBs;

        public bool IniciarConWindows;

        /// <summary>P1-1:用户选择"不再提示"后,mega:// 注册失败不再弹框(仍写日志)。</summary>
        public bool NoMostrarAvisoURI;

        public Log.LevelLogType NivelLog;

        public DescompresionPriority PrioridadDescompresion;

        public bool ServidorStreamingActivo;

        public int ServidorStreamingPuerto;
        private const int DEFAULT_STREAMING_PORT = 54321;

        public string ServidorStreamingPassword;

        public bool ServidorWebActivo;

        public string ServidorWebNombre;

        public string ServidorWebRutaPlantilla;

        public int ServidorWebPuerto;

        public string ServidorWebPassword;

        public int ServidorWebTimeout;

        /// <summary>
	/// 	''' Allow LAN devices to reach the web remote-control interface.
	/// 	''' False (default) binds 127.0.0.1 only — local machine access.
	/// 	''' True binds all interfaces so phones/other PCs can push downloads;
	/// 	''' requires ServidorWebPassword to be set (enforced at server start).
	/// 	''' </summary>
        public bool ServidorWebPermitirLAN;

        /// <summary>
	/// 	''' Specific local IP address to bind when ServidorWebPermitirLAN is enabled.
	/// 	''' Empty (default) binds all interfaces (0.0.0.0). Useful on multi-NIC
	/// 	''' machines to expose the listener on the LAN adapter only, keeping it
	/// 	''' off VPN/virtual adapters.
	/// 	''' </summary>
        public string ServidorWebBindIP;

        public string VLCPath;

        public List<SecureString> ListaPreSharedKeys;

        private SecureString _Usuario;

        private SecureString _Password;

        private List<ELCAccountHelper.Account> _ELCAccountList;

        private const string KeyPassword = "A9G7dHUprtNmNEBLEDhFneBAcyRTZdd5RuAzYQKc3qJ4BaVH";
        public string Usuario
        {
            get
            {
                return Criptografia.ToInsecureString(_Usuario);
            }
            set
            {
                _Usuario = Criptografia.ToSecureString(value);
            }
        }
        public string Password
        {
            get
            {
                return Criptografia.ToInsecureString(_Password);
            }
            set
            {
                _Password = Criptografia.ToSecureString(value);
            }
        }

        public List<ELCAccountHelper.Account> ELCAccounts
        {
            get
            {
                return _ELCAccountList;
            }
            set
            {
                _ELCAccountList = value;
                // Final review: ELCAccounts is appended to the XmlDocument AFTER the
                // de-dup snapshot is taken, so account edits are invisible to
                // _LastSavedXML. P-14's 60s fingerprint throttle therefore made a
                // non-dialog account change invisible for up to a minute. Invalidate
                // the cached hash here (the only assignment path is
                // ELCAccountHelper.SaveToConfig) so the next periodic save recomputes.
                _LastSavedELCAccountsHash = null;
            }
        }


        private static string _LastSavedXML = null;
        // B2-⑦:ServidorWebPassword 明文快照,用于去重比对。密文每次随机 IV 必变,
        // 不得参与 OuterXml 比对(否则有 Web 密码时配置每 5s 必重写)。
        private static string _LastSavedWebPasswordPlain = null;
        // P0-4:代理/流媒体口令 DPAPI 密文同样非确定性(Protect 每次随机)，同样不得参与
        // OuterXml 比对；此处快照其明文用于去重（Web 口令模式的复用）。
        private static string _LastSavedProxyPasswordPlain = null;
        private static string _LastSavedStreamingPasswordPlain = null;
        // R-P0-1:ELCAccounts 的内容指纹(见 ELCAccountsHash)。账号节点是在去重快照 _LastSavedXML
        // 之后才追加到 XML 的,所以账号增删改对周期性 GuardarXML(False) 完全不可见 —— P0-1 只能在
        // 两处导入调用点补 GuardarXML(True) 兜底,陷阱本身还在。此字段让周期保存也能感知账号变更。
        private static string _LastSavedELCAccountsHash = null;
        // P-14: 上次真正重算账号指纹的时刻(节流用,不参与去重比较)。
        private static DateTime _lastELCHashUtc = DateTime.MinValue;
        // P0-3 补充:GuardarXML 每 5s 被调用一次,瞬时锁状态下若每次都写 Warning,一小时 700+ 条。
        // 只在首次真正拦截时记一条,状态本身仍由 LoadTransientError 决定(不影响行为)。
        private static bool _TransientSkipLogged = false;
        // DPAPI 密文存储前缀：有前缀=DPAPI blob（解密失败=需重填）；无前缀=历史明文（直接沿用，
        // 下次保存时转加密迁移）。前缀法避免 base64 启发式误判（如 8 位字母数字口令恰为合法 base64）。
        // 注意：降级到旧版本会读到 "DPAPI:..." 原字串当密码用——升级前备份配置（release notes 已要求）。
        private const string DPAPI_PREFIX = "DPAPI:";
        // P0-4：DPAPI 解密失败（换机器/换账户恢复配置）的运行时标记，不落盘。
        // 由设置界面读取并提示用户重填；用户保存新值后清零。
        public bool ProxyPasswordNeedsReentry;
        public bool ServidorStreamingPasswordNeedsReentry;

        /// <summary>R-P0-1:ELCAccounts 的内容指纹(SHA-256/base64)。
	/// 	''' Account.User/Key 是随机 IV 的 DPAPI 密文,不能参与 OuterXml 去重比对(否则每次调用都
	/// 	''' 不同、后台每 5s 必落盘 —— 与 ServidorWebPassword 后移到比对之后同理),所以这里按
	/// 	''' 明文内容取哈希:既能感知账号的增/删/改/顺序变化,又只留哈希不留明文副本。</summary>
        private string ELCAccountsHash()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                // P1-5:持统一锁遍历(列表可能正被 ELCAccountHelper.SaveToConfig 换引用/释放)
                lock (ELCAccountHelper.ELCAccountsLock)
                {
                    foreach (ELCAccountHelper.Account Account in ELCAccounts)
                        sb.Append(Account.Alias).Append(Constants.vbTab).Append(Account.URL).Append(Constants.vbTab).Append(Criptografia.ToInsecureString(Account.User)).Append(Constants.vbTab).Append(Criptografia.ToInsecureString(Account.Key)).Append(Constants.vbTab).Append(Account.DefaultAccount ? "1" : "0").Append(Constants.vbLf);
                }
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.Unicode.GetBytes(sb.ToString())));
                }
            }
            catch (Exception ex)
            {
                // ELCAccountHelper.SaveToConfig 会先 Dispose 旧 SecureString、Clear 再重建整个列表,
                // 全程不持锁;而本方法可能正跑在 bgwActualizadorDatosDisco 的周期保存上
                // (Main.vb:2765 在 DoWork 内),撞上那个窗口会抛 ObjectDisposedException /
                // InvalidOperationException。返回 Nothing 只会让下个周期多存一次盘并重试,
                // 绝不能让异常冒出去把整次保存打掉(此处不在 GuardarXML 的 Try 覆盖范围内)。
                Log.WriteDebug("ELCAccountsHash skipped (account list busy): " + Log.SafeException(ex));
                return null;
            }
        }

        public void GuardarXML(bool ForzarGuardado)
        {
            ApplyConfigLimits();
            var Xml = new XmlDocument();

            Xml.AppendChild(Xml.CreateElement("Configuration"));

            Xml.DocumentElement.AppendChild(Xml.CreateElement("Language")).InnerText = Idioma;
            Xml.DocumentElement.AppendChild(Xml.CreateElement("RutaDefecto")).InnerText = RutaDefecto;
            Xml.DocumentElement.AppendChild(Xml.CreateElement("CondicionesAceptadas")).InnerText = CondicionesAceptadas.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("VersionConfig")).InnerText = VersionConfig;
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ExtraerAutomaticamente")).InnerText = ExtraerAutomaticamente.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("CrearDirectorioPaquete")).InnerText = CrearDirectorioPaquete.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("AnalizarPortapapeles")).InnerText = AnalizarPortapapeles.ToString();
            // Xml.DocumentElement.AppendChild(Xml.CreateElement("PermitirSkins")).InnerText = PermitirSkins.ToString
            Xml.DocumentElement.AppendChild(Xml.CreateElement("MaxConexionesGuardadas")).InnerText = MaxConexionesGuardadas.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("TamanoPaqueteKB")).InnerText = TamanoPaqueteKB.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("TamanoBufferKB")).InnerText = TamanoBufferKB.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("DescargasSimultaneas")).InnerText = DescargasSimultaneas.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ConexionesPorFichero")).InnerText = ConexionesPorFichero.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErrores")).InnerText = ResetearErrores.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("QuotaBreakthroughMode")).InnerText = QuotaBreakthroughMode.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErroresMigratedV25")).InnerText = ResetearErroresMigratedV25.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("WorkbenchColumnsMigrated")).InnerText = WorkbenchColumnsMigrated.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ColumnUIDefaultsMigratedV26")).InnerText = ColumnUIDefaultsMigratedV26.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ColumnUIDefaultsMigratedV25")).InnerText = ColumnUIDefaultsMigratedV26.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("ApagarPC")).InnerText = ApagarPC.ToString();
            Xml.DocumentElement.AppendChild(DownloadSchedule.ToXml(Xml));

            Xml.DocumentElement.AppendChild(Xml.CreateElement("ComenzarDescargando")).InnerText = ComenzarDescargando.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("CheckUpdates")).InnerText = CheckUpdates.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("UpdateCheckIntervalHours")).InnerText = UpdateCheckIntervalHours.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("UpdateManualOnly")).InnerText = UpdateManualOnly.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("UpdateSkipVersion")).InnerText = UpdateSkipVersion;

            Xml.DocumentElement.AppendChild(Xml.CreateElement("MantenerUltimaConfiguracion")).InnerText = MantenerUltimaConfiguracion.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("UsarProxy")).InnerText = UsarProxy.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyPort")).InnerText = ProxyPort.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyIP")).InnerText = ProxyIP;
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyUser")).InnerText = ProxyUser;
            // P0-4:口令密文写盘后移（见比对后 DPAPI 块）。此处不写明文节点，
            // 旧明文值在首次保存后即被密文替换（向前兼容读侧保留明文回退）。

            Xml.DocumentElement.AppendChild(Xml.CreateElement("VLCPath")).InnerText = VLCPath;

            Xml.DocumentElement.AppendChild(Xml.CreateElement("ResetearErroresPeriodoMinutos")).InnerText = ResetearErroresPeriodoMinutos.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("NivelLog")).InnerText = Enum.GetName(typeof(Log.LevelLogType), NivelLog);

            Xml.DocumentElement.AppendChild(Xml.CreateElement("PrioridadDescompresion")).InnerText = Enum.GetName(typeof(DescompresionPriority), PrioridadDescompresion);

            Xml.DocumentElement.AppendChild(Xml.CreateElement("LimiteVelocidadKBs")).InnerText = LimiteVelocidadKBs.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("IniciarConWindows")).InnerText = IniciarConWindows.ToString();
            if (NoMostrarAvisoURI)
                Xml.DocumentElement.AppendChild(Xml.CreateElement("NoMostrarAvisoURI")).InnerText = NoMostrarAvisoURI.ToString();

            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingActivo")).InnerText = ServidorStreamingActivo.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingPuerto")).InnerText = ServidorStreamingPuerto.ToString();
            // P0-4:同 ProxyPassword，密文节点后移，见下。

            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebActivo")).InnerText = ServidorWebActivo.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebNombre")).InnerText = ServidorWebNombre;
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebRutaPlantilla")).InnerText = ServidorWebRutaPlantilla;
            // B2-⑦:ServidorWebPassword 密文(随机 IV,每次不同)不得参与去重比对,
            // 否则 OuterXml 恒变、后台每 5s 必落盘。此处先不写该节点,比对通过后再补(见下)。
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebTimeout")).InnerText = ServidorWebTimeout.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPermitirLAN")).InnerText = ServidorWebPermitirLAN.ToString();
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebBindIP")).InnerText = ServidorWebBindIP;
            Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPuerto")).InnerText = ServidorWebPuerto.ToString();

            if (HideCollaborateButton)
                Xml.DocumentElement.AppendChild(Xml.CreateElement("HideCollaborateButton")).InnerText = HideCollaborateButton.ToString();


            ConfigUI.GuardarXML(ref Xml);

            string Fichero;

            Fichero = _configurationPath ?? ObtenerRutaFicheroConfiguracion();

            // B1/B2:加载降级期间一律拒写,不再依赖 File.Exists —— 文件被杀软隔离/移走时
            // 旧条件会失效,默认值就能落盘并毁掉用户配置。真正的恢复由主窗体的
            // 计时器完成(TryRecoverFromTransientLock),保存路径不擅自重载,以免把用户
            // 当前正在编辑的字段覆盖回磁盘旧值还弹出"保存成功"。
            if (LoadTransientError)
            {
                if (!_TransientSkipLogged)
                {
                    _TransientSkipLogged = true;
                    Log.WriteWarning("Skipping configuration save: the on-disk file could not be loaded earlier (locked). It is preserved untouched.");
                }
                ErrorConfig = ErrorConfigClass.Fichero_Bloqueado;
                return;
            }

            string curWebPasswordPlain = ServidorWebPassword ?? "";
            string curProxyPasswordPlain = ProxyPassword ?? "";
            string curStreamingPasswordPlain = ServidorStreamingPassword ?? "";
            // P-14: periodic saves (ForzarGuardado=False) used to walk every account,
            // DPAPI-decrypt two SecureStrings and hash them every 5 seconds even when
            // nothing changed. Reuse the fingerprint that was actually persisted, but
            // force a real recomputation on explicit saves and at most once per minute,
            // so a mutation that bypasses the settings dialog is still detected.
            string curELCAccountsHash;
            if (!ForzarGuardado && _LastSavedELCAccountsHash is not null && _LastSavedXML is not null && (DateTime.Now - _lastELCHashUtc).TotalSeconds < 60d)
            {
                curELCAccountsHash = _LastSavedELCAccountsHash;
            }
            else
            {
                curELCAccountsHash = ELCAccountsHash();
                _lastELCHashUtc = DateTime.Now;
            }
            if ((_LastSavedXML is null || (_LastSavedXML ?? "") != (Xml.DocumentElement.OuterXml ?? "") || _LastSavedWebPasswordPlain is null || (_LastSavedWebPasswordPlain ?? "") != (curWebPasswordPlain ?? "") || _LastSavedProxyPasswordPlain is null || (_LastSavedProxyPasswordPlain ?? "") != (curProxyPasswordPlain ?? "") || _LastSavedStreamingPasswordPlain is null || (_LastSavedStreamingPasswordPlain ?? "") != (curStreamingPasswordPlain ?? "") || _LastSavedELCAccountsHash is null || (_LastSavedELCAccountsHash ?? "") != (curELCAccountsHash ?? "")) | ForzarGuardado)
            {

                _LastSavedXML = Xml.DocumentElement.OuterXml;
                _LastSavedWebPasswordPlain = curWebPasswordPlain;
                _LastSavedProxyPasswordPlain = curProxyPasswordPlain;
                _LastSavedStreamingPasswordPlain = curStreamingPasswordPlain;
                // R-P0-1:Nothing(账号列表正被 SaveToConfig 重建)也照存,下个周期会因
                // "_LastSavedELCAccountsHash Is Nothing" 再存一次盘 —— 方向安全,只会多写不会漏写。
                _LastSavedELCAccountsHash = curELCAccountsHash;

                // B2-⑦:随机 IV 密文只写盘、不比对。加密失败返回 Nothing 时跳过该节点,
                // 保留磁盘旧密文而非存入空值(与旧逻辑一致,只是位置后移)。
                string encryptedWebPassword = Criptografia.AES_EncryptString(ServidorWebPassword, KeyPassword);
                if (encryptedWebPassword is not null)
                {
                    Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorWebPassword")).InnerText = encryptedWebPassword;
                }

                // Como el usuario y password se guarda cifrado con entropia, cada vez tendrá un valor distinto, no podemos compararlos...
                Xml.DocumentElement.AppendChild(Xml.CreateElement("Usuario")).InnerText = Criptografia.EncryptString_DPAPI(_Usuario);
                Xml.DocumentElement.AppendChild(Xml.CreateElement("Password")).InnerText = Criptografia.EncryptString_DPAPI(_Password);
                // P0-4:代理/流媒体口令 DPAPI 密文（非确定性，只写盘不比对）。
                // 空值照写空节点（读侧缺省空字串，语义不变）；仅加密失败返回 Nothing 时跳过
                // 该节点，保留磁盘旧值而非存入空值覆盖。
                string encryptedProxyPassword = EncryptPasswordField(ProxyPassword);
                if (encryptedProxyPassword is not null)
                {
                    Xml.DocumentElement.AppendChild(Xml.CreateElement("ProxyPassword")).InnerText = encryptedProxyPassword;
                }
                string encryptedStreamingPassword = EncryptPasswordField(ServidorStreamingPassword);
                if (encryptedStreamingPassword is not null)
                {
                    Xml.DocumentElement.AppendChild(Xml.CreateElement("ServidorStreamingPassword")).InnerText = encryptedStreamingPassword;
                }
                Xml.DocumentElement.AppendChild(SavePreSharedKeys(ref Xml));


                var AccountsNode = Xml.DocumentElement.AppendChild(Xml.CreateElement("ELCAccounts"));
                // P1-5:持统一锁遍历 —— 本循环在 GuardarXML 的 Try 之外,此前
                // ELCAccountHelper.SaveToConfig 就地重建列表会抛 "Collection was modified"
                // / ObjectDisposedException 直接炸死磁盘 worker(自动存盘全停)。
                lock (ELCAccountHelper.ELCAccountsLock)
                {
                    foreach (ELCAccountHelper.Account Account in ELCAccounts)
                    {
                        var AccountNode = AccountsNode.AppendChild(Xml.CreateElement("Account"));
                        AccountNode.AppendChild(Xml.CreateElement("Name")).InnerText = Account.Alias;
                        AccountNode.AppendChild(Xml.CreateElement("URL")).InnerText = Account.URL;
                        AccountNode.AppendChild(Xml.CreateElement("User")).InnerText = Criptografia.EncryptString_DPAPI(Account.User);
                        AccountNode.AppendChild(Xml.CreateElement("Key")).InnerText = Criptografia.EncryptString_DPAPI(Account.Key);
                        AccountNode.AppendChild(Xml.CreateElement("Default")).InnerText = Account.DefaultAccount ? "1" : "0";
                    }
                }

                Log.WriteDebug("Saving configuration");

                lock (Mutex.GuardarConfig)
                {
                    try
                    {
                        AtomicFile.SaveXml(Xml, Fichero);
                        // P0-2: clear the sticky error flag on success. Without this, one transient
                        // failure (e.g. disk full) keeps ErrorConfig non-zero forever, so every later
                        // save shows a bogus "Configuration could not be saved" and Configuration.vb
                        // then Exit-Subs away the rest of the settings application.
                        // Clearing unconditionally is safe ONLY because the LOAD result now lives in
                        // LoadErrorConfig (see its declaration) - do not merge the two back together.
                        ErrorConfig = ErrorConfigClass.SinErrores;
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error saving configuration XML: " + Log.SafeException(ex));
                        ErrorConfig = ErrorConfigClass.Fichero_No_Creado;
                        _LastSavedXML = null;
                        _LastSavedWebPasswordPlain = null;
                        _LastSavedProxyPasswordPlain = null;
                        _LastSavedStreamingPasswordPlain = null;
                        _LastSavedELCAccountsHash = null;
                    }
                }



            }
        }

        public XmlElement SavePreSharedKeys(ref XmlDocument XmlDoc)
        {
            var list = XmlDoc.CreateElement("PreSharedKeys");
            foreach (SecureString key in ListaPreSharedKeys)
                list.AppendChild(XmlDoc.CreateElement("Key")).InnerText = Criptografia.EncryptString_DPAPI(key);
            return list;
        }

        /// <summary>公开的无参入口保持不变,避免改变既有调用方/反射可见的方法签名。</summary>
        public void CargaXML()
        {
            CargaXMLInterno(false);
        }

        /// <summary>Recovery retries the source that was locked without resetting the current configuration.</summary>
        private void CargaXMLInterno(bool soloRecuperar)
        {

            string Fichero;

            Fichero = _configurationPath ?? ObtenerRutaFicheroConfiguracion();

            Log.WriteDebug("Loading configuration XML");

            XmlDocument Xml = null;
            bool recoveredFromBackup = false;
            bool transientFailure = false;

            if (soloRecuperar)
            {
                if (!LoadTransientError)
                    return;
                if (!AtomicFile.TryReloadAfterTransientFailure(Fichero, _transientBlockedSource, ref Xml))
                    return;
            }

            if (!soloRecuperar)
            {
                lock (Mutex.GuardarConfig)
                {
                    if (!AtomicFile.TryLoadXmlWithRecoveryInfo(Fichero, ref Xml, ref recoveredFromBackup, ref transientFailure, ref _transientBlockedSource, _initialReadAttempts, AtomicFile.StartupRetryDelayMs))
                    {
                        if (!File.Exists(Fichero) && !File.Exists(Fichero + ".bak"))
                        {
                            Log.WriteWarning("Configuration file does not exist");
                            ErrorConfig = ErrorConfigClass.Fichero_No_Existe;
                            LoadErrorConfig = ErrorConfigClass.Fichero_No_Existe;
                        }
                        else
                        {
                            ErrorConfig = ErrorConfigClass.Fichero_No_Valido;
                            LoadErrorConfig = ErrorConfigClass.Fichero_No_Valido;
                            // P0-3:被占用(杀软/备份/同步盘)≠损坏。内存照跑默认值,但磁盘文件
                            // 可能完好,置标记让 GuardarXML 一律拒写,保住旧文件。
                            LoadTransientError = transientFailure;
                            if (transientFailure)
                            {
                                Log.WriteError("Configuration file is locked by another process (transient). Running with defaults; writes are suspended to preserve the on-disk file.");
                            }
                            else
                            {
                                Log.WriteError("Configuration file could not be loaded (primary and backup failed).");
                            }
                        }
                        ConfiguracionDefectoVacia();
                        return;
                    }
                    if (recoveredFromBackup)
                    {
                        Log.WriteWarning("Configuration restored from backup file.");
                    }
                }
            }

            // P-21: 一次遍历代替 60+ 次 XPath 全文档查找。只缓存顶层元素的
            // 名称→InnerText;带路径/谓词的查询仍走原 SelectSingleNode。
            _loadedNodes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XmlNode node in Xml.DocumentElement.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Element && !_loadedNodes.ContainsKey(node.Name))
                {
                    _loadedNodes[node.Name] = node.InnerText;
                }
            }

            string argPath = "Language";
            Idioma = Language.NormalizeLanguagePreference(LeerNodo(ref Xml, ref argPath, ""));


            string argPath1 = "VersionConfig";
            VersionConfig = LeerNodo(ref Xml, ref argPath1, "0");
            string argPath2 = "RutaDefecto";
            RutaDefecto = LeerNodo(ref Xml, ref argPath2, "");
            string argPath3 = "ProxyIP";
            ProxyIP = LeerNodo(ref Xml, ref argPath3, "");
            string argPath4 = "ProxyUser";
            ProxyUser = LeerNodo(ref Xml, ref argPath4, "");
            string argPath5 = "ProxyPassword";
            ProxyPassword = DecryptPasswordField(LeerNodo(ref Xml, ref argPath5, ""), "ProxyPassword", ref ProxyPasswordNeedsReentry);
            string argPath6 = "VLCPath";
            VLCPath = LeerNodo(ref Xml, ref argPath6, "");
            string argPath7 = "Usuario";
            _Usuario = Criptografia.DecryptString_DPAPI(LeerNodo(ref Xml, ref argPath7, ""));
            string argPath8 = "Password";
            _Password = Criptografia.DecryptString_DPAPI(LeerNodo(ref Xml, ref argPath8, ""));
            ExtraerAutomaticamente = false;
            CrearDirectorioPaquete = false;
            AnalizarPortapapeles = false;
            // PermitirSkins = True
            ResetearErrores = false;
            QuotaBreakthroughMode = false;
            UsarProxy = false;
            ApagarPC = false;
            DownloadSchedule = new DownloadScheduleSettings();
            ComenzarDescargando = true;
            IniciarConWindows = false;
            MantenerUltimaConfiguracion = true;
            HideCollaborateButton = false;
            CheckUpdates = true;
            string argPath9 = "CheckUpdates";
            bool.TryParse(LeerNodo(ref Xml, ref argPath9, "true"), out CheckUpdates);
            // 6: 新字段缺失时回退默认（间隔 1h / 非手动），旧配置向前兼容。
            int storedInterval;
            string argPath10 = "UpdateCheckIntervalHours";
            if (int.TryParse(LeerNodo(ref Xml, ref argPath10, "1"), out storedInterval) && storedInterval > 0)
            {
                UpdateCheckIntervalHours = storedInterval;
            }
            else
            {
                UpdateCheckIntervalHours = 1;
            }
            string argPath11 = "UpdateManualOnly";
            bool.TryParse(LeerNodo(ref Xml, ref argPath11, "false"), out UpdateManualOnly);
            string argPath12 = "UpdateSkipVersion";
            UpdateSkipVersion = LeerNodo(ref Xml, ref argPath12, "");
            string argPath13 = "HideCollaborateButton";
            bool.TryParse(LeerNodo(ref Xml, ref argPath13, "false"), out HideCollaborateButton);
            string argPath14 = "ExtraerAutomaticamente";
            bool.TryParse(LeerNodo(ref Xml, ref argPath14, "false"), out ExtraerAutomaticamente);
            string argPath15 = "CondicionesAceptadas";
            bool.TryParse(LeerNodo(ref Xml, ref argPath15, "false"), out CondicionesAceptadas);
            string argPath16 = "CrearDirectorioPaquete";
            bool.TryParse(LeerNodo(ref Xml, ref argPath16, "false"), out CrearDirectorioPaquete);
            string argPath17 = "AnalizarPortapapeles";
            bool.TryParse(LeerNodo(ref Xml, ref argPath17, "false"), out AnalizarPortapapeles);
            // Boolean.TryParse(LeerNodo(Xml, "PermitirSkins", "true"), PermitirSkins)
            string argPath18 = "ResetearErrores";
            bool.TryParse(LeerNodo(ref Xml, ref argPath18, "true"), out ResetearErrores);
            string argPath19 = "QuotaBreakthroughMode";
            bool.TryParse(LeerNodo(ref Xml, ref argPath19, "false"), out QuotaBreakthroughMode);
            string argPath20 = "ResetearErroresMigratedV25";
            bool.TryParse(LeerNodo(ref Xml, ref argPath20, "false"), out ResetearErroresMigratedV25);
            string argPath21 = "WorkbenchColumnsMigrated";
            bool.TryParse(LeerNodo(ref Xml, ref argPath21, "false"), out WorkbenchColumnsMigrated);
            string argPath22 = "ColumnUIDefaultsMigratedV26";
            bool.TryParse(LeerNodo(ref Xml, ref argPath22, "false"), out ColumnUIDefaultsMigratedV26);
            bool migratedV25Alias = false;
            string argPath23 = "ColumnUIDefaultsMigratedV25";
            bool.TryParse(LeerNodo(ref Xml, ref argPath23, "false"), out migratedV25Alias);
            if (migratedV25Alias)
                ColumnUIDefaultsMigratedV26 = true;
            bool needV25Migration = !ResetearErroresMigratedV25;
            string argPath24 = "UsarProxy";
            bool.TryParse(LeerNodo(ref Xml, ref argPath24, "false"), out UsarProxy);
            string argPath25 = "IniciarConWindows";
            bool.TryParse(LeerNodo(ref Xml, ref argPath25, "false"), out IniciarConWindows);
            string argPath26 = "NoMostrarAvisoURI";
            bool.TryParse(LeerNodo(ref Xml, ref argPath26, "false"), out NoMostrarAvisoURI);
            string argPath27 = "MantenerUltimaConfiguracion";
            bool.TryParse(LeerNodo(ref Xml, ref argPath27, "true"), out MantenerUltimaConfiguracion);
            string argPath28 = "ComenzarDescargando";
            bool.TryParse(LeerNodo(ref Xml, ref argPath28, "true"), out ComenzarDescargando);
            string argPath29 = "ApagarPC";
            bool.TryParse(LeerNodo(ref Xml, ref argPath29, "false"), out ApagarPC);
            DownloadSchedule = DownloadScheduleSettings.FromXml(Xml.DocumentElement.SelectSingleNode("DownloadSchedule"));
            TamanoPaqueteKB = 50;
            TamanoBufferKB = 750;
            MaxConexionesGuardadas = 100;
            ResetearErroresPeriodoMinutos = 15;
            ProxyPort = 0;
            LimiteVelocidadKBs = 0;
            string argPath30 = "TamanoPaqueteKB";
            int.TryParse(LeerNodo(ref Xml, ref argPath30, "50"), out TamanoPaqueteKB);
            string argPath31 = "TamanoBufferKB";
            int.TryParse(LeerNodo(ref Xml, ref argPath31, "750"), out TamanoBufferKB);
            string argPath32 = "ProxyPort";
            int.TryParse(LeerNodo(ref Xml, ref argPath32, "0"), out ProxyPort);
            string argPath33 = "LimiteVelocidadKBs";
            int.TryParse(LeerNodo(ref Xml, ref argPath33, "0"), out LimiteVelocidadKBs);
            string argPath34 = "MaxConexionesGuardadas";
            int.TryParse(LeerNodo(ref Xml, ref argPath34, "100"), out MaxConexionesGuardadas);
            string argPath35 = "ResetearErroresPeriodoMinutos";
            int.TryParse(LeerNodo(ref Xml, ref argPath35, "15"), out ResetearErroresPeriodoMinutos);
            if (ResetearErroresPeriodoMinutos < 1 | ResetearErroresPeriodoMinutos > 999)
            {
                ResetearErroresPeriodoMinutos = 15;
            }
            if (needV25Migration)
            {
                // v2.5 beta:存量配置一次性迁移到默认开(15min)。新装直接缺省 True,不走这里。
                ResetearErrores = true;
                ResetearErroresMigratedV25 = true;
                Log.WriteWarning("Migrated ResetearErrores to True (v2.5 default-on self-heal).");
            }
            ApplyConfigLimits();

            NivelLog = Log.LevelLogType.Normal;
            string argPath37 = "NivelLog";
            if (Enum.IsDefined(typeof(Log.LevelLogType), LeerNodo(ref Xml, ref argPath37, "")))
            {
                string argPath36 = "NivelLog";
                NivelLog = (Log.LevelLogType)Conversions.ToInteger(Enum.Parse(typeof(Log.LevelLogType), LeerNodo(ref Xml, ref argPath36, "")));
            }

            PrioridadDescompresion = DescompresionPriority.Normal;
            string argPath39 = "PrioridadDescompresion";
            if (Enum.IsDefined(typeof(DescompresionPriority), LeerNodo(ref Xml, ref argPath39, "")))
            {
                string argPath38 = "PrioridadDescompresion";
                PrioridadDescompresion = (DescompresionPriority)Conversions.ToInteger(Enum.Parse(typeof(DescompresionPriority), LeerNodo(ref Xml, ref argPath38, "")));
            }

            DescargasSimultaneas = 3;
            ConexionesPorFichero = 3;
            string argPath40 = "DescargasSimultaneas";
            int.TryParse(LeerNodo(ref Xml, ref argPath40, "3"), out DescargasSimultaneas);
            string argPath41 = "ConexionesPorFichero";
            int.TryParse(LeerNodo(ref Xml, ref argPath41, "3"), out ConexionesPorFichero);
            ApplyConfigLimits();

            // Servidor streaming
            ServidorStreamingActivo = false;
            string argPath42 = "ServidorStreamingActivo";
            bool.TryParse(LeerNodo(ref Xml, ref argPath42, "false"), out ServidorStreamingActivo);
            ServidorStreamingPuerto = DEFAULT_STREAMING_PORT;
            string argPath43 = "ServidorStreamingPuerto";
            int.TryParse(LeerNodo(ref Xml, ref argPath43, DEFAULT_STREAMING_PORT.ToString()), out ServidorStreamingPuerto);
            string argPath44 = "ServidorStreamingPassword";
            ServidorStreamingPassword = DecryptPasswordField(LeerNodo(ref Xml, ref argPath44, ""), "ServidorStreamingPassword", ref ServidorStreamingPasswordNeedsReentry);

            // Servidor web
            ServidorWebActivo = false;
            string argPath45 = "ServidorWebActivo";
            bool.TryParse(LeerNodo(ref Xml, ref argPath45, "false"), out ServidorWebActivo);
            string argPath46 = "ServidorWebNombre";
            ServidorWebNombre = LeerNodo(ref Xml, ref argPath46, "");
            string argPath47 = "ServidorWebRutaPlantilla";
            ServidorWebRutaPlantilla = LeerNodo(ref Xml, ref argPath47, "");
            string argPath48 = "ServidorWebPassword";
            ServidorWebPassword = LeerNodo(ref Xml, ref argPath48, "");
            try
            {
                if (!string.IsNullOrEmpty(ServidorWebPassword))
                {
                    ServidorWebPassword = Criptografia.AES_DecryptString(ServidorWebPassword, KeyPassword);
                }
            }
            catch (Exception ex)
            {
                // 解密失败必须记录并回退:保留密文原值当密码用会导致登录永远失败且日志无线索
                Log.WriteError("ServidorWebPassword decrypt failed; resetting to empty: " + ex.Message);
                ServidorWebPassword = "";
            }
            ServidorWebTimeout = 5;
            string argPath49 = "ServidorWebTimeout";
            int.TryParse(LeerNodo(ref Xml, ref argPath49, "5"), out ServidorWebTimeout);
            ServidorWebPermitirLAN = false;
            string argPath50 = "ServidorWebPermitirLAN";
            bool.TryParse(LeerNodo(ref Xml, ref argPath50, "false"), out ServidorWebPermitirLAN);
            string argPath51 = "ServidorWebBindIP";
            ServidorWebBindIP = LeerNodo(ref Xml, ref argPath51, "");
            ServidorWebPuerto = 0;
            string argPath52 = "ServidorWebPuerto";
            int.TryParse(LeerNodo(ref Xml, ref argPath52, "0"), out ServidorWebPuerto);
            // P3-13: 端口/超时的钳制必须放在解析之后。ApplyConfigLimits 此前在 CargaXML
            // 里只在 :542 和 :558 调用,两次都早于 ServidorStreamingPuerto(:563)、
            // ServidorWebPuerto(:587)、ServidorWebTimeout(:582) 的解析 → 被手改坏的 XML
            // 带来的越界值根本不会被钳,本次启动每次都弹 "Error starting web server",
            // 要等下一次 GuardarXML 才钳回去。
            ApplyConfigLimits();

            // Pre shared keys
            ListaPreSharedKeys = new List<SecureString>();
            foreach (XmlNode keynode in Xml.DocumentElement.SelectNodes("PreSharedKeys/Key"))
                ListaPreSharedKeys.Add(Criptografia.DecryptString_DPAPI(keynode.InnerText));

            if ((_Usuario is null || _Password is null || _Usuario.Length == 0) | _Password.Length == 0)
            {
                // ErrorConfig = ErrorConfigClass.Usuario_Password_Incorrecto
            }
            if (ConexionesPorFichero == 0)
            {
                ErrorConfig = ErrorConfigClass.Fichero_No_Valido;
                // R1: this is a LOAD-path failure too (see LoadErrorConfig) - without this line
                // Main.vb:450 would no longer open the configuration form for this case.
                LoadErrorConfig = ErrorConfigClass.Fichero_No_Valido;
            }


            _ELCAccountList = new List<ELCAccountHelper.Account>();
            foreach (XmlNode accountNodeItem in Xml.DocumentElement.SelectNodes("ELCAccounts/Account"))
            {
                XmlNode AccountNode = accountNodeItem;
                var newAccount = new ELCAccountHelper.Account();
                string argPath53 = "User";
                newAccount.User = Criptografia.DecryptString_DPAPI(LeerNodo(ref AccountNode, ref argPath53, ""));
                string argPath54 = "Key";
                newAccount.Key = Criptografia.DecryptString_DPAPI(LeerNodo(ref AccountNode, ref argPath54, ""));
                string argPath55 = "URL";
                newAccount.URL = LeerNodo(ref AccountNode, ref argPath55, "");
                string argPath56 = "Name";
                newAccount.Alias = LeerNodo(ref AccountNode, ref argPath56, "");
                if (string.IsNullOrEmpty(newAccount.Alias))
                {
                    newAccount.Alias = newAccount.URL;
                }
                string argPath57 = "Default";
                newAccount.DefaultAccount = LeerNodo(ref AccountNode, ref argPath57, "") == "1";
                _ELCAccountList.Add(newAccount);
            }

            // UI
            ConfigUI.CargarXML(Xml);

            if (soloRecuperar)
            {
                // B2: 恢复完成后解除所有"加载降级"标记,之后的保存回到正常路径。
                // 去重快照必须作废,否则恢复后的第一次真实写盘会被误判为"无变化"。
                LoadTransientError = false;
                _transientBlockedSource = null;
                LoadErrorConfig = ErrorConfigClass.SinErrores;
                ErrorConfig = ErrorConfigClass.SinErrores;
                _TransientSkipLogged = false;
                _LastSavedXML = null;
                _LastSavedWebPasswordPlain = null;
                _LastSavedProxyPasswordPlain = null;
                _LastSavedStreamingPasswordPlain = null;
                _LastSavedELCAccountsHash = null;
                Log.WriteInfo("Configuration recovered after the on-disk file was unlocked.");
            }
            _loadedNodes = null;

        }

        /// <summary>B1/B2: 文件被瞬时占用后,由主窗体的恢复计时器周期调用。
	/// 	''' 成功重载真实配置时返回 True 并解除写保护;仍被占用返回 False,不改动内存状态。</summary>
        public bool TryRecoverFromTransientLock()
        {
            if (!LoadTransientError)
                return false;
            try
            {
                CargaXMLInterno(true);
            }
            catch (Exception ex)
            {
                Log.WriteError("Configuration recovery failed: " + Log.SafeException(ex));
                return false;
            }
            if (LoadTransientError)
                return false;
            try
            {
                Conexion.SetProxy(this);
                MegaQuotaManager.SetBreakthroughMode(QuotaBreakthroughMode);
                ThrottledStreamController.GetController().SetMaxGlobalSpeed(LimiteVelocidadKBs * 1024L);
            }
            catch (Exception ex)
            {
                Log.WriteError("Re-applying runtime settings after recovery failed: " + Log.SafeException(ex));
            }
            return true;
        }

        public void ConfiguracionDefectoVacia()
        {
            RutaDefecto = "";
            ResetearErrores = true;
            ResetearErroresMigratedV25 = true;
            ResetearErroresPeriodoMinutos = 15;
            Idioma = Language.FollowSystemLanguageCode;
            ExtraerAutomaticamente = false;
            CondicionesAceptadas = false;
            CrearDirectorioPaquete = true;
            AnalizarPortapapeles = true;
            HideCollaborateButton = false;
            ApagarPC = false;
            DownloadSchedule = new DownloadScheduleSettings();
            UsarProxy = false;
            // P3-5(b): align the empty-config default with CargaXML (:407 pre-read
            // assignment and :428 node default "true", Forms/Configuration.vb:552 write
            // path). The field default was False here, so a fresh install behaved
            // differently from every loaded/reset configuration (inverted default).
            MantenerUltimaConfiguracion = true;
            ComenzarDescargando = true;
            CheckUpdates = true;
            UpdateCheckIntervalHours = 1;
            UpdateManualOnly = false;
            UpdateSkipVersion = "";
            ProxyPort = 0;
            VersionConfig = "0";
            DescargasSimultaneas = 3;
            ConexionesPorFichero = 3;
            ProxyIP = "";
            ServidorWebTimeout = 5;
            ServidorWebPermitirLAN = false;
            ServidorWebBindIP = "";
            MaxConexionesGuardadas = 100;
            NivelLog = Log.LevelLogType.Normal;
            ServidorStreamingActivo = false;
            ServidorWebActivo = false;
            ServidorStreamingPuerto = DEFAULT_STREAMING_PORT;
            PrioridadDescompresion = DescompresionPriority.Normal;
            _Usuario = Criptografia.ToSecureString("");
            _Password = Criptografia.ToSecureString("");
            ListaPreSharedKeys = new List<SecureString>();
            TamanoPaqueteKB = 50;
            TamanoBufferKB = 750;
            _ELCAccountList = new List<ELCAccountHelper.Account>();
            ConfigUI.ConfiguracionDefectoVacia();
            ApplyConfigLimits();
        }

        /// <summary>
	/// 	''' Clamps configuration values so hand-edited XML cannot bypass UI safety limits.
	/// 	''' </summary>
        public void ApplyConfigLimits()
        {
            const int MaxPackageKB = 1024;
            const int MaxBufferKB = 16 * 1024;
            const int MaxConnections = 32;
            const int MaxSimultaneous = 20;
            const int MaxSpeedKBs = 100 * 1024 * 1024; // 100 GB/s absurd upper bound

            if (LimiteVelocidadKBs < 0)
                LimiteVelocidadKBs = 0;
            if (LimiteVelocidadKBs > MaxSpeedKBs)
                LimiteVelocidadKBs = MaxSpeedKBs;
            if (TamanoPaqueteKB < 1)
                TamanoPaqueteKB = 50;
            if (TamanoPaqueteKB > MaxPackageKB)
                TamanoPaqueteKB = MaxPackageKB;
            if (TamanoBufferKB < 1)
                TamanoBufferKB = 750;
            if (TamanoBufferKB > MaxBufferKB)
                TamanoBufferKB = MaxBufferKB;
            if (TamanoBufferKB < TamanoPaqueteKB)
                TamanoBufferKB = Math.Min(MaxBufferKB, TamanoPaqueteKB * 5);
            if (DescargasSimultaneas < 1)
                DescargasSimultaneas = 3;
            if (DescargasSimultaneas > MaxSimultaneous)
                DescargasSimultaneas = MaxSimultaneous;
            if (ConexionesPorFichero < 1)
                ConexionesPorFichero = 3;
            if (ConexionesPorFichero > MaxConnections)
                ConexionesPorFichero = MaxConnections;
            if (MaxConexionesGuardadas < 1)
                MaxConexionesGuardadas = 100;
            if (MaxConexionesGuardadas > 10000)
                MaxConexionesGuardadas = 10000;

            // P3-5(a): the UI validates the ports (Forms/Configuration.vb:439/509), but a
            // hand-edited Configuration.xml could inject out-of-range values that reached
            // New WebProxy(host, port) (Conexion.vb:100, hot request path) and the HTTP
            // server binders unvalidated. Clamp here — this method's stated purpose is
            // "hand-edited XML cannot bypass UI safety limits". ProxyPort is only touched
            // while the proxy is enabled: 0 with UsarProxy=False is the documented default
            // ("no port configured") and must survive a fresh/reset config untouched.
            if (UsarProxy && (ProxyPort < 1 || ProxyPort > 65535))
            {
                int fixedProxyPort = ProxyPort > 65535 ? 65535 : 8080;
                Log.WriteWarning("Proxy port " + ProxyPort + " is out of range; clamped to " + fixedProxyPort + ".");
                ProxyPort = fixedProxyPort;
            }
            if (ServidorStreamingPuerto < 0)
            {
                // 0 keeps its documented "not configured" meaning (empty textbox in the UI);
                // only clearly invalid negatives are normalized.
                Log.WriteWarning("Streaming port " + ServidorStreamingPuerto + " is negative; reset to 0 (not configured).");
                ServidorStreamingPuerto = 0;
            }
            else if (ServidorStreamingPuerto > 65535)
            {
                Log.WriteWarning("Streaming port " + ServidorStreamingPuerto + " is out of range; clamped to 65535.");
                ServidorStreamingPuerto = 65535;
            }
            if (ServidorWebPuerto < 0)
            {
                Log.WriteWarning("Web server port " + ServidorWebPuerto + " is negative; reset to 0 (not configured).");
                ServidorWebPuerto = 0;
            }
            else if (ServidorWebPuerto > 65535)
            {
                Log.WriteWarning("Web server port " + ServidorWebPuerto + " is out of range; clamped to 65535.");
                ServidorWebPuerto = 65535;
            }
            // P3-13: ServidorWebTimeout 同样没被钳。UI 的合法区间是 0..99
            // (Forms/Configuration.vb:490),越界值一样会让 Web 服务器启动失败。
            if (ServidorWebTimeout < 0)
            {
                Log.WriteWarning("Web server timeout " + ServidorWebTimeout + " is negative; reset to 5 (default).");
                ServidorWebTimeout = 5;
            }
            else if (ServidorWebTimeout > 99)
            {
                Log.WriteWarning("Web server timeout " + ServidorWebTimeout + " is out of range; clamped to 99.");
                ServidorWebTimeout = 99;
            }

            if (!string.IsNullOrWhiteSpace(VLCPath))
            {
                try
                {
                    if (Directory.Exists(VLCPath))
                    {
                    }
                    // OK: directory containing vlc.exe
                    else if (File.Exists(VLCPath) && VLCPath.ToLowerInvariant().EndsWith(".exe"))
                    {
                        VLCPath = Path.GetDirectoryName(VLCPath);
                    }
                    else
                    {
                        Log.WriteWarning("VLC path is invalid and was cleared: " + VLCPath);
                        VLCPath = "";
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("VLC path validation failed, clearing VLCPath (" + (VLCPath ?? "") + "): " + Log.SafeException(ex));
                    VLCPath = "";
                }
            }
        }

        #endregion


        #region Métodos privados


        private string LeerNodo(ref XmlDocument DocumentoXML, ref string Path, string ValorDefecto)
        {
            // P-21: simple top-level names come from the map built once per load.
            if (_loadedNodes is not null && Path.IndexOf('/') < 0 && Path.IndexOf('[') < 0)
            {
                string cached = null;
                if (_loadedNodes.TryGetValue(Path, out cached))
                    return cached;
                return ValorDefecto;
            }
            var nodo = DocumentoXML.DocumentElement.SelectSingleNode(Path);
            if (nodo is null)
            {
                return ValorDefecto;
            }
            else
            {
                return nodo.InnerText;
            }
        }
        private string LeerNodo(ref XmlNode NodoXML, ref string Path, string ValorDefecto)
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

        /// <summary>
	/// 	''' P0-4：口令字段写盘加密。空值返回 ""（节点照写空字串，保持旧行为）；
	/// 	''' 非空返回 "DPAPI:" + DPAPI 密文。加密失败返回 Nothing，调用方跳过该节点
	/// 	''' （保留磁盘旧值，而非存入空值覆盖——与 ServidorWebPassword 模式一致）。
	/// 	''' </summary>
        private static string EncryptPasswordField(string plainValue)
        {
            if (string.IsNullOrEmpty(plainValue))
                return "";
            try
            {
                return DPAPI_PREFIX + Criptografia.EncryptString_DPAPI(Criptografia.ToSecureString(plainValue));
            }
            catch (Exception ex)
            {
                Log.WriteError("EncryptPasswordField: DPAPI encrypt failed, keeping on-disk value: " + Log.SafeException(ex));
                return null;
            }
        }

        /// <summary>
	/// 	''' P0-4：口令字段双读。无前缀=历史明文（直接沿用，迁移由下次保存完成）；
	/// 	''' 有前缀=DPAPI blob，解密成功返回明文，失败则 needsReentry=True 并返回 ""，
	/// 	''' 由设置界面提示用户重填（绝不静默置空当正常值用）。
	/// 	''' </summary>
        private static string DecryptPasswordField(string rawValue, string fieldName, ref bool needsReentry)
        {
            needsReentry = false;
            if (string.IsNullOrEmpty(rawValue))
                return "";
            if (!rawValue.StartsWith(DPAPI_PREFIX, StringComparison.Ordinal))
            {
                return rawValue;
            }
            string blob = rawValue.Substring(DPAPI_PREFIX.Length);
            string plain = "";
            try
            {
                plain = Criptografia.ToInsecureString(Criptografia.DecryptString_DPAPI(blob));
            }
            catch (Exception ex)
            {
                plain = "";
                Log.WriteWarning(fieldName + ": DPAPI decrypt threw, treating as needs-reentry: " + Log.SafeException(ex));
            }
            if (string.IsNullOrEmpty(plain))
            {
                needsReentry = true;
                Log.WriteWarning(fieldName + ": stored password cannot be decrypted on this machine/user (config restored from backup or DPAPI key lost); please re-enter the password in Settings. 口令无法在本机/账户解密（如从备份恢复配置），请在设置中重新输入。");
                return "";
            }
            return plain;
        }



        private static string ObtenerRutaFicheroConfiguracion()
        {

            string PathLog = Path.Combine(ApplicationDataDirectory.GetLegacyRoot(), "Config");

            if (!Directory.Exists(PathLog))
            {
                Directory.CreateDirectory(PathLog);
            }
            PathLog = Path.Combine(PathLog, "Configuration.xml");
            return PathLog;
        }

        #endregion

    }
}