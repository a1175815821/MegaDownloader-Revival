using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BrightIdeasSoftware;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public partial class Main : IDownloaderService
    {

        public Main()
        {


            // WORKERS

            /// <summary>
    /// Se ocupa de comprobar periodicamente el número de conexiones máximas
    /// </summary>
    /// <remarks></remarks>
            _bgwComprobarMaxConexiones = new BackgroundWorker();

            /// <summary>
    /// Se ocupa de refrescar el listado de descarga
    /// </summary>
    /// <remarks></remarks>
            _bgwActualizadorListaDescargas = new BackgroundWorker();

            /// <summary>
    /// Se ocupa de ir actualizando la información de ficheros en cola y de configuracion
    /// </summary>
    /// <remarks></remarks>
            _bgwActualizadorDatosDisco = new BackgroundWorker();

            /// <summary>
    /// Se ocupa de descomprimir los ficheros en segundo plano
    /// </summary>
    /// <remarks></remarks>
            _bgwDescompresor = new BackgroundWorker();
            RetryAllFailedMenuItem = new ToolStripMenuItem();
            RemoveAllFailedMenuItem = new ToolStripMenuItem();
            ResetColumnsMenuItem = new ToolStripMenuItem();
            navQuickExtractButton = default;
            navQuickLibraryButton = default;
            navQuickLogsButton = default;
            SuspendLayout();
            InitializeComponent();
            UiStyles.InitializeForm(this);
            ResumeLayout(false);
        }

        #region Variables internas

        /// <summary>
    /// Configuración de la aplicación (usuario, password, configuración por defecto, etc)
    /// </summary>
    /// <remarks>Es pública para que se pueda modificar desde la pantalla "Configuration".</remarks>
        public Configuracion Config;

        /// <summary>B1/B2: 启动期文件被瞬时占用时,驱动"锁释放后自动重载"的计时器与状态提示。</summary>
        private System.Windows.Forms.Timer _recoveryTimer = null;
        private bool _recoveryBusy = false;
        private System.Windows.Forms.ToolStripStatusLabel _recoveryStatusLabel = null;

        /// <summary>
    /// Indica si ha fallado el usuario y password. Se usa para evitar peticiones innecesarias 
    /// (si falla la identificacion no tiene sentido seguir intentandolo).
    /// </summary>
    /// <remarks>Es pública para que se pueda modificar desde la pantalla "Configuration".</remarks>
        public bool NecesitaCambiarUsuarioYPassword = false;

        /// <summary>
    /// Listado de paquetes y descargas
    /// </summary>
    /// <remarks></remarks>
        private List<Paquete> ListaPaquetes;

        /// <summary>
    /// Objeto que se ocupa de mirar en el portapapeles si hay links
    /// </summary>
    /// <remarks></remarks>
        private ClipboardViewer _clipChange;

        private ClipboardViewer clipChange
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _clipChange;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_clipChange != null)
                {
                    _clipChange.ClipboardChanged -= clipChange_ClipboardChanged;
                }

                _clipChange = value;
                if (_clipChange != null)
                {
                    _clipChange.ClipboardChanged += clipChange_ClipboardChanged;
                }
            }
        }

        /// <summary>
    /// Objeto interno que se encarga de gestionar el "drag and drop" del listado.
    /// </summary>
    /// <remarks></remarks>
        private SimpleDropSink _dropSink;

        private SimpleDropSink dropSink
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _dropSink;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_dropSink != null)
                {
                    _dropSink.ModelDropped -= dropSink_ModelDropped;
                }

                _dropSink = value;
                if (_dropSink != null)
                {
                    _dropSink.ModelDropped += dropSink_ModelDropped;
                }
            }
        }

        /// <summary>
    /// 进度条渲染器(主题切换时需同步颜色)。自定义绘制解决 OLV BarRenderer 三个固有问题:
    /// 背景底色整条灰、渐变分支忽略 FillColor、小进度整型截断无填充。
    /// </summary>
        private ThemeBarRenderer progressBarRenderer;

        private class QueueTreeRenderer : TreeListView.TreeRenderer
        {

            public override void Render(System.Drawing.Graphics g, System.Drawing.Rectangle r)
            {
                DrawBackground(g, r);
                base.Render(g, r);
            }
        }

        /// <summary>进度条与百分比共用一列，先清除原生单元格文字，再绘制主题感知的进度与数值。</summary>
        private class ThemeBarRenderer : BarRenderer
        {

            public override void Render(System.Drawing.Graphics g, System.Drawing.Rectangle r)
            {
                try
                {
                    DrawBackground(g, r);
                    double minV = MinimumValue;
                    double maxV = MaximumValue;
                    double cur = 0.0d;
                    if (Aspect is not null && Information.IsNumeric(Aspect))
                    {
                        cur = Conversions.ToDouble(Aspect);
                    }
                    double frac = 0.0d;
                    if (maxV > minV)
                    {
                        frac = (cur - minV) / (maxV - minV);
                    }
                    if (frac < 0.0d)
                        frac = 0.0d;
                    if (frac > 1.0d)
                        frac = 1.0d;
                    float scale = g.DpiX / 96.0f;
                    int labelWidth = (int)Math.Round(46f * scale);
                    int barH = Math.Max(3, (int)Math.Round(5f * scale));
                    var barR = new System.Drawing.Rectangle(r.X + (int)Math.Round(8f * scale), r.Y + (r.Height - barH) / 2, Math.Max(0, r.Width - labelWidth - (int)Math.Round(20f * scale)), barH);
                    IDescarga model = RowObject as IDescarga;
                    var fill = ThemeManager.GetColor("ProgressFill");
                    if (model is not null)
                    {
                        if (model.DescargaEstado() == Estado.Completado)
                            fill = ThemeManager.GetColor("SuccessFore");
                        if (model.DescargaEstado() == Estado.Erroneo)
                            fill = ThemeManager.GetColor("ErrorFore");
                    }
                    using (var brush = new System.Drawing.SolidBrush(ThemeManager.GetColor("ProgressBack")))
                    {
                        g.FillRectangle(brush, barR);
                    }
                    if (frac > 0d && barR.Width > 0)
                    {
                        using (var brush = new System.Drawing.SolidBrush(fill))
                        {
                            g.FillRectangle(brush, new System.Drawing.Rectangle(barR.X, barR.Y, Math.Min(barR.Width, Math.Max(1, (int)Math.Round(barR.Width * frac))), barR.Height));
                        }
                    }
                    var label = new System.Drawing.Rectangle(r.Right - labelWidth - (int)Math.Round(4f * scale), r.Top, labelWidth, r.Height);
                    using (var format = new System.Drawing.StringFormat() { Alignment = System.Drawing.StringAlignment.Far, LineAlignment = System.Drawing.StringAlignment.Center })
                    {
                        using (var brush = new System.Drawing.SolidBrush(ThemeManager.GetColor("Muted")))
                        {
                            g.DrawString(cur.ToString("0") + "%", ListView.Font, brush, label, format);
                        }
                    }
                }

                catch (Exception ex)
                {
                    base.Render(g, r);
                }
            }
        }

        private BackgroundWorker _bgwComprobarMaxConexiones;
        private BackgroundWorker _bgwActualizadorListaDescargas;
        private BackgroundWorker _bgwActualizadorDatosDisco;
        private BackgroundWorker _bgwDescompresor;

        private bool _bgwComprobarMaxConexionesCompleted = false;
        private bool _bgwActualizadorListaDescargasCompleted = false;
        private bool _bgwActualizadorDatosDiscoCompleted = false;
        // P1-2:解压 worker 看门狗的重启防风暴计数。该 worker 是常驻循环,正常只在关闭时结束;
        // 若它启动即崩,无节制重启会变成 3 秒一次的死循环(比原 bug 更吵)。
        // 记录上次启动时刻:上一轮存活不足 30s 即视为崩溃,连续 5 次则放弃重启并写日志。
        private DateTime _descompresorLastStartUtc = DateTime.MinValue;
        private int _descompresorCrashCount = 0;

        // ACCIONES

        /// <summary>
    /// Indica cuando se ha solicitado guardar la configuración en disco
    /// </summary>
    /// <remarks></remarks>
        private DateTime PeticionGuardadoConfig = DateTime.MinValue;

        /// <summary>
    /// Indica cuando se ha guardado la configuración en disco
    /// </summary>
    /// <remarks></remarks>
        private DateTime UltimoGuardadoConfig = DateTime.MinValue;

        /// <summary>
    /// Indica cuando se ha solicitado guardar el listado de paquetes en disco
    /// </summary>
    /// <remarks></remarks>
        private DateTime PeticionGuardadoFichero = DateTime.MinValue;

        /// <summary>
    /// Indica cuando se ha guardado el listado de paquetes en disco
    /// </summary>
    /// <remarks></remarks>
        private DateTime UltimoGuardadoFichero = DateTime.MinValue;

        /// <summary>
    /// Indica cuando se ha de hacer un flush de memoria
    /// </summary>
    /// <remarks></remarks>

        /// <summary>
    /// Indica la próxima comprobación del número máximo de conexiones
    /// </summary>
    /// <remarks></remarks>
        private DateTime ProximaComprobacionMaxConexiones = DateTime.Now;

        /// <summary>
    /// Velocidad global de todas las descargas en conjunto
    /// </summary>
    /// <remarks></remarks>
        private decimal? VelocidadGlobalDescarga = default;

        /// <summary>
    /// Número de descarga activas (bajando)
    /// </summary>
    /// <remarks></remarks>
        private int? NumDescargasActivas = default;

        /// <summary>
    /// Número de descargas en cola
    /// </summary>
    /// <remarks></remarks>
        private int? NumDescargasEnCola = default;

        /// <summary>
    /// Número de descargas que han dado error
    /// </summary>
    /// <remarks></remarks>
        private int? NumDescargasErroneas = default;

        /// <summary>
    /// Número de descargas que se han completado
    /// </summary>
    /// <remarks></remarks>
        private int? NumDescargasCompletadas = default;

        /// <summary>
    /// 完成/失败通知的状态（仅 430ms 后台循环读写，无需加锁）。
    /// 按"跃迁"通知：首次见到的已完成/已失败不弹（避免启动时对存量红字刷屏），
    /// 只有本轮新进入终态的才弹；包完成只弹一次，重试后再次完成可重弹。
    /// </summary>
        private Dictionary<Fichero, Estado> _lastFileEstado = new Dictionary<Fichero, Estado>();
        private Dictionary<Paquete, bool> _notifiedPackages = new Dictionary<Paquete, bool>();
        private List<DateTime> _recentBalloonTimes = new List<DateTime>();

        /// <summary>
    /// Indica el número máximo de conexiones 
    /// </summary>
    /// <remarks></remarks>
        private int NumeroConexionesMaxima;

        /// <summary>
    /// Nueva versión (si está disponible) del Megadowloader
    /// </summary>
    /// <remarks></remarks>
        private string UrlNuevaVersionMegadownloader;
        private string VersionNuevaVersionMegadownloader;


        // Enabled after Shown; the refresh worker owns the sampler and its Process handle.
        private int _resourceMonitoringEnabled;
        private bool _startupReady = false;
        private bool _startupCompleting;
        private bool _startupNeedsAttention = false;
        private bool _firstPaintLogged = false;

        private void LogStartupStage(string stage)
        {
            Log.WriteWarning("Startup timing " + stage + ": " + My.MyApplication.StartupClock.ElapsedMilliseconds.ToString() + " ms");
        }


        /// <summary>
    /// Indica el estado de la aplicación (descargando, pausa, parado)
    /// </summary>
    /// <remarks></remarks>
        private TipoEstadoAplicacion EstadoAplicacion = TipoEstadoAplicacion.Parado;

        internal enum TipoEstadoAplicacion
        {
            Descargando,
            Pausa,
            Parado
        }

        #endregion

        #region Carga del formulario


        private void Main_Load(object sender, EventArgs e)
        {

            My.MyApplication.Main_Form = this;

            // Language.SaveTranslationReport()

            Log.WriteError("Starting Megadownloader");

            Log.WriteError("Version: " + InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER"));

            PrepareStartupQueue();
            // A locked config is write-protected and recovered without sleeping on the UI.
            Config = new Configuracion(null, 1);
            LogStartupStage("configuration loaded");
            Log.SetLogLevel = Config.NivelLog;
            Language.InitLanguage(Config.Idioma);
            UiStyles.SetAppFont(this, Font.SizeInPoints);
            LogStartupStage("language loaded");

            bool Silent = IsSilent();

            SplashScreen s = null;
            if (!Silent)
            {
                s = new SplashScreen();
                s.Show();
            }

            Visible = false;
            StartPosition = (System.Windows.Forms.FormStartPosition)Conversions.ToInteger(FormStartPosition.CenterScreen);

            Translate();

            clipChange = new ClipboardViewer();
            clipChange.AssignHandle(Handle);
            clipChange.Install();

            CrearMenus();
            InitWorkbench();
            InitBatchMenu();
            InitQuotaBanner();
            InitFailureBanner();
            InitDownloadAreaLayout();

            // btnCollaborate.Visible = Not Config.HideCollaborateButton
            btnCollaborate.Visible = false; // Lo quitamos... nadie lo usa...
            btnUpdate.Visible = false;
            // RC:状态栏 RAM/Proc 固定 90px 运行时必截断,改自动宽度。
            try
            {
                RAMProcToolStripStatusLabel.AutoSize = true;
            }
            catch
            {
            }

            if (WindowState != FormWindowState.Minimized)
            {
                IconoMinimizado.Visible = false;
            }

            // The initial empty view is replaced atomically after background recovery.

            // Usamos esta librería para pintar la lista de descargas:
            // http://objectlistview.sourceforge.net/cs/index.html
            DefinirColumnas();
            ListaDescargas.SetObjects(ListaPaquetes);

            // 左栏总览/快捷入口:必须在 DefinirColumnas→InitNavList 建好 5 个过滤器项之后再建,
            // 否则 navListBox.Items.Count 还是 0,拿不到正确高度,总览会压在列表上。
            InitNavRail();
            UpdateNavRailOverview();
            // Apply colors before the first visible frame, including all runtime controls.
            try
            {
                ApplyCurrentTheme();
            }
            catch (Exception ex)
            {
                Log.WriteError("Failed to apply theme: " + ex.ToString());
            }

            DescompresionThrottle.DecompressionPriority = Config.PrioridadDescompresion;

            Conexion.PingMega();

            NumeroConexionesMaxima = Config.MaxConexionesGuardadas;

            // If Me.Config.PermitirSkins And Not String.IsNullOrEmpty(Me.Config.ConfigUI.RutaSkin) AndAlso System.IO.File.Exists(Me.Config.ConfigUI.RutaSkin) Then
            // SkinEngine.SkinFile = Me.Config.ConfigUI.RutaSkin
            // If Not SkinEngine.Active Then
            // SkinEngine.Active = True
            // End If
            // End If

            if (Config.IniciarConWindows)
            {
                StartupRegistration.RegisterInStartup(true); // Actualizamos con la ruta actual (por si hemos movido el ejecutable de sitio)
            }

            if (!Silent)
            {
                s.Close();
                Visible = true;
            }

            // Autodetect mega:// parameters from browser
            // N-P2-6: surface a registration failure instead of discarding the return value.
            // On a fresh install run without admin rights (the default, manifest is
            // asInvoker) the HKCR write is denied and mega:// links silently do nothing;
            // the user had no way to learn that one elevated run is required.
            var uriRegError = MegaURIProtocol.RegisterUrlProtocol();
            if (uriRegError is not null)
            {
                Log.WriteWarning("mega:// protocol registration failed: " + Log.SafeException(uriRegError));
                // P1-1:silent 自启时绝不能弹模态框——无属主框会阻塞 Main_Load,
                // ComenzarDescargando/StartWebServer/ProcessArgs 全部执行不到,程序看似"不下载"。
                if (Silent)
                {
                    Log.WriteWarning("Silent mode: suppressing the mega:// registration warning dialog.");
                }
                else if (!Config.NoMostrarAvisoURI)
                {
                    try
                    {
                        DialogResult resp = MessageBox.Show(Language.GetText("The mega:// protocol could not be registered, so links opened from the browser will not start MegaDownloader. Run the application once as administrator to register it.") + Constants.vbNewLine + Constants.vbNewLine + Language.GetText("Do not show this message again?"), Language.GetText("Warning"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                        if (resp == System.Windows.Forms.DialogResult.Yes)
                        {
                            Config.NoMostrarAvisoURI = true;
                            if (Config.LoadErrorConfig == Configuracion.ErrorConfigClass.SinErrores && Config.CondicionesAceptadas)
                                Config.GuardarXML(false);
                        }
                    }
                    catch
                    {
                    }
                }
            }


        }

        private void Main_Shown(object sender, EventArgs e)
        {

            LogStartupStage("shown event");

            Text = InternalConfiguration.ObtenerNombreApp() + InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER");

            if (Config.ConfigUI.AltoVentanaPrincipal > 0 & Config.ConfigUI.AnchoVentanaPrincipal > 0)
            {
                Log.WriteDebug("Window size - X: " + Config.ConfigUI.AnchoVentanaPrincipal + " Y:" + Config.ConfigUI.AltoVentanaPrincipal);
                Size = new System.Drawing.Size(Config.ConfigUI.AnchoVentanaPrincipal, Config.ConfigUI.AltoVentanaPrincipal);
                StartPosition = (System.Windows.Forms.FormStartPosition)Conversions.ToInteger(FormStartPosition.CenterScreen);
                Main_Resize(null, null);
            }

            // Centramos la pantalla
            // http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
            var scr = Screen.FromPoint(Location);
            ClampMainWindowToWorkingArea(scr.WorkingArea);
            Size = new Size(Math.Min(Width, scr.WorkingArea.Width), Math.Min(Height, scr.WorkingArea.Height));
            Location = new Point(scr.WorkingArea.Left + (scr.WorkingArea.Width - Width) / 2, scr.WorkingArea.Top + (scr.WorkingArea.Height - Height) / 2);


            if (Config.ConfigUI.EstadoLista is not null)
            {
                Log.WriteDebug("Restoring columns");
                ListaDescargas.RestoreState(Config.ConfigUI.EstadoLista);
            }

            // P0-1 UI:一次性列默认集(开 Progreso%、藏 Descargado、Estado 加宽)。只跑一次,不覆盖用户之后的手动调整。
            if (!Config.ColumnUIDefaultsMigratedV26)
            {
                ApplyColumnDefaults();
                Config.ColumnUIDefaultsMigratedV26 = true;
                Config.ConfigUI.EstadoLista = ListaDescargas.SaveState();
                if (Config.LoadErrorConfig == Configuracion.ErrorConfigClass.SinErrores && Config.CondicionesAceptadas)
                    Config.GuardarXML(false);
            }
            else if (RepairColumnStateIfCorrupted())
            {
                // 坏列状态自愈:持久化曾被拖成 0/上千像素(# 0 + 文件名 0 + 横向滚动条),
                // 迁移已跑过不会重跑,此处按宽度 sanity 修一次并落盘,用户重启即恢复。
                Config.ConfigUI.EstadoLista = ListaDescargas.SaveState();
                if (Config.LoadErrorConfig == Configuracion.ErrorConfigClass.SinErrores && Config.CondicionesAceptadas)
                    Config.GuardarXML(false);
            }

            // The first visual upgrade replaces the old index-based column layout.
            // Later column-menu choices remain authoritative across restarts.
            if (!Config.WorkbenchColumnsMigrated)
            {
                // TreeListView uses its first column for the tree. Move the name to
                // that slot and migrate the old index-based column state once.
                ApplyColumnDefaults();
                Config.WorkbenchColumnsMigrated = true;
                Config.ConfigUI.EstadoLista = ListaDescargas.SaveState();
                if (Config.LoadErrorConfig == Configuracion.ErrorConfigClass.SinErrores && Config.CondicionesAceptadas)
                    Config.GuardarXML(false);
            }
            // RestoreState / RebuildColumns can recreate the native list and its header.
            ThemeManager.RefreshListTheme(ListaDescargas);

            if (IsSilent())
            {
                IconoMinimizado.Text = "MegaDownloader started";
                WindowState = (System.Windows.Forms.FormWindowState)Conversions.ToInteger(FormWindowState.Minimized);
            }
            if (_startupQueueLoading)
            {
                BeginStartupQueueLoad();
            }
            else
            {
                ContinueStartupAfterQueueLoad();
            }
        }

        private void CompleteStartup()
        {
            if (_startupReady || _startupCompleting || Cerrando || IsDisposed || System.Threading.Volatile.Read(ref _startupQueueLoading) || Paquete.HasTransientLoadError || Config is null)
                return;
            _startupCompleting = true;
            try
            {
                CompleteStartupCore();
            }
            finally
            {
                _startupCompleting = false;
            }
        }

        private void CompleteStartupCore()
        {
            IniciarRecuperacionTransitoria();
            // Defaults do not represent the user's start, proxy, speed or schedule
            // preferences. Keep the UI/recovery timer alive without starting workers.
            if (Config.LoadTransientError)
                return;
            _startupNeedsAttention = false;
            if (!CheckMEGAConditions())
                return;
            CheckVersionStatistics();
            // B1: 瞬时占用不是"配置损坏"。磁盘文件可能完全完好,此时不得强制弹首次配置
            // 向导(否则用户保存必失败、关窗即强退);恢复计时器会在锁释放后自动重载。
            if (Config.LoadErrorConfig != Configuracion.ErrorConfigClass.SinErrores && !Config.LoadTransientError)
            {
                Log.WriteWarning("Invalid configuration, opening configuration form.");
                using (var frmName = new Configuration())
                {
                    frmName.MainForm = this;
                    frmName.Config = Config;
                    frmName.RequiereConfiguracion = true;
                    frmName.ShowDialog(this);
                    // B1: 降级保存失败已在 Configuration.btnGuardar_Click 里置位并结束向导,
                    // 这里再兜一层,任何情况下都不因"文件被占用"把整个程序关掉。
                    if (!frmName.StartupConfigurationSaved && !Config.LoadTransientError)
                    {
                        Log.WriteWarning("Startup cancelled before configuration was saved.");
                        _ForzarCierre = true;
                        Close();
                        return;
                    }
                }
            }

            ApplyRuntimeDownloadSettings();
            if (Config.ComenzarDescargando && Config.DownloadSchedule.AllowsDownload(DateTime.Now))
            {
                EstadoAplicacion = TipoEstadoAplicacion.Descargando;
            }
            InitDownloadScheduler();
            Log.WriteInfo("Starting workers");
            _bgwComprobarMaxConexiones.WorkerReportsProgress = true;
            _bgwComprobarMaxConexiones.WorkerSupportsCancellation = true;
            _bgwActualizadorListaDescargas.WorkerReportsProgress = true;
            _bgwActualizadorListaDescargas.WorkerSupportsCancellation = true;
            _bgwActualizadorDatosDisco.WorkerReportsProgress = true;
            _bgwActualizadorDatosDisco.WorkerSupportsCancellation = true;
            _bgwDescompresor.DoWork += DescompresorController.DescompresorController_DoWork;
            _bgwDescompresor.WorkerReportsProgress = true;
            _bgwDescompresor.WorkerSupportsCancellation = true;
            _descompresorLastStartUtc = DateTime.UtcNow;
            _startupReady = true;
            var updateReminderTimer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
            updateReminderTimer.Tick += UpdateReminder_Tick;
            updateReminderTimer.Start();
            _bgwComprobarMaxConexiones.RunWorkerAsync();
            _bgwActualizadorListaDescargas.RunWorkerAsync();
            _bgwActualizadorDatosDisco.RunWorkerAsync();
            _bgwDescompresor.RunWorkerAsync();

            StartConfiguredWebServers();

            ProcessArgs(Environment.GetCommandLineArgs());
            ProcessDeferredStartupArguments();

            // Resource statistics must not hold up the first window. Sampling starts on the
            // existing refresh worker after the Shown handler has finished preparing the UI.
            System.Threading.Volatile.Write(ref _resourceMonitoringEnabled, 1);
            Log.WriteInfo("Start process finished");
            LogStartupStage("startup ready");
            if (_startupQueueCompletion is not null)
                _startupQueueCompletion.TrySetResult(true);
        }

        private void ApplyRuntimeDownloadSettings()
        {
            lock (Mutex.NumeroConexionesMaxima)
                NumeroConexionesMaxima = Config.MaxConexionesGuardadas;
            DescompresionThrottle.DecompressionPriority = Config.PrioridadDescompresion;
        }

        private void StartConfiguredWebServers()
        {
            string strErrServidorWeb = WebServerHost.Start(this, Config);
            if (!string.IsNullOrEmpty(strErrServidorWeb))
            {
                Log.WriteError("Error starting web server: " + strErrServidorWeb);
                if (IsSilent() && WindowState == FormWindowState.Minimized)
                {
                    IconoMinimizado.ShowBalloonTip(5000, Text, Language.GetText("Error starting web server") + ": " + strErrServidorWeb, ToolTipIcon.Error);
                }
                else
                {
                    MessageBox.Show(Language.GetText("Error starting web server") + ": " + strErrServidorWeb, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ResumeAfterConfigurationRecovery()
        {
            UpdateStartupActionAvailability();
            if (!_startupReady)
            {
                if (IsSilent() && WindowState == FormWindowState.Minimized && (!Config.CondicionesAceptadas || Config.LoadErrorConfig != Configuracion.ErrorConfigClass.SinErrores))
                {
                    _startupNeedsAttention = true;
                    IconoMinimizado.ShowBalloonTip(5000, Text, Language.GetText("Startup requires attention. Open the main window to continue."), ToolTipIcon.Warning);
                    return;
                }
                CompleteStartup();
                return;
            }
            ApplyRuntimeDownloadSettings();
            WebServerHost.Stop();
            StartConfiguredWebServers();
            scheduleController.Reset();
            ScheduleTick(null, EventArgs.Empty);
        }

        /// <summary>B1/B2: 启动降级后每秒尝试一次重载,并在状态栏持续提示。
    /// 计时器由消息泵驱动、只在未恢复期间运行,不触碰 430ms 下载刷新主循环。</summary>
        private bool HayCargaBloqueada()
        {
            return Config is not null && Config.LoadTransientError || Paquete.HasTransientLoadError || StreamingLibraryManager.HasPendingRecovery();
        }

        private void IniciarRecuperacionTransitoria()
        {
            // The streaming library instance is created lazily on first use, so a lock on
            // that file can be detected *after* startup. Keep a 1s heartbeat (three cheap
            // boolean reads) instead of only arming the timer when startup already saw a
            // degraded source; the tick stops it while the form is closing.
            if (_recoveryTimer is null)
            {
                _recoveryTimer = new System.Windows.Forms.Timer();
                _recoveryTimer.Interval = 1000;
                _recoveryTimer.Tick += RecuperacionTransitoria_Tick;
            }
            if (HayCargaBloqueada())
            {
                MostrarAvisoCargaBloqueada();
                Log.WriteWarning("Startup data is degraded by a transient file lock; automatic recovery is active.");
            }
            if (!_recoveryTimer.Enabled)
                _recoveryTimer.Start();
        }

        private void MostrarAvisoCargaBloqueada()
        {
            try
            {
                if (_recoveryStatusLabel is null)
                {
                    _recoveryStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
                    _recoveryStatusLabel.Name = "RecoveryStatusLabel";
                    _recoveryStatusLabel.ForeColor = System.Drawing.Color.OrangeRed;
                    StatusStrip1.Items.Add(_recoveryStatusLabel);
                }
                // 损坏与被占用是两回事:前者不会"文件释放后自动恢复",照抄占用文案会让用户白等。
                if (Paquete.HasCorruptLoadError)
                {
                    _recoveryStatusLabel.Text = Language.GetText("Queue_LoadCorruptWarning");
                }
                else
                {
                    _recoveryStatusLabel.Text = Language.GetText("Config_LoadLockedWarning");
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Showing transient-lock banner failed: " + Log.SafeException(ex));
            }
        }

        private async void RecuperacionTransitoria_Tick(object sender, EventArgs e)
        {
            if (Cerrando || IsDisposed)
            {
                if (_recoveryTimer is not null)
                    _recoveryTimer.Stop();
                return;
            }
            if (_recoveryBusy)
                return;
            _recoveryBusy = true;
            try
            {
                if (HayCargaBloqueada())
                    MostrarAvisoCargaBloqueada();
                bool configRecovered = false;
                bool queueRecovered = false;
                bool libraryRecovered = false;

                if (Config is not null && Config.LoadTransientError)
                {
                    configRecovered = await Task.Run(() => Config.TryRecoverFromTransientLock());
                    if (Cerrando || IsDisposed)
                        return;
                    if (configRecovered)
                    {
                        // Final fix: the startup language was picked from the default
                        // config (Idioma="auto"). CargaXMLInterno(True) restores the
                        // persisted Idioma, but the static language packs and every
                        // translated label were still the degraded ones. Re-initialise
                        // and re-translate once, so the settings dialog and the UI agree.
                        try
                        {
                            Language.InitLanguage(Config.Idioma);
                            Translate();
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("Language refresh after config recovery failed: " + Log.SafeException(ex));
                        }
                        try
                        {
                            ApplyCurrentTheme();
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("Theme refresh after config recovery failed: " + Log.SafeException(ex));
                        }
                    }
                }

                if (!System.Threading.Volatile.Read(ref _startupQueueLoading) && Paquete.HasTransientLoadError)
                {
                    var real = await Task.Run(() => Paquete.TryReloadAfterTransientLock());
                    if (Cerrando || IsDisposed)
                    {
                        Paquete.DisposeLoadedQueue(real);
                        return;
                    }
                    if (real is not null)
                    {
                        lock (Mutex.ListaDescargas)
                        {
                            // 降级期间队列从空开始,当前列表里的都是用户新建的任务;
                            // 追加到真实队列之后,避免恢复动作把新任务静默丢掉。
                            if (ListaPaquetes is not null)
                                real.AddRange(ListaPaquetes);
                            ListaPaquetes = real;
                            // Final fix: keep write protection armed until the merged list
                            // is the live queue. Releasing it inside TryReload... let the
                            // 250ms disk worker persist the incomplete degraded queue and
                            // rotate the freshly recovered queue into .bak.
                            Paquete.CommitTransientRecovery();
                        }
                        RefreshListaDescargas(true);
                        ClearStartupQueueBanner();
                        ListaDescargas.EmptyListMsg = Language.GetText("OLV_EmptyList");
                        UpdateStartupActionAvailability();
                        queueRecovered = true;
                    }
                }

                if (StreamingLibraryManager.HasPendingRecovery())
                {
                    libraryRecovered = await Task.Run(() => StreamingLibraryManager.TryRecoverPending());
                    if (Cerrando || IsDisposed)
                        return;
                }

                if (configRecovered)
                {
                    ResumeAfterConfigurationRecovery();
                    if (queueRecovered && _startupReady)
                        ProcessDeferredStartupArguments();
                }
                else if (queueRecovered)
                {
                    if (_startupReady)
                    {
                        ProcessDeferredStartupArguments();
                    }
                    else
                    {
                        ContinueStartupAfterQueueLoad();
                    }
                }

                if (configRecovered || queueRecovered || libraryRecovered)
                {
                    Log.WriteInfo("On-disk application data was recovered.");
                }

                // Only celebrate once, when every degraded source is healthy again:
                // config/queue/library often unlock on different ticks, and showing a
                // toast per source produced a stack of identical notifications.
                // The banner is a natural one-shot latch (it is removed here).
                if (!HayCargaBloqueada() && _recoveryStatusLabel is not null)
                {
                    ToastForm.ShowToast(this, Language.GetText("Config_LoadRecovered"));
                    StatusStrip1.Items.Remove(_recoveryStatusLabel);
                    _recoveryStatusLabel.Dispose();
                    _recoveryStatusLabel = null;
                    Log.WriteInfo("Transient file lock recovery finished; writes are enabled again.");
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Transient load recovery tick failed: " + Log.SafeException(ex));
            }
            finally
            {
                _recoveryBusy = false;
            }
        }

        private void Main_FirstPaint(object sender, PaintEventArgs e)
        {
            if (_firstPaintLogged)
                return;
            _firstPaintLogged = true;
            LogStartupStage("first main window paint");
        }

        private void Translate()
        {
            Text = "MegaDownloader";
            OlvColumnVelocidad.Text = Language.GetText("Speed");
            OlvColumnEDT.Text = Language.GetText("Estimated");
            OlvColumnProgreso.Text = Language.GetText("Progress");
            OlvColumnProgresoPorc.Text = Language.GetText("Progress %");
            OlvColumnEstado.Text = Language.GetText("Status");
            OlvColumnTamano.Text = Language.GetText("Size");
            OlvColumnDescargado.Text = Language.GetText("Downloaded");
            OlvColumnNombre.Text = Language.GetText("Name");
            OlvColumnRestante.Text = Language.GetText("Remaining");
            ListaDescargas.EmptyListMsg = Language.GetText("OLV_EmptyList");
            detailGroup.Text = Language.GetText("Detail_Title");
            ApplyNavListColors();
            UpdateNavRailTexts();
            UpdateNavCounts();
            UpdateDetailPanel();
            AbrirEnCarpetaToolStripMenuItem.Text = Language.GetText("Open directory");
            SubirPrioridadMenuItem.Text = Language.GetText("Increase priority");
            BajarPrioridadMenuItem.Text = Language.GetText("Decrease priority");
            PausarStripMenuItem.Text = Language.GetText("Pause");
            ForceDownloadStripMenuItem.Text = Language.GetText("Force download");
            EliminarMenuItem.Text = Language.GetText("Delete from list");
            EliminarYBorrarMenuItem.Text = Language.GetText("Delete from list and disk");
            VerErrorToolStripMenuItem.Text = Language.GetText("See error");
            VerLinksToolStripMenuItem.Text = Language.GetText("See links");
            VerLinksDescToolStripMenuItem.Text = Language.GetText("See links + desc");
            OcultarEnlacesImagenMenuItem.Text = Language.GetText("Hide links inside an image");
            ResetToolStripMenuItem.Text = Language.GetText("Reset");
            VerProgresoDescompresionToolStripMenuItem.Text = Language.GetText("See decompression progress");
            PropiedadesToolStripMenuItem.Text = Language.GetText("Properties");
            AgregarLinksToolStripMenuItem.Text = Language.GetText("Add links");
            LimpiarCompletados2ToolStripMenuItem.Text = Language.GetText("Clean completed");
            LimpiarCompletadosToolStripMenuItem.Text = Language.GetText("Clean completed");
            AbrirToolStripMenuItem1.Text = Language.GetText("Open");
            AgregarLinkStripMenuItem.Text = Language.GetText("Add link");
            CerrarToolStripMenuItem.Text = Language.GetText("Close");
            ToolTipBotones.SetToolTip(btnConfig, Language.GetText("Configuration"));
            ToolTipBotones.SetToolTip(btnPlay, Language.GetText("Start downloads"));
            ToolTipBotones.SetToolTip(btnCollaborate, Language.GetText("Collaborate"));
            ToolTipBotones.SetToolTip(btnPause, Language.GetText("Pause downloads"));
            ToolTipBotones.SetToolTip(btnStop, Language.GetText("Stop downloads"));
            ToolTipBotones.SetToolTip(btnAddLink, Language.GetText("Add links"));
            ToolTipBotones.SetToolTip(btnUpdate, Language.GetText("New version do you want to download it?"));
            // P0-2 UI:工具栏文字化,短键缺失时回退显示英文原文(Language.GetText 三级回退)
            btnPlay.Text = Language.GetText("Start");
            btnPause.Text = Language.GetText("Pause");
            btnStop.Text = Language.GetText("Stop");
            btnAddLink.Text = Language.GetText("Add");
            btnUpdate.Text = Language.GetText("Update");
            StatusToolStripStatusLabel.Text = Language.GetText("Status: -");
            RAMProcToolStripStatusLabel.Text = Language.GetText("RAM Proc Empty");
            UpdateBatchMenuTexts();
            UpdateQuotaBannerTexts();
            ApplyQuotaBannerTheme();
            UpdateWorkbenchTexts();
        }



        /// <summary>
    /// P2-7:顶部菜单条由经典 MainMenu 换成 MenuStrip。
    /// 经典 MainMenu 的条带由操作系统按系统菜单色绘制(实测:即便把每个 MenuItem 的
    /// OwnerDraw 置 True,条带仍是 F0F0F0 的系统亮色),它既不是 ToolStrip、也不在
    /// Controls 树里,ThemeManager 的四类覆盖(ToolStrip 渲染器 / Controls 递归 /
    /// ContextMenuStrip / ToolTip)全都够不着它 → 深色主题下顶部是一条明显亮带。
    /// MenuStrip 走 ToolStripManager.Renderer + ThemeColorTable,随主题自动换肤。
    /// </summary>
        private void CrearMenus()
        {
            if (MainMenuStrip is not null)
            {
                var previous = MainMenuStrip;
                MainMenuStrip = null;
                Controls.Remove(previous);
                previous.Dispose();
            }
            var menuPrincipal = new MenuStrip() { AutoSize = false, CanOverflow = false, Font = Font };
            MainMenuStrip = menuPrincipal;
            menuPrincipal.Name = "MenuPrincipal";
            menuPrincipal.Dock = DockStyle.Top;

            var Archivo = new ToolStripMenuItem(Language.GetText("&File"));

            var OpenELC = new ToolStripMenuItem(Language.GetText("Open &ELC"));
            OpenELC.Click += OpenDLC_Click;

            var Salir = new ToolStripMenuItem(Language.GetText("E&xit"));
            Salir.Click += CerrarToolStripMenuItem_Click;

            Archivo.DropDownItems.Add(OpenELC);
            Archivo.DropDownItems.Add(new ToolStripSeparator());
            Archivo.DropDownItems.Add(Salir);
            menuPrincipal.Items.Add(Archivo);

            // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

            var ColaExtraccion = new ToolStripMenuItem(Language.GetText("See extraction &queue"));
            ColaExtraccion.Click += VerDescompresor_Click;

            var Configuracion = new ToolStripMenuItem(Language.GetText("&Configuration"));
            Configuracion.Click += btnConfig_Click;

            var VerLogs = new ToolStripMenuItem(Language.GetText("See lo&gs"));
            VerLogs.Click += VerLogs_Click;

            var CodificarEnlaces = new ToolStripMenuItem(Language.GetText("Encode lin&ks"));
            CodificarEnlaces.Click += CodificarEnlaces_Click;

            var GenerateELC = new ToolStripMenuItem(Language.GetText("Generat&e ELC"));
            GenerateELC.Click += GenerateELC_Click;

            var Stegano = new ToolStripMenuItem(Language.GetText("Steganograph&y"));

            var Opciones = new ToolStripMenuItem(Language.GetText("&Options"));

            Opciones.DropDownItems.Add(CodificarEnlaces);
            Opciones.DropDownItems.Add(GenerateELC);
            Opciones.DropDownItems.Add(Stegano);
            Opciones.DropDownItems.Add(new ToolStripSeparator());
            Opciones.DropDownItems.Add(ColaExtraccion);
            Opciones.DropDownItems.Add(VerLogs);
            scheduleMenuItem = new ToolStripMenuItem(Language.GetText("Schedule_Title"));
            scheduleMenuItem.Click += OpenDownloadSchedule;
            Opciones.DropDownItems.Add(scheduleMenuItem);
            Opciones.DropDownItems.Add(new ToolStripSeparator());
            Opciones.DropDownItems.Add(Configuracion);
            menuPrincipal.Items.Add(Opciones);

            // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

            var CreateStegano = new ToolStripMenuItem(Language.GetText("&Hide links inside an image"));
            CreateStegano.Click += CreateStegano_Click;
            var UseStegano = new ToolStripMenuItem(Language.GetText("&Retrieve links from an image"));
            UseStegano.Click += UseStegano_Click;

            Stegano.DropDownItems.Add(CreateStegano);
            Stegano.DropDownItems.Add(UseStegano);

            // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

            var VerStreaming = new ToolStripMenuItem(Language.GetText("Watch &Online"));
            VerStreaming.Click += VerStreaming_Click;

            var LibraryManager = new ToolStripMenuItem(Language.GetText("Manage Streaming &Library"));
            LibraryManager.Click += LibraryManager_Click;

            var SeeLibraryManager = new ToolStripMenuItem(Language.GetText("See Streaming &Library"));
            SeeLibraryManager.Click += SeeLibraryManager_Click;

            var Streaming = new ToolStripMenuItem(Language.GetText("&Streaming"));
            Streaming.DropDownItems.Add(VerStreaming);
            Streaming.DropDownItems.Add(new ToolStripSeparator());
            Streaming.DropDownItems.Add(SeeLibraryManager);
            Streaming.DropDownItems.Add(LibraryManager);
            menuPrincipal.Items.Add(Streaming);


            // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

            var FAQ = new ToolStripMenuItem(Language.GetText("FA&Q"));
            FAQ.Click += FAQ_Click;

            var About = new ToolStripMenuItem(Language.GetText("&About"));
            About.Click += About_Click;

            var CheckUpdates = new ToolStripMenuItem(Language.GetText("Chec&k for updates"));
            CheckUpdates.Click += CheckUpdates_Click;

            var Ayuda = new ToolStripMenuItem(Language.GetText("&Help"));
            Ayuda.DropDownItems.Add(CheckUpdates);
            Ayuda.DropDownItems.Add(new ToolStripSeparator());
            Ayuda.DropDownItems.Add(FAQ);
            Ayuda.DropDownItems.Add(new ToolStripSeparator());
            Ayuda.DropDownItems.Add(About);
            menuPrincipal.Items.Add(Ayuda);

            // 挂到窗体上:两条都不用 Dock,改为显式坐标。
            // 此前菜单条与工具栏都靠 Dock=Top,上下关系由"Controls 索引越大越先停靠"这条
            // 未文档化的规则决定 —— 顺序一反,菜单条就停到工具栏下面,压住下载区顶部
            // (用户实测到的"顶栏挡住下面主窗口 UI")。现在几何一次写死,与停靠顺序无关。
            menuPrincipal.Dock = DockStyle.None;
            Controls.Add(menuPrincipal);
            menuPrincipal.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            LayoutMainMenu();

            // 工具栏紧接菜单条之下;宽度靠 Anchor 跟随,高度沿用设计器里的 40。
            // 下载区 LayoutDownloadArea() 用 TableLayoutPanel1.Bottom 起算,无需手工偏移。
            if (!workbenchReady && TableLayoutPanel1 is not null)
            {
                TableLayoutPanel1.Dock = (System.Windows.Forms.DockStyle)Conversions.ToInteger(DockStyle.None);
                TableLayoutPanel1.Location = new Point(0, menuPrincipal.Height);
                TableLayoutPanel1.Width = ClientSize.Width;
                TableLayoutPanel1.Anchor = (System.Windows.Forms.AnchorStyles)Conversions.ToInteger(AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            }

            PerformLayout();
            if (workbenchReady)
                LayoutDownloadArea();
        }

        private void LayoutMainMenu()
        {
            var menu = MainMenuStrip;
            if (menu is null || menu.IsDisposed)
                return;
            float scale = GetUiScale();
            int height = Math.Max((int)Math.Round(30f * scale), Font.Height + (int)Math.Round(8f * scale));
            menu.AutoSize = false;
            menu.Padding = new Padding((int)Math.Round(8f * scale), (int)Math.Round(2f * scale), (int)Math.Round(8f * scale), (int)Math.Round(2f * scale));
            menu.SetBounds(0, 0, ClientSize.Width, height);
            menu.PerformLayout();
        }

        private bool IsSilent()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args is not null)
            {
                foreach (string arg in args)
                {
                    if (arg == "-silent")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void CheckVersionStatistics()
        {
            // P3-14: 此前用 Double.TryParse 解析 "2.5.7" —— 带两个小数点的版本号必然解析失败,
            // VersionActual 保持 0;而 Config.VersionConfig 缺省也是 "0",
            // 于是 0 > 0 恒假,PingNewVersion() 永远到不了(版本统计上报形同虚设)。
            // 2.5.0 时代 VERSION_UPDATE 写的是 "2.5"(恰好能被 Double 解析),2.5.7 改成
            // "2.5.7" 之后这条链就断了;更新检查本身用 System.Version,不受影响。
            // 改为与 Updater 同一套 System.Version 比较:先规整成四段再逐段比。
            // 旧配置里存的 "0" 视为"从未记录过",任何真实版本号都应触发一次上报。
            Version versionActual = null;
            if (!Version.TryParse(Updater.NormalizeVersionString(InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_UPDATE")), out versionActual))

            {
                Log.WriteWarning("Could not parse VERSION_UPDATE; version statistics ping skipped.");
                return;
            }

            Version ultimaVersion = null;
            if (!Version.TryParse(Updater.NormalizeVersionString(Config.VersionConfig), out ultimaVersion))
            {
                ultimaVersion = new Version(0, 0, 0, 0);
            }

            if (versionActual > ultimaVersion)
            {
                Config.VersionConfig = versionActual.ToString();

                // Hitcount new user of this version
                Conexion.PingNewVersion();
            }
        }

        private bool CheckMEGAConditions()
        {
            // Final fix: on a transient lock the on-disk terms state is simply unknown.
            // Asking again (and forcing a close on "No") would replace a perfectly good
            // config with defaults; the recovery timer reloads the persisted value.
            if (!Config.CondicionesAceptadas && !Config.LoadTransientError)
            {
                if (MessageBox.Show(Language.GetText("Accept terms of use"), Language.GetText("Terms of use"), MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button3, 0, InternalConfiguration.ObtenerValueFromInternalConfig("MEGA_TERMS"), "") != System.Windows.Forms.DialogResult.Yes)
                {
                    _ForzarCierre = true;
                    Close();

                    return false;
                }
                else
                {
                    // Hitcount new user app
                    Conexion.PingNewUser();
                }
            }
            Config.CondicionesAceptadas = true;
            return true;
        }

        #endregion

        #region Cierre del formulario
        private bool Cerrando = false;
        private void Main_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
        {
            // SkinEngine.Dispose()
            // 第19项:置位必须在 DoEvents 之前,否则泵出的托盘点击会复活窗口
            Cerrando = true;
            CancelStartupQueueLoad();
            Application.DoEvents();

            var Crono = DateTime.Now;

            Log.WriteInfo("Closing, cancelling workers");

            // 1) Stop accepting new work via Web/Streaming
            Log.WriteInfo("Stopping web server");
            WebServerHost.Stop();

            // 2) Cancel background workers and extraction
            if (_startupReady)
            {
                _bgwActualizadorListaDescargas.CancelAsync();
                _bgwComprobarMaxConexiones.CancelAsync();
                _bgwActualizadorDatosDisco.CancelAsync();
                _bgwDescompresor.CancelAsync();
            }
            try
            {
                DescompresorController.GetController().RequestCancel();
            }
            catch (Exception ex)
            {
                Log.WriteError("Shutdown: DescompresorController.RequestCancel failed: " + ex.ToString());
            }

            // 3) Stop downloads and wait
            Log.WriteInfo("Cancelling downloads");
            PararDescargaFicheros();
            if (_startupReady)
                EsperarParadaDescargasYWorkers();

            // 4) Atomic-ish persistence before releasing UI lists
            Log.WriteInfo("Saving download list");
            if (_startupReady)
                GuardarFicheroDescargas();

            if (WindowState != FormWindowState.Minimized)
            {
                Config.ConfigUI.AnchoVentanaPrincipal = Width;
                Config.ConfigUI.AltoVentanaPrincipal = Height;
            }
            if (_startupReady)
            {
                Config.ConfigUI.EstadoLista = ListaDescargas.SaveState();
                Config.GuardarXML(false);
            }

            // 5) Release list/UI resources
            ListaDescargas.ClearObjects();
            ListaDescargas.Dispose();

            // Esto es solo para mostrar la imagen y que no parpadee xD
            if (DateTime.Now.Subtract(Crono).TotalMilliseconds < 500d)
            {
                System.Threading.Thread.Sleep(500);
            }

            Log.WriteError("Closing MegaDownloader, bye bye!" + Constants.vbNewLine);

            Log.Flush(true);

            // Liberamos resto de recursos
            IconoMinimizado.Dispose();
            // 先 Uninstall 取消剪贴板监听,再 DestroyHandle 释放窗口句柄。
            // 原顺序反了会导致监听器在句柄已销毁后仍触发一次回调引发异常。
            clipChange.Uninstall();
            clipChange.DestroyHandle();

            System.Windows.Forms.Application.Exit();

        }

        private bool _ForzarCierre = false;
        private void Main_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {

            string msjCerrar = Language.GetText("Do you want to exit?");
            var comp = DescompresorController.GetController();
            if (comp.Ocupado())
            {
                msjCerrar = Language.GetText("Files extracting, corruption danger") + Constants.vbNewLine + msjCerrar;
            }

            if (e.CloseReason == CloseReason.UserClosing & !_ForzarCierre && MessageBox.Show(msjCerrar, Language.GetText("Close"), MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                // 第19项:进入关闭流程即置 Cerrando(此前要到 FormClosed 才置)。
                // 关闭等待循环里的 DoEvents 会派发托盘点击/Resize,把窗口重新显示出来;
                // 先置位后,RestaurarVentana/Resize 的守卫就能拦住"关机期复活"。
                Cerrando = true;
                CancelStartupQueueLoad();
                Visible = false;
                var ventanaCerrando = new Cerrando();
                ventanaCerrando.lblMensaje.Text = Language.GetText("Closing please wait");
                ventanaCerrando.Show();
            }
        }
        private void CerrarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void VerDescompresor_Click(object sender, EventArgs e)
        {
            if (IsFormAlreadyOpen(typeof(Descompresor)) is null)
            {
                var frmName = new Descompresor();
                frmName.Show();
            }
        }


        private void OpenDLC_Click(object sender, EventArgs e)
        {

            if (DLCProcessing)
            {
                MessageBox.Show(Language.GetText("There is a DLC being processed, please wait"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var Examinar = new OpenFileDialog();
            Examinar.CheckFileExists = true;
            Examinar.Filter = Language.GetText("ELC") + " (*.elc)|*.elc|" + Language.GetText("DLC") + " (discontinued) (*.dlc)|*.dlc";
            Examinar.Multiselect = false;

            string DLCPath = string.Empty;
            if (Examinar.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DLCPath = Examinar.FileName;
            }
            Examinar.Dispose();

            AddDLC(DLCPath);

        }

        #endregion

        #region Pintado del formulario



        private void Main_Resize(object sender, EventArgs e)
        {

            // Minimizamos
            if (WindowState == FormWindowState.Minimized)
            {
                IconoMinimizado.Visible = true;
                Visible = false;
                IconoMinimizado.ShowBalloonTip(5000, Text, IconoMinimizado.Text, default);
            }
            // 第19项:关机期 DoEvents 泵出的 Resize 不得把已隐藏的窗口重新显示
            else if (!Cerrando)
            {
                IconoMinimizado.Visible = false;
                // Resizing is also raised during construction and DPI layout.
                // Startup and tray restoration own visibility explicitly; showing
                // here can re-enter Load and rebuild the entire workbench.
            }
            LayoutQuotaBanner();
            LayoutFailureBanner();
            LayoutDownloadArea();
        }

        private void RestaurarVentana()
        {
            // 第19项:关机期 DoEvents 会把托盘点击派发进来,复活已隐藏的窗口。
            // 守卫范式同 SetStatusBar/后台 worker(:3228 一带)。
            if (Cerrando || _ForzarCierre || IsDisposed || Disposing)
                return;
            if (WindowState == FormWindowState.Minimized)
            {
                Visible = true;
                IconoMinimizado.Visible = false;
                WindowState = (System.Windows.Forms.FormWindowState)Conversions.ToInteger(FormWindowState.Normal);
            }
            if (_startupNeedsAttention)
                CompleteStartup();
        }

        internal void ActivateExistingInstance()
        {
            if (_startupNeedsAttention)
                RestaurarVentana();
            if (!IsDisposed)
                Activate();
        }


        private void AbrirToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            RestaurarVentana();
        }


        private void IconoMinimizado_DoubleClick(object sender, EventArgs e)
        {
            RestaurarVentana();
        }
        private void IconoMinimizado_Click(object sender, EventArgs e)
        {
            if (e is System.Windows.Forms.MouseEventArgs)
            {
                if (((System.Windows.Forms.MouseEventArgs)e).Button == System.Windows.Forms.MouseButtons.Left)
                {
                    RestaurarVentana();
                }
            }
        }


        #endregion

        #region Columnas del ListView

        /// <summary>
    /// Define el comportamiento de las columnas del listview
    /// </summary>
    /// <remarks></remarks>
        private void DefinirColumnas()
        {
            ListaDescargas.TreeColumnRenderer = new QueueTreeRenderer();
            int IndiceColumnaPrioridad = 1;
            int IndiceColumnaNombre = 0;
            int IndiceColumnaDescargado = 2;
            int IndiceColumnaTamano = 3;
            int IndiceColumnaEstado = 4;
            int IndiceColumnaPorcentajeTexto = 5;
            int IndiceColumnaPorcentaje = 6;
            int IndiceColumnaVelocidad = 7;
            int IndiceColumnaEDT = 8;
            int IndiceColumnaRestante = 9;



            ListaDescargas.PrimarySortColumn = ListaDescargas.AllColumns[IndiceColumnaPrioridad];

            ListaDescargas.SortGroupItemsByPrimaryColumn = true;


            var ColPrioridad = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaPrioridad]);
            ColPrioridad.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) => ele.DescargaPrioridad());

            var ColNombre = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaNombre]);
            ColNombre.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    if (ele is Fichero)
                    {
                        Fichero ficN = (Fichero)ele;
                        string relN = ficN.RutaRelativa ?? string.Empty;
                        if (!ficN.DescargaProcesada)
                        {
                            // 验名前 NombreFichero 是整串 URL,先显示短 FileID,验完自动刷出真名
                            string fidN = "";
                            try
                            {
                                fidN = ficN.FileID;
                            }
                            catch
                            {
                                fidN = "";
                            }
                            if (!string.IsNullOrEmpty(fidN))
                                return System.IO.Path.Combine(relN, fidN);
                        }
                        string nmN = "";
                        try
                        {
                            nmN = ele.DescargaNombre();
                        }
                        catch
                        {
                            nmN = "";
                        }
                        if (string.IsNullOrEmpty(nmN))
                            return relN;
                        return System.IO.Path.Combine(relN, nmN);
                    }
                    else
                    {
                        return ele.DescargaNombre();
                    }

                });

            var ColDescargado = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaDescargado]);
            ColDescargado.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    decimal tamano = ele.DescargaTamanoBytes();
                    if (tamano == 0m)
                        return "-";
                    decimal descargado = Math.Ceiling(ele.DescargaPorcentaje() * tamano / 100m);
                    return PintarTamano(descargado);
                });
            ListaDescargas.AllColumns[IndiceColumnaDescargado].TextAlign = (System.Windows.Forms.HorizontalAlignment)Conversions.ToInteger(HorizontalAlignment.Right);


            var ColRestante = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaRestante]);
            ColRestante.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    decimal tamano = ele.DescargaTamanoBytes();
                    if (tamano == 0m)
                        return "-";
                    decimal descargado = Math.Ceiling(ele.DescargaPorcentaje() * tamano / 100m);
                    return PintarTamano(tamano - descargado);
                });
            ListaDescargas.AllColumns[IndiceColumnaRestante].TextAlign = (System.Windows.Forms.HorizontalAlignment)Conversions.ToInteger(HorizontalAlignment.Right);


            var ColTamano = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaTamano]);
            ColTamano.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    decimal tamano = ele.DescargaTamanoBytes();
                    if (tamano == 0m)
                        return "-";
                    return PintarTamano(tamano);

                });
            ListaDescargas.AllColumns[IndiceColumnaTamano].TextAlign = (System.Windows.Forms.HorizontalAlignment)Conversions.ToInteger(HorizontalAlignment.Right);

            var ColEstado = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaEstado]);
            ColEstado.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    try
                    {
                        var Estado = ele.DescargaEstado();
                        switch (Estado)
                        {
                            case MegaDownloader.Estado.EnCola:
                                return Language.GetText("In queue");
                            case MegaDownloader.Estado.CreandoLocal:
                                return Language.GetText("Creating files");
                            case MegaDownloader.Estado.Verificando:
                                return Language.GetText("Verifying");
                            case MegaDownloader.Estado.Erroneo:
                                {
                                    string errBase = Language.GetText("Error capital leters");
                                    string errReason = ShortErrorReason(ele);
                                    if (string.IsNullOrEmpty(errReason))
                                        return errBase;
                                    return errBase + ": " + errReason;
                                }
                            case MegaDownloader.Estado.Pausado:
                                return Language.GetText("Paused");
                            case MegaDownloader.Estado.Descomprimiendo:
                                return Language.GetText("Extracting");
                            case MegaDownloader.Estado.Descargando:
                                return Language.GetText("Downloading");
                            case MegaDownloader.Estado.ComprobandoMD5:
                                return Language.GetText("Hashing MD5");
                            case MegaDownloader.Estado.Completado:
                                return Language.GetText("Completed");
                            default:
                                return "---";
                        }
                    }
                    catch (Exception ex)
                    {
                        // AspectGetter 在列表每次重绘时高频执行:只记日志并返回安全占位值。
                        // 此处弹窗/重新抛出会让每一行重绘都弹一次错误框并最终闪退。
                        Log.WriteError("Error displaying download status: " + ex.ToString());
                        return "---";
                    }
                });

            var ColPorcentaje = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaPorcentaje]);
            ColPorcentaje.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    try
                    {
                        return ele.DescargaPorcentaje();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error displaying download %: " + ex.ToString());
                        return 0;
                    }
                });
            progressBarRenderer = new ThemeBarRenderer();
            progressBarRenderer.UseStandardBar = false;
            progressBarRenderer.MaximumWidth = 9999;
            progressBarRenderer.MinimumValue = 0d;
            progressBarRenderer.MaximumValue = 100d;
            ApplyProgressBarThemeColors();
            ListaDescargas.AllColumns[IndiceColumnaPorcentaje].Renderer = progressBarRenderer;

            var ColVelocidad = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaVelocidad]);
            ColVelocidad.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) => PintarVelocidadDescarga(ele));
            ListaDescargas.AllColumns[IndiceColumnaVelocidad].TextAlign = (System.Windows.Forms.HorizontalAlignment)Conversions.ToInteger(HorizontalAlignment.Right);


            var ColEDT = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaEDT]);
            ColEDT.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    try
                    {
                        return ele.DescargaTiempoEstimadoDescarga();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error displaying download time: " + ex.ToString());
                        return "---";
                    }
                });
            ListaDescargas.AllColumns[IndiceColumnaEDT].TextAlign = (System.Windows.Forms.HorizontalAlignment)Conversions.ToInteger(HorizontalAlignment.Right);

            var ColPorcentajeTexto = new TypedColumn<IDescarga>(ListaDescargas.AllColumns[IndiceColumnaPorcentajeTexto]);
            ColPorcentajeTexto.AspectGetter = new TypedColumn<IDescarga>.TypedAspectGetterDelegate((ele) =>
                {
                    try
                    {
                        return ele.DescargaPorcentaje().ToString("F2") + "%";
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("Error displaying download % (text): " + ex.ToString());
                        return "";
                    }
                });
            ListaDescargas.AllColumns[IndiceColumnaPorcentajeTexto].TextAlign = (System.Windows.Forms.HorizontalAlignment)Conversions.ToInteger(HorizontalAlignment.Right);
            // The separate numeric column remains optional; the default progress
            // renderer already includes a percentage beside its bar.



            ListaDescargas.AllowColumnReorder = true;

            ListaDescargas.CanExpandGetter = new TreeListView.CanExpandGetterDelegate((ele) => ele is Paquete && ((Paquete)ele).ListaFicheros is not null && ((Paquete)ele).ListaFicheros.Count > 0);


            ListaDescargas.ChildrenGetter = new TreeListView.ChildrenGetterDelegate((ele) =>
                {
                    // P2-11b:非 All 分组下只展开命中文件(包穿透规则与计数共用同一谓词)。
                    // 注意:不可用 OLV UseFiltering(启动期静默退出),此处包裹是唯一的过滤点。
                    try
                    {
                        if (!(ele is Paquete))
                        {
                            return null;
                        }
                        var files = ((Paquete)ele).ListaFicheros;
                        if (files is null)
                            return null;
                        if (_navScope == DownloadEstadoFilter.NavScope.All && queueQuery.Length == 0)
                            return files;
                        var shown = new List<Fichero>();
                        foreach (Fichero f in files)
                        {
                            if (MatchesQueueFile(f, (Paquete)ele))
                                shown.Add(f);
                        }
                        return shown;
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("FilteredChildrenGetter failed: " + ex.ToString());
                        Paquete p0 = ele as Paquete;
                        if (p0 is null)
                            return null;
                        return p0.ListaFicheros;
                    }
                });


            ListaDescargas.SelectColumnsOnRightClickBehaviour = ObjectListView.ColumnSelectBehaviour.InlineMenu;

            ListaDescargas.FullRowSelect = true;

            ListaDescargas.DragSource = new SimpleDragSource();
            dropSink = new SimpleDropSink();
            ListaDescargas.DropSink = dropSink;
            dropSink.CanDropBetween = true;
            dropSink.CanDropOnBackground = false;
            dropSink.CanDropOnItem = false;
            dropSink.CanDropOnSubItem = false;


            // 列宽上下限:WinForms 表头分栏线无最大宽度,OLV Minimum/MaximumWidth 只在代码赋值时生效,
            // 拖动时靠 ColumnWidthChanging 事件手动夹取(见下方 handler)。
            ApplyColumnWidthLimits();

            // 行高随当前 DPI / 字体留出间距；空态复用语言包。
            ListaDescargas.RowHeight = Math.Max((int)Math.Round(40f * GetUiScale()), Font.Height + 16);
            ListaDescargas.EmptyListMsg = Language.GetText("OLV_EmptyList");
            // P2-11b:导航初始化(计数+分组;过滤走 Roots 子集,禁用 OLV UseFiltering/ModelFilter)
            InitNavList();
        }
        private void ListaDescargas_FormatRow(object sender, FormatRowEventArgs e)
        {
            if (e.DisplayIndex % 2 == 0)
            {
                e.Item.BackColor = ThemeManager.GetColor("Back");
            }
            else
            {
                e.Item.BackColor = ThemeManager.GetColor("AltBack");
            }
            e.Item.ForeColor = ThemeManager.GetColor("Fore");
            e.UseCellFormatEvents = true;
        }

        private void ListaDescargas_FormatCell(object sender, FormatCellEventArgs e)
        {
            if (e.ColumnIndex != OlvColumnEstado.Index)
                return;
            IDescarga desc = e.Model as IDescarga;
            if (desc is null)
                return;
            if (desc.DescargaEstado() == Estado.Erroneo)
            {
                e.SubItem.ForeColor = ThemeManager.GetColor("ErrorFore");
            }
            else if (desc.DescargaEstado() == Estado.Completado)
            {
                e.SubItem.ForeColor = ThemeManager.GetColor("SuccessFore");
            }
            else if (desc.DescargaEstado() == Estado.Descargando)
            {
                e.SubItem.ForeColor = ThemeManager.GetColor("Link");
            }
            else
            {
                e.SubItem.ForeColor = ThemeManager.GetColor("Muted");
            }
        }

        /// <summary>
    /// 应用当前配置主题到主窗体,并同步列表进度条等非递归控件颜色。
    /// </summary>
        public void ApplyCurrentTheme()
        {
            if (Config is null || Config.ConfigUI is null)
            {
                ThemeManager.ApplyTheme(this);
            }
            else
            {
                ThemeManager.ApplyTheme(this, Config.ConfigUI.Tema);
            }
            ApplyProgressBarThemeColors();
            ApplyQuotaBannerTheme();
            ApplyToolbarIconTheme();
            ApplyNavListColors();
            ApplyNavRailTheme();
            ApplyDetailContentTheme();
            if (workbenchReady)
            {
                UiStyles.Apply(this);
                UiStyles.Apply(btnAddLink);
                RAMProcToolStripStatusLabel.ForeColor = ThemeManager.GetColor("Muted");
                navListBox.Invalidate();
            }
            if (ListaDescargas is not null)
            {
                ListaDescargas.Invalidate();
            }
        }

        private void ApplyProgressBarThemeColors()
        {
            if (progressBarRenderer is null)
                return;
            // RC:背景透明露出行底色(解决浅色整条发灰,0%行纯灰);边框随主题不再纯黑;
            // 清空渐变走 FillColor 实色(解决 FillColor 死赋值);条高跟随行高。
            progressBarRenderer.BackgroundColor = System.Drawing.Color.Transparent;
            progressBarRenderer.FillColor = ThemeManager.GetColor("ProgressFill");
            progressBarRenderer.GradientStartColor = System.Drawing.Color.Empty;
            progressBarRenderer.GradientEndColor = System.Drawing.Color.Empty;
            progressBarRenderer.FrameColor = ThemeManager.GetColor("Border");
            progressBarRenderer.FrameWidth = 1.0f;
            progressBarRenderer.MaximumHeight = 5;
        }

        // Line icons are drawn once for each theme / DPI change.
        private void ApplyToolbarIconTheme()
        {
            if (workbenchReady)
                ApplyWorkbenchIcons();
        }

        #region v2.5 beta: 配额横幅 + 批量失败操作

        private Panel quotaBannerPanel = default;
        private Label quotaBannerLabel = default;
        private Button quotaRetryNowButton = default;
        private bool quotaNotified = false;
        private ToolStripMenuItem RetryAllFailedMenuItem;
        private ToolStripMenuItem RemoveAllFailedMenuItem;
        private ToolStripMenuItem ResetColumnsMenuItem;
        private readonly ToolStripSeparator sepBatchMenuItem = new ToolStripSeparator();
        private readonly ToolStripSeparator sepColumnsMenuItem = new ToolStripSeparator();

        #endregion

        #region v2.5: 左侧栏任务总览 + 快捷入口

        // 左栏纵向扩展后的分段控件。navListBox 仍是过滤器本体(0..4),下方依次是
        // 任务总览(只读)与快捷入口(复用既有菜单命令)。
        private Label navOverviewHeader = default;
        private Label navOverviewSpeedValue = default;
        private Label navOverviewCountsValue = default;
        private ProgressBar navOverviewProgressBar = default;
        private Label navOverviewProgressText = default;
        private Label navOverviewRemainingValue = default;
        private Label navQuickHeader = default;
        private Button _navQuickExtractButton;

        private Button navQuickExtractButton
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _navQuickExtractButton;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_navQuickExtractButton != null)
                {
                    _navQuickExtractButton.Click -= navQuickExtractButton_Click;
                }

                _navQuickExtractButton = value;
                if (_navQuickExtractButton != null)
                {
                    _navQuickExtractButton.Click += navQuickExtractButton_Click;
                }
            }
        }

        private Button _navQuickLibraryButton;

        private Button navQuickLibraryButton
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _navQuickLibraryButton;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_navQuickLibraryButton != null)
                {
                    _navQuickLibraryButton.Click -= navQuickLibraryButton_Click;
                }

                _navQuickLibraryButton = value;
                if (_navQuickLibraryButton != null)
                {
                    _navQuickLibraryButton.Click += navQuickLibraryButton_Click;
                }
            }
        }

        private Button _navQuickLogsButton;

        private Button navQuickLogsButton
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _navQuickLogsButton;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_navQuickLogsButton != null)
                {
                    _navQuickLogsButton.Click -= navQuickLogsButton_Click;
                }

                _navQuickLogsButton = value;
                if (_navQuickLogsButton != null)
                {
                    _navQuickLogsButton.Click += navQuickLogsButton_Click;
                }
            }
        }
        private bool navRailInitialized = false;

        // 左栏宽度:130 太窄放不下"总速度 12.4 MB/s"这类文案,加宽到 168。
        // 中部列表 FillsFreeSpace 列会自动吸收差额,1024 宽下仍有约 640px。
        // 注意:做成字段而非常量——过滤器文本超宽(大计数/长译文)时 FitNavListBoxNoScroll
        // 会按实测加宽到 220,免横滚;Const 写死就加不动了。
        private int NavRailWidth = 168;

        #endregion

        #region v2.5: 左栏任务总览 + 快捷入口 —— 实现

        /// <summary>
    /// 左栏下半部分此前 70% 留白(5 项过滤器约 130px,面板 429px)。此处补两段:
    /// B 任务总览——速度/计数/队列进度/剩余时间,数据全部复用既有字段与 430ms 刷新循环,
    /// 不新增计时器;C 快捷入口——把埋在二级菜单里的解压队列/流媒体库/日志提到首屏。
    /// 所有控件挂到 navPanel 内并置于 navListBox 之下,由 LayoutNavRail 统一定位,
    /// 主题走 ThemeManager 递归(navListBox 除外,仍需 ApplyNavListColors 手动同步)。
    /// </summary>
        private void InitNavRail()
        {
            try
            {
                if (navPanel is null)
                    return;

                float sc = GetUiScale();
                int pad = (int)Math.Round(6f * sc);
                int innerW = Math.Max(40, navPanel.ClientSize.Width - pad * 2);
                int rowH = (int)Math.Round(20f * sc);
                // ---- B 段:任务总览 ----
                navOverviewHeader = CreateNavSectionHeader(Language.GetText("Overview_Title"), sc);

                var speedLabel = new Label();
                speedLabel.Name = "navOverviewSpeedLabel";
                speedLabel.AutoSize = false;
                speedLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                speedLabel.Text = Language.GetText("Overview_Speed");
                speedLabel.Height = rowH;

                navOverviewSpeedValue = new Label();
                navOverviewSpeedValue.Name = "navOverviewSpeedValue";
                navOverviewSpeedValue.AutoSize = false;
                navOverviewSpeedValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                UiStyles.SetFont(navOverviewSpeedValue, 7, false, Font);
                navOverviewSpeedValue.Height = rowH;
                navOverviewSpeedValue.Text = "-";

                navOverviewCountsValue = new Label();
                navOverviewCountsValue.Name = "navOverviewCountsValue";
                navOverviewCountsValue.AutoSize = false;
                navOverviewCountsValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                navOverviewCountsValue.Height = (int)Math.Round(34f * sc);
                navOverviewCountsValue.Text = "-";

                var progressCaption = new Label();
                progressCaption.Name = "navOverviewProgressCaption";
                progressCaption.AutoSize = false;
                progressCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                progressCaption.Text = Language.GetText("Overview_Progress");
                progressCaption.Height = rowH;

                navOverviewProgressText = new Label();
                navOverviewProgressText.Name = "navOverviewProgressText";
                navOverviewProgressText.AutoSize = false;
                navOverviewProgressText.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
                navOverviewProgressText.Height = rowH;
                navOverviewProgressText.Text = "0%";

                navOverviewProgressBar = new SlimProgressBar();
                navOverviewProgressBar.Name = "navOverviewProgressBar";
                navOverviewProgressBar.Minimum = 0;
                navOverviewProgressBar.Maximum = 100;
                navOverviewProgressBar.Value = 0;
                navOverviewProgressBar.Height = (int)Math.Round(10f * sc);

                var remainingCaption = new Label();
                remainingCaption.Name = "navOverviewRemainingCaption";
                remainingCaption.AutoSize = false;
                remainingCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                remainingCaption.Text = Language.GetText("Overview_Remaining");
                remainingCaption.Height = rowH;

                navOverviewRemainingValue = new Label();
                navOverviewRemainingValue.Name = "navOverviewRemainingValue";
                navOverviewRemainingValue.AutoSize = false;
                navOverviewRemainingValue.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
                navOverviewRemainingValue.Height = rowH;
                navOverviewRemainingValue.Text = "-";

                // 控件登记到 navPanel,Text 属性留待 UpdateNavRailTexts 统一本地化(切换语言时重建)。
                navPanel.Controls.Add(navOverviewHeader);
                navPanel.Controls.Add(speedLabel);
                navPanel.Controls.Add(navOverviewSpeedValue);
                navPanel.Controls.Add(navOverviewCountsValue);
                navPanel.Controls.Add(progressCaption);
                navPanel.Controls.Add(navOverviewProgressText);
                navPanel.Controls.Add(navOverviewProgressBar);
                navPanel.Controls.Add(remainingCaption);
                navPanel.Controls.Add(navOverviewRemainingValue);

                // 说明性 Label 用 Tag 登记,供 FindNavLabel 在换语言/换肤时按语义定位(它们不是交互控件)。
                speedLabel.Tag = "overviewSpeedLabel";
                progressCaption.Tag = "overviewProgressCaption";
                remainingCaption.Tag = "overviewRemainingCaption";

                // ---- C 段:快捷入口 ----
                navQuickHeader = CreateNavSectionHeader(Language.GetText("Quick_Title"), sc);

                navQuickExtractButton = CreateNavQuickButton("navQuickExtractButton", Language.GetText("Quick_ExtractQueue"), innerW, sc);
                navQuickLibraryButton = CreateNavQuickButton("navQuickLibraryButton", Language.GetText("Quick_StreamingLibrary"), innerW, sc);
                navQuickLogsButton = CreateNavQuickButton("navQuickLogsButton", Language.GetText("Quick_Logs"), innerW, sc);

                navPanel.Controls.Add(navQuickHeader);
                navPanel.Controls.Add(navQuickExtractButton);
                navPanel.Controls.Add(navQuickLibraryButton);
                navPanel.Controls.Add(navQuickLogsButton);

                // 复用的既有命令,通过 ToolTip 说明书与菜单入口一致
                ToolTipBotones.SetToolTip(navQuickExtractButton, Language.GetText("See extraction queue"));
                ToolTipBotones.SetToolTip(navQuickLibraryButton, Language.GetText("Manage Streaming Library"));
                ToolTipBotones.SetToolTip(navQuickLogsButton, Language.GetText("See logs"));

                // 侧栏整体接受链接文本拖放(含递归到的 Label/按钮等子控件),转发到 Main_DragDrop。
                EnableLinkDrop(navPanel);
                if (detailGroup is not null && !detailGroup.IsDisposed)
                    EnableLinkDrop(detailGroup);

                navRailInitialized = true;
                LayoutNavRail();
                ApplyNavRailTheme();
                UpdateNavRailTexts();
            }
            catch (Exception ex)
            {
                Log.WriteError("InitNavRail failed: " + ex.ToString());
            }
        }

        private Label CreateNavSectionHeader(string text, float sc)
        {
            var lbl = new Label();
            lbl.Name = "navSectionHeader";
            lbl.AutoSize = false;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lbl.Height = (int)Math.Round(20f * sc);
            lbl.Text = text;
            // 分节标题用加粗主色(与说明性灰字区分)。Font 只在此处建一次,
            // 换肤只改颜色——避免每次 ApplyTheme 都 New Font 造成 GDI 对象堆积。
            UiStyles.SetFont(lbl, -1, false, Font);
            return lbl;
        }

        private Button CreateNavQuickButton(string name, string text, int width, float sc)
        {
            var btn = new CommandButton();
            btn.Name = name;
            btn.Text = text;
            btn.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            btn.Height = Math.Max((int)Math.Round(34f * sc), Font.Height + 12);
            btn.Width = width;
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = (System.Windows.Forms.FlatStyle)Conversions.ToInteger(FlatStyle.Flat);
            UiStyles.SetRole(btn, UiStyles.Role.QuietButton);
            return btn;
        }

        /// <summary>左栏两段的所有几何一次算死,不依赖 Anchor 累加,避免 resize/DPI 变化漂移。
    /// 总览与快捷入口占据列表区下方,过滤器本体高度保持不变。</summary>
        private void LayoutNavRail()
        {
            var previousScroll = System.Drawing.Point.Empty;
            try
            {
                if (!navRailInitialized || navPanel is null)
                    return;
                // Lay out in unscrolled coordinates and then restore the reading position.
                previousScroll = navPanel.AutoScrollPosition;
                navPanel.AutoScrollPosition = System.Drawing.Point.Empty;
                float sc = GetUiScale();
                int pad = (int)Math.Round(6f * sc);
                int innerW = Math.Max(40, navPanel.ClientSize.Width - pad * 2);
                int rowH = (int)Math.Round(20f * sc);
                navListBox.Location = new System.Drawing.Point((int)Math.Round(8f * sc), (int)Math.Round(8f * sc));

                // 过滤器本体高度 = 5 项 × 真实行高(见 GetNavActualItemHeight);它不再 Dock=Fill,必须显式给定,
                // 否则默认 130 在字体/DPI 变化时会截断第 5 项。Normal 模式 ItemHeight 无效,不可直接用它。
                if (navListBox is not null && !navListBox.IsDisposed && navListBox.Items.Count > 0)
                {
                    FitNavListBoxNoScroll();
                }

                int y = navListBox.Bottom + (int)Math.Round(24f * sc);
                SetNavRailSectionVisible(false, true);
                navOverviewHeader.Visible = false;
                navOverviewCountsValue.Visible = false;
                navOverviewRemainingValue.Visible = false;
                var remCap = FindNavLabel("overviewRemainingCaption");
                if (remCap is not null)
                    remCap.Visible = false;
                navQuickHeader.SetBounds(pad + (int)Math.Round(6f * sc), y, innerW, (int)Math.Round(22f * sc));
                y = navQuickHeader.Bottom + (int)Math.Round(6f * sc);
                foreach (Button button in new Button[] { navQuickExtractButton, navQuickLibraryButton, navQuickLogsButton })
                {
                    button.SetBounds(pad, y, innerW, (int)Math.Round(34f * sc));
                    y = button.Bottom + (int)Math.Round(4f * sc);
                }
                y = Math.Max(y + (int)Math.Round(24f * sc), navPanel.ClientSize.Height - (int)Math.Round(180f * sc));
                var speedLabel = FindNavLabel("overviewSpeedLabel");
                if (speedLabel is not null)
                {
                    speedLabel.Visible = true;
                    speedLabel.SetBounds(pad + (int)Math.Round(6f * sc), y, innerW, rowH);
                }
                y += rowH;
                navOverviewSpeedValue.Visible = true;
                navOverviewSpeedValue.SetBounds(pad + (int)Math.Round(6f * sc), y, innerW, (int)Math.Round(38f * sc));
                y += (int)Math.Round(42f * sc);
                var progressLabel = FindNavLabel("overviewProgressCaption");
                if (progressLabel is not null)
                {
                    progressLabel.Visible = true;
                    progressLabel.SetBounds(pad + (int)Math.Round(6f * sc), y, innerW - (int)Math.Round(44f * sc), rowH);
                }
                navOverviewProgressText.Visible = true;
                navOverviewProgressText.SetBounds(pad + innerW - (int)Math.Round(44f * sc), y, (int)Math.Round(44f * sc), rowH);
                y += rowH + (int)Math.Round(4f * sc);
                navOverviewProgressBar.Visible = true;
                navOverviewProgressBar.SetBounds(pad + (int)Math.Round(6f * sc), y, innerW - (int)Math.Round(12f * sc), (int)Math.Round(5f * sc));
                y += (int)Math.Round(24f * sc);
                btnConfig.SetBounds(pad, y, innerW, (int)Math.Round(36f * sc));
                y = btnConfig.Bottom;
                navPanel.AutoScrollMinSize = new System.Drawing.Size(0, y + (int)Math.Round(12f * sc));
            }

            catch (Exception ex)
            {
                Log.WriteDebug("LayoutNavRail failed: " + Log.SafeException(ex));
            }
            finally
            {
                if (navPanel is not null && !navPanel.IsDisposed)
                {
                    navPanel.AutoScrollPosition = new System.Drawing.Point(-previousScroll.X, -previousScroll.Y);
                }
            }
        }

        /// <summary>按 Tag 找 navPanel 内的说明性 Label(它们不参与交互,无需字段)。</summary>
        private Label FindNavLabel(string tag)
        {
            if (navPanel is null)
                return default;
            foreach (Control c in navPanel.Controls)
            {
                if (c is Label && string.Equals(c.Tag as string, tag))
                    return (Label)c;
            }
            return default;
        }

        /// <summary>整体显隐左栏两段。overview 与 quick 分开控制,便于未来只保留总览。</summary>
        private void SetNavRailSectionVisible(bool overviewVisible, bool quickVisible)
        {
            try
            {
                if (navOverviewHeader is not null)
                    navOverviewHeader.Visible = overviewVisible;
                if (navOverviewSpeedValue is not null)
                    navOverviewSpeedValue.Visible = overviewVisible;
                if (navOverviewCountsValue is not null)
                    navOverviewCountsValue.Visible = overviewVisible;
                if (navOverviewProgressBar is not null)
                    navOverviewProgressBar.Visible = overviewVisible;
                if (navOverviewProgressText is not null)
                    navOverviewProgressText.Visible = overviewVisible;
                if (navOverviewRemainingValue is not null)
                    navOverviewRemainingValue.Visible = overviewVisible;
                foreach (string tag in new string[] { "overviewSpeedLabel", "overviewProgressCaption", "overviewRemainingCaption" })
                {
                    var l = FindNavLabel(tag);
                    if (l is not null)
                        l.Visible = overviewVisible;
                }

                if (navQuickHeader is not null)
                    navQuickHeader.Visible = quickVisible;
                if (navQuickExtractButton is not null)
                    navQuickExtractButton.Visible = quickVisible;
                if (navQuickLibraryButton is not null)
                    navQuickLibraryButton.Visible = quickVisible;
                if (navQuickLogsButton is not null)
                    navQuickLogsButton.Visible = quickVisible;
            }
            catch (Exception ex)
            {
                Log.WriteDebug("SetNavRailSectionVisible failed: " + Log.SafeException(ex));
            }
        }

        private float GetUiScale()
        {
            return Math.Max(1.0f, DeviceDpi / 96.0f);
        }

        /// <summary>切换语言后重建左栏所有文案。计数/速度等动态值由 UpdateNavRailOverview 刷新。</summary>
        private void UpdateNavRailTexts()
        {
            try
            {
                if (!navRailInitialized)
                    return;
                navOverviewHeader.Text = Language.GetText("Overview_Title");
                navQuickHeader.Text = Language.GetText("Quick_Title");
                var speedLbl = FindNavLabel("overviewSpeedLabel");
                if (speedLbl is not null)
                    speedLbl.Text = Language.GetText("Overview_Speed");
                var progCap = FindNavLabel("overviewProgressCaption");
                if (progCap is not null)
                    progCap.Text = Language.GetText("Overview_Progress");
                var remCap = FindNavLabel("overviewRemainingCaption");
                if (remCap is not null)
                    remCap.Text = Language.GetText("Overview_Remaining");
                if (navQuickExtractButton is not null)
                    navQuickExtractButton.Text = Language.GetText("Quick_ExtractQueue");
                if (navQuickLibraryButton is not null)
                    navQuickLibraryButton.Text = Language.GetText("Quick_StreamingLibrary");
                if (navQuickLogsButton is not null)
                    navQuickLogsButton.Text = Language.GetText("Quick_Logs");
            }
            catch (Exception ex)
            {
                Log.WriteDebug("UpdateNavRailTexts failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>
    /// 任务总览数值刷新。在 430ms 刷新循环里调用(UI 线程),速度直接取该循环算好的
    /// VelocidadGlobalDescarga;计数复用 UpdateNavCounts 的同一判定 MatchesScope,
    /// 保证与过滤器后缀括号里的数字完全一致;进度与剩余时间按字节加权。
    /// </summary>
        private void UpdateNavRailOverview()
        {
            // P1(审查):本函数在 430ms 后台循环里被直接调用,此前直接写控件属性=
            // 跨线程违规(与 UpdateQuotaUI/SetStatusBar 的编组惯例相悖),且无锁遍历
            // ListaPaquetes(他处持 Mutex.ListaDescargas 增删,foreach 可抛)。
            // 修法:先编组回 UI(同 UpdateQuotaUI 的 BeginInvoke 模式),计数快照持锁。
            try
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                if (InvokeRequired)
                {
                    try
                    {
                        BeginInvoke(new Action(UpdateNavRailOverview));
                    }
                    catch
                    {
                    }
                    return;
                }
                if (!navRailInitialized)
                    return;
                if (navPanel is null || navPanel.IsDisposed)
                    return;

                // 速度(与状态栏同源,单位换算复用 PintarVelocidadDescarga)
                string speedTxt = "-";
                if (VelocidadGlobalDescarga.HasValue && VelocidadGlobalDescarga.Value > 0m)
                {
                    speedTxt = PintarVelocidadDescarga(VelocidadGlobalDescarga.Value);
                }
                UiStyles.SetText(navOverviewSpeedValue, speedTxt);
                try
                {
                    TintNavSpeedValue();
                }
                catch
                {
                }

                // 导航和总览读取同一份后台快照，不在界面线程重复遍历队列。
                var snapshot = System.Threading.Volatile.Read(ref queueOverviewSnapshot);
                if (snapshot is null)
                    return;
                decimal totalBytes = snapshot.TotalBytes;
                decimal doneBytes = snapshot.DoneBytes;
                UiStyles.SetText(navOverviewCountsValue, Language.GetText("Overview_Active") + " " + snapshot.Totals[1].ToString() + "   " + Language.GetText("Overview_Queued") + " " + snapshot.Totals[2].ToString() + "   " + Language.GetText("Overview_Failed") + " " + snapshot.Totals[3].ToString());

                int pctOverall = 0;
                if (totalBytes > 0m)
                    pctOverall = (int)Math.Round(Math.Floor(doneBytes * 100m / totalBytes));
                if (pctOverall < 0)
                    pctOverall = 0;
                if (pctOverall > 100)
                    pctOverall = 100;

                // ProgressBar.Value 越界会抛异常(高 DPI/极小文件时 pct 计算可能溢出)
                try
                {
                    if (navOverviewProgressBar.Value != pctOverall)
                        navOverviewProgressBar.Value = pctOverall;
                }
                catch
                {
                }
                UiStyles.SetText(navOverviewProgressText, pctOverall.ToString() + "%");

                // 剩余:总剩余字节 / 当前总速度(KB/s)。速度缺失或无剩余时不显示估算,避免编造数字。
                string remainTxt = "-";
                if (totalBytes > 0m && doneBytes < totalBytes && VelocidadGlobalDescarga.HasValue && VelocidadGlobalDescarga.Value > 0m)
                {
                    decimal remainKB = (totalBytes - doneBytes) / 1024m;
                    decimal secs = remainKB / VelocidadGlobalDescarga.Value;
                    if (secs > 0m && secs < 86400 * 7)
                    {
                        remainTxt = FormatNavRemaining((int)Math.Round(Math.Ceiling(secs)));
                    }
                }
                UiStyles.SetText(navOverviewRemainingValue, remainTxt);
            }
            catch (Exception ex)
            {
                Log.WriteDebug("UpdateNavRailOverview failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>剩余时间简写:&lt;1min 读秒,否则分/时。与配额倒计时风格保持一致。</summary>
        private static string FormatNavRemaining(int totalSec)
        {
            if (totalSec < 60)
                return totalSec.ToString() + " s";
            int totalMin = totalSec / 60;
            if (totalMin < 60)
                return totalMin.ToString() + " min";
            int h = totalMin / 60;
            int m = totalMin % 60;
            if (h >= 24)
            {
                int d = h / 24;
                return d.ToString() + " d " + (h % 24).ToString() + " h";
            }
            return h.ToString() + " h " + m.ToString() + " min";
        }

        /// <summary>左栏总览/快捷入口换肤。分节标题用主题强调色(深浅主题下分别是深蓝/浅蓝),
    /// 说明文字用次要灰,数值用主色/语义色区分——避免之前全取 Border 一片灰、换肤也看不出变化。</summary>
        private void ApplyNavRailTheme()
        {
            try
            {
                if (!navRailInitialized)
                    return;
                // P2-8: 说明文字此前取 "Border"(边框色)当前景。三个 Label 自身透明,
                // 挂在 navPanel(Panel → ControlBack)上,对比度浅色只有 1.38:1、深色 1.82:1,
                // 远低于正文 4.5:1 的下限 → 左栏总览说明几乎读不出来。
                // 改用主题 Muted 专用色(对 ControlBack:浅 5.16:1 / 深 4.97:1)。
                var subtle = ThemeManager.GetColor("Muted");
                var fore = ThemeManager.GetColor("Fore");
                var accent = ThemeManager.GetColor("Muted");

                // 分节标题:强调色+加粗(换肤可见),不再用灰。
                foreach (Label hdr in new Label[] { navOverviewHeader, navQuickHeader })
                {
                    if (hdr is not null)
                    {
                        hdr.ForeColor = accent;
                    }
                }

                // 计数行:主色保证可读(之前全灰,数字看不清)。
                if (navOverviewCountsValue is not null)
                    navOverviewCountsValue.ForeColor = fore;

                // 左列说明(label)次要灰,右列数值主色/语义色,两列一眼区分。
                var speedLbl = FindNavLabel("overviewSpeedLabel");
                if (speedLbl is not null)
                    speedLbl.ForeColor = subtle;
                var progCap = FindNavLabel("overviewProgressCaption");
                if (progCap is not null)
                    progCap.ForeColor = subtle;
                var remCap = FindNavLabel("overviewRemainingCaption");
                if (remCap is not null)
                    remCap.ForeColor = subtle;

                // 总速度:有速度时语义绿,无速度(-)时主色(由 UpdateNavRailOverview 每次刷新后重调)。
                if (navOverviewSpeedValue is not null)
                    navOverviewSpeedValue.ForeColor = fore;
                if (navOverviewProgressText is not null)
                    navOverviewProgressText.ForeColor = fore;
                if (navOverviewRemainingValue is not null)
                    navOverviewRemainingValue.ForeColor = fore;
                try
                {
                    TintNavSpeedValue();
                }
                catch
                {
                }

                // 快捷入口按钮:沿用主题按钮配色(与工具栏一致的 Flat + 边框)
                foreach (Button btn in new Button[] { navQuickExtractButton, navQuickLibraryButton, navQuickLogsButton })
                {
                    if (btn is not null)
                    {
                        UiStyles.SetRole(btn, UiStyles.Role.QuietButton);
                    }
                }

                // 进度条走主题 token(与列表内进度条同色系)
                // 注意:系统 VisualStyles 下 ProgressBar 会忽略 Back/Fore,此处尽力而为。
                if (navOverviewProgressBar is not null)
                {
                    navOverviewProgressBar.BackColor = ThemeManager.GetColor("ControlBack");
                    navOverviewProgressBar.ForeColor = ThemeManager.GetColor("Selection");
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ApplyNavRailTheme failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>总速度数值语义色:下载中绿色强调, idle 显示 - 时回主色,换肤/每次刷新都重调。</summary>
        private void TintNavSpeedValue()
        {
            if (navOverviewSpeedValue is null || navOverviewSpeedValue.IsDisposed)
                return;
            navOverviewSpeedValue.ForeColor = ThemeManager.GetColor("Fore");
        }

        #endregion

        #region v2.5: 右侧栏结构化详情（已迁移至 Forms/Main.DetailPanel.vb）

        // 实现已移至 Forms/Main.DetailPanel.vb（Partial 同类纯平移，无逻辑改动）。
        // 本 Region 仅留索引；Designer 不受影响（运行时控件，无 Handles 绑定）。

        #endregion

        /// <summary>配额横幅:Anchor 布局,显示/隐藏时整体下移下载列表,无 Dock 冲突。
    /// RC:Top 跟随工具栏底部(不再写死 40),高度按 DPI 缩放,防 125%/150% 下压住工具栏。</summary>
        private void InitQuotaBanner()
        {
            float sc = 1.0f;
            try
            {
                using (var g = CreateGraphics())
                {
                    sc = g.DpiY / 96.0f;
                }
            }
            catch
            {
            }
            if (sc < 1.0f)
                sc = 1.0f;
            int bannerH = (int)Math.Round(30f * sc);
            quotaBannerPanel = new Panel();
            quotaBannerPanel.Name = "quotaBannerPanel";
            quotaBannerPanel.Height = bannerH;
            quotaBannerPanel.Left = 0;
            try
            {
                quotaBannerPanel.Top = TableLayoutPanel1.Bottom;
            }
            catch
            {
                quotaBannerPanel.Top = 40;
            }
            quotaBannerPanel.Width = ClientSize.Width;
            quotaBannerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            quotaBannerPanel.Visible = false;

            quotaBannerLabel = new Label();
            quotaBannerLabel.Name = "quotaBannerLabel";
            quotaBannerLabel.AutoSize = false;
            quotaBannerLabel.Left = 12;
            quotaBannerLabel.Top = 0;
            quotaBannerLabel.Height = bannerH;
            quotaBannerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            quotaBannerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            quotaRetryNowButton = new CommandButton();
            quotaRetryNowButton.Name = "quotaRetryNowButton";
            quotaRetryNowButton.Height = (int)Math.Round(23f * sc);
            quotaRetryNowButton.Width = (int)Math.Round(110f * sc);
            quotaRetryNowButton.Top = (bannerH - quotaRetryNowButton.Height) / 2;
            quotaRetryNowButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            quotaRetryNowButton.Click += QuotaRetryNow_Click;

            quotaBannerPanel.Controls.Add(quotaBannerLabel);
            quotaBannerPanel.Controls.Add(quotaRetryNowButton);
            Controls.Add(quotaBannerPanel);
            quotaBannerPanel.BringToFront();
            LayoutQuotaBanner();
            ApplyQuotaBannerTheme();
            UpdateQuotaBannerTexts();
        }

        private void LayoutQuotaBanner()
        {
            if (quotaBannerPanel is null || quotaRetryNowButton is null || quotaBannerLabel is null)
                return;
            try
            {
                quotaBannerPanel.Top = TableLayoutPanel1.Bottom;
            }
            catch
            {
            }
            quotaBannerPanel.Width = ClientSize.Width;
            quotaRetryNowButton.Left = quotaBannerPanel.Width - quotaRetryNowButton.Width - 12;
            quotaBannerLabel.Width = Math.Max(50, quotaRetryNowButton.Left - 18);
        }

        private void ApplyQuotaBannerTheme()
        {
            if (quotaBannerPanel is null || quotaBannerLabel is null)
                return;
            quotaBannerPanel.BackColor = ThemeManager.GetColor("QuotaBack");
            quotaBannerLabel.ForeColor = ThemeManager.GetColor("QuotaFore");
            try
            {
                quotaRetryNowButton.BackColor = ThemeManager.GetColor("QuotaBack");
                quotaRetryNowButton.ForeColor = ThemeManager.GetColor("QuotaFore");
                quotaRetryNowButton.FlatStyle = FlatStyle.Flat;
                quotaRetryNowButton.FlatAppearance.BorderColor = ThemeManager.GetColor("QuotaFore");
            }
            catch
            {
            }
        }

        private void UpdateQuotaBannerTexts()
        {
            if (quotaBannerLabel is null || quotaRetryNowButton is null)
                return;
            quotaRetryNowButton.Text = Language.GetText("Quota_RetryNow");
            if (string.IsNullOrEmpty(quotaRetryNowButton.Text))
                quotaRetryNowButton.Text = "Retry now";
        }

        /// <summary>配额倒计时文案。Ceiling 分钟在最后 60 秒会卡住"1 min"不动,
    /// 且 1h59m30s 会进位成"2 h 0 min":120 秒内直接读秒,以上向下取整。</summary>
        private static string FormatQuotaRemaining(TimeSpan span)
        {
            int totalSec = Math.Max(1, (int)Math.Round(Math.Ceiling(span.TotalSeconds)));
            if (totalSec < 120)
            {
                if (totalSec < 60)
                    return totalSec.ToString() + " s";
                return "1 min " + (totalSec - 60).ToString() + " s";
            }
            int totalMin = totalSec / 60;
            int h = totalMin / 60;
            int m = totalMin % 60;
            if (h > 0)
                return h.ToString() + " h " + m.ToString() + " min";
            return m.ToString() + " min";
        }

        /// <summary>后台线程每 430ms 调用,内部编组回 UI。配额进入/解除时各弹一次气泡。</summary>
        private void UpdateQuotaUI()
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(UpdateQuotaUI));
                    return;
                }
                if (quotaBannerPanel is null)
                    return;
                var quotaRem = MegaQuotaManager.GetRemaining();
                if (quotaRem.HasValue)
                {
                    quotaBannerPanel.Visible = true;
                    LayoutQuotaBanner();
                    LayoutDownloadArea();
                    quotaBannerLabel.Text = Language.GetText("Quota_Banner").Replace("%T%", FormatQuotaRemaining(quotaRem.Value));
                    if (!quotaNotified)
                    {
                        quotaNotified = true;
                        Log.WriteWarning("Showing MEGA quota banner, remaining " + quotaRem.Value.ToString());
                        try
                        {
                            IconoMinimizado.ShowBalloonTip(5000, "MegaDownloader", quotaBannerLabel.Text, ToolTipIcon.Warning);
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    if (quotaBannerPanel.Visible)
                    {
                        quotaBannerPanel.Visible = false;
                        LayoutDownloadArea();
                    }
                    if (quotaNotified)
                    {
                        quotaNotified = false;
                        Log.WriteWarning("MEGA quota cleared; queue auto-resumes.");
                        try
                        {
                            IconoMinimizado.ShowBalloonTip(5000, "MegaDownloader", Language.GetText("Quota_Recovered"), ToolTipIcon.Info);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void QuotaRetryNow_Click(object sender, EventArgs e)
        {
            try
            {
                MegaQuotaManager.ClearQuota();
                WakeQuotaFailedItems();
                UpdateQuotaUI();
                RefreshListaDescargas(true);
            }
            catch (Exception ex)
            {
                Log.WriteError("QuotaRetryNow failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>把配额失败项全部唤回 EnCola(手动出口与到期自动恢复共用)。
    /// 收集用局部表:调用方横跨 UI 线程(立即重试)与调度线程(到期边沿),共享字段会竞态重复唤醒。</summary>
        private void WakeQuotaFailedItems()
        {
            int woken = 0;
            var pend = new List<Fichero>();
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero fic in paq.ListaFicheros)
                    {
                        if (fic.DescargaEstado() == Estado.Erroneo && fic.FailedByQuota)
                        {
                            pend.Add(fic);
                        }
                    }
                }
            }
            foreach (Fichero fic in pend)
            {
                // P0-1:配额唤醒保留断点,不删 .part。
                fic.ResetearDescarga(true);
                fic.SetDescargaEstado = Estado.EnCola;
                woken += 1;
            }
            if (woken > 0)
                Log.WriteWarning("Woke " + woken + " quota-failed files back to queue.");
        }

        private void InitBatchMenu()
        {
            RetryAllFailedMenuItem.Name = "RetryAllFailedMenuItem";
            RemoveAllFailedMenuItem.Name = "RemoveAllFailedMenuItem";
            ResetColumnsMenuItem.Name = "ResetColumnsMenuItem";
            sepBatchMenuItem.Name = "sepBatchMenuItem";
            sepColumnsMenuItem.Name = "sepColumnsMenuItem";
            MenuDescarga.Items.Add(sepBatchMenuItem);
            MenuDescarga.Items.Add(RetryAllFailedMenuItem);
            MenuDescarga.Items.Add(RemoveAllFailedMenuItem);
            MenuDescarga.Items.Add(sepColumnsMenuItem);
            MenuDescarga.Items.Add(ResetColumnsMenuItem);
            UpdateBatchMenuTexts();
        }

        private void UpdateBatchMenuTexts()
        {
            RetryAllFailedMenuItem.Text = Language.GetText("Retry all failed");
            if (string.IsNullOrEmpty(RetryAllFailedMenuItem.Text))
                RetryAllFailedMenuItem.Text = "Retry all failed";
            RemoveAllFailedMenuItem.Text = Language.GetText("Remove all failed");
            if (string.IsNullOrEmpty(RemoveAllFailedMenuItem.Text))
                RemoveAllFailedMenuItem.Text = "Remove all failed";
            ResetColumnsMenuItem.Text = Language.GetText("Reset column widths");
            if (string.IsNullOrEmpty(ResetColumnsMenuItem.Text))
                ResetColumnsMenuItem.Text = "Reset column widths";
        }

        private void ResetColumnsMenuItem_Click(object sender, EventArgs e)
        {
            // 程序内回到默认的入口:此前 # 列 Hideable=False + 迁移已跑过,坏状态只能手改配置。
            try
            {
                ApplyColumnDefaults();
                try
                {
                    Config.ConfigUI.EstadoLista = ListaDescargas.SaveState();
                    Config.GuardarXML(false);
                }
                catch (Exception ex)
                {
                    Log.WriteError("ResetColumns persist failed: " + ex.ToString());
                }
                RefreshListaDescargas(true);
                ToastForm.ShowToast(this, ResetColumnsMenuItem.Text);
            }
            catch (Exception ex)
            {
                Log.WriteError("ResetColumns failed: " + Log.SafeException(ex));
            }
        }

        private List<Fichero> CollectErroneoFiles()
        {
            var res = new List<Fichero>();
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero fic in paq.ListaFicheros)
                    {
                        if (fic.DescargaEstado() == Estado.Erroneo)
                            res.Add(fic);
                    }
                }
            }
            return res;
        }

        private void RetryAllFailedMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var lista = CollectErroneoFiles();
                foreach (Fichero fic in lista)
                {
                    fic.ResetearDescarga();
                    fic.SetDescargaEstado = Estado.EnCola;
                }
                Log.WriteWarning("Retrying all failed files: " + lista.Count);
                RefreshListaDescargas(true);
            }
            catch (Exception ex)
            {
                Log.WriteError("RetryAllFailed failed: " + Log.SafeException(ex));
            }
        }

        private void RemoveAllFailedMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var lista = CollectErroneoFiles();
                if (lista.Count == 0)
                    return;
                string confirm = Language.GetText("Remove all failed confirm").Replace("%N%", lista.Count.ToString());
                if (MessageBox.Show(confirm, Language.GetText("Confirmation"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != System.Windows.Forms.DialogResult.OK)
                    return;
                bool deletePart = MessageBox.Show(Language.GetText("Remove all failed delete part"), Language.GetText("Confirmation"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes;
                foreach (Fichero fic in lista)
                    Eliminar(fic, deletePart, false);
                RefreshListaDescargas(true);
            }
            catch (Exception ex)
            {
                Log.WriteError("RemoveAllFailed failed: " + Log.SafeException(ex));
            }
        }

        private string PintarVelocidadDescarga(IDescarga ele)
        {
            if (ele.DescargaEstado() == Estado.Descargando)
            {
                return PintarVelocidadDescarga(ele.DescargaVelocidadKBs());
            }
            else
            {
                return "";
            }
        }
        private string PintarVelocidadDescarga(decimal vel)
        {
            string Dato = "KB/s";
            if (vel > 1024m)
            {
                Dato = "MB/s";
                vel = vel / 1024m;
            }
            return vel.ToString("F2") + " " + Dato;

        }

        private string PintarTamano(decimal numBytes)
        {
            string Dato = "B";
            if (numBytes > 1024m)
            {
                Dato = "KB";
                numBytes = numBytes / 1024m;
            }
            if (numBytes > 1024m)
            {
                Dato = "MB";
                numBytes = numBytes / 1024m;
            }
            if (numBytes > 1024m)
            {
                Dato = "GB";
                numBytes = numBytes / 1024m;
            }
            if (numBytes > 1024m)
            {
                Dato = "TB";
                numBytes = numBytes / 1024m;
            }
            return numBytes.ToString("F2") + " " + Dato;

        }


        /// <summary>列宽上下限(拖拽"无限长"的根因:此前所有列都无 Minimum/MaximumWidth)。</summary>
        private void ApplyColumnWidthLimits()
        {
            try
            {
                if (ListaDescargas is null || ListaDescargas.AllColumns is null)
                    return;
                if (ListaDescargas.AllColumns.Count < 10)
                    return;
                SetColumnWidthLimit(0, 180, 900);
                SetColumnWidthLimit(1, 40, 64);
                SetColumnWidthLimit(2, 60, 120);
                SetColumnWidthLimit(3, 60, 120);
                SetColumnWidthLimit(4, 90, 320);
                SetColumnWidthLimit(5, 45, 80);
                SetColumnWidthLimit(6, 120, 260);
                SetColumnWidthLimit(7, 55, 120);
                SetColumnWidthLimit(8, 55, 120);
                SetColumnWidthLimit(9, 55, 120);
                // 文件名列保持自动吃剩余空间(窗口拉宽不留白);"反向"手感由上下限兜底:它永远不会被压成 0。
                ListaDescargas.AllColumns[0].FillsFreeSpace = true;
            }
            catch (Exception ex)
            {
                Log.WriteError("ApplyColumnWidthLimits failed: " + ex.ToString());
            }
        }

        private void SetColumnWidthLimit(int idx, int minW, int maxW)
        {
            try
            {
                var col = ListaDescargas.AllColumns[idx];
                if (col is null)
                    return;
                col.MinimumWidth = (int)Math.Round(minW * GetUiScale());
                col.MaximumWidth = (int)Math.Round(maxW * GetUiScale());
            }
            catch (Exception ex)
            {
                Log.WriteError("SetColumnWidthLimit failed: " + ex.ToString());
            }
        }

        /// <summary>全量列默认:顺序/显隐/宽度。右键恢复与自愈共用,保持行为一致。</summary>
        private void ApplyColumnDefaults()
        {
            try
            {
                if (ListaDescargas is null || ListaDescargas.AllColumns is null)
                    return;
                if (ListaDescargas.AllColumns.Count < 10)
                    return;
                ApplyColumnWidthLimits();
                int[] widths = new int[] { 240, 44, 76, 84, 112, 55, 144, 92, 92, 68 };
                for (int i = 0; i <= 9; i++)
                {
                    var col = ListaDescargas.AllColumns[i];
                    col.Width = (int)Math.Round(widths[i] * GetUiScale());
                    col.IsVisible = i != 1 && i != 2 && i != 5 && i != 9;
                    try
                    {
                        col.DisplayIndex = i;
                    }
                    catch
                    {
                    }
                }
                // Name is the native tree column. Priority, downloaded, numeric-only
                // percentage and remaining bytes stay available from the column menu.
                ListaDescargas.AllColumns[2].IsVisible = false;
                ListaDescargas.AllColumns[9].IsVisible = false;
                ListaDescargas.AllColumns[0].FillsFreeSpace = true;
                for (int i = 0; i <= 9; i++)
                {
                    if (i != 0)
                    {
                        try
                        {
                            ListaDescargas.AllColumns[i].FillsFreeSpace = false;
                        }
                        catch
                        {
                        }
                    }
                }
                ListaDescargas.RebuildColumns();
                ThemeManager.RefreshListTheme(ListaDescargas);
                Log.WriteWarning("Applied column defaults (widths/order/visibility).");
            }
            catch (Exception ex)
            {
                Log.WriteError("ApplyColumnDefaults failed: " + ex.ToString());
            }
        }

        /// <summary>
    /// 坏列状态自愈:RestoreState 读回的持久化宽度若出现关键列被压 0 / 单列上千像素 /
    /// 可见列总宽远超窗口,即判 corruption。返回 True 表示已重置,调用方负责落盘。
    /// </summary>
        private bool RepairColumnStateIfCorrupted()
        {
            try
            {
                if (ListaDescargas is null || ListaDescargas.AllColumns is null)
                    return false;
                if (ListaDescargas.AllColumns.Count < 10)
                    return false;
                var hashCol = ListaDescargas.AllColumns[1];
                var nameCol = ListaDescargas.AllColumns[0];
                if (hashCol.Width < 20 || nameCol.Width < 50)
                {
                    Log.WriteWarning("Column state corrupted (#=" + hashCol.Width + ", Nombre=" + nameCol.Width + "); resetting to defaults.");
                    ApplyColumnDefaults();
                    return true;
                }
                int total = 0;
                foreach (OLVColumn c in ListaDescargas.AllColumns)
                {
                    if (c.Width < 0 || c.Width > 800)
                    {
                        Log.WriteWarning("Column state corrupted (col '" + c.Text + "' width=" + c.Width + "); resetting to defaults.");
                        ApplyColumnDefaults();
                        return true;
                    }
                    try
                    {
                        if (c.IsVisible)
                            total += c.Width;
                    }
                    catch
                    {
                        total += c.Width;
                    }
                }
                // 总宽阈值跟窗口走:上限夹取后合法最大约 2120,固定 2000 会误伤宽屏手动布局。
                int totalLimit = Math.Max(2000, ClientSize.Width * 2);
                if (total > totalLimit)
                {
                    Log.WriteWarning("Column state corrupted (visible total=" + total + "); resetting to defaults.");
                    ApplyColumnDefaults();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.WriteError("RepairColumnStateIfCorrupted failed: " + ex.ToString());
                return false;
            }
        }

        private void ListaDescargas_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            // OLV Minimum/MaximumWidth 不拦截表头拖拽,此处手动夹取,防止再次拖出 0 / 上千像素。
            try
            {
                ObjectListView olv = sender as ObjectListView;
                if (olv is null || olv.Columns is null)
                    return;
                if (e.ColumnIndex < 0 || e.ColumnIndex >= olv.Columns.Count)
                    return;
                OLVColumn col = olv.Columns[e.ColumnIndex] as OLVColumn;
                if (col is null)
                    return;
                if (col.MinimumWidth > 0 && e.NewWidth < col.MinimumWidth)
                    e.NewWidth = col.MinimumWidth;
                if (col.MaximumWidth > 0 && e.NewWidth > col.MaximumWidth)
                    e.NewWidth = col.MaximumWidth;
            }
            catch (Exception ex)
            {
                Log.WriteError("ColumnWidthChanging clamp failed: " + ex.ToString());
            }
        }

        /// <summary>
    /// P1-10 UI:错误行内联原因首行(截断 40 字)。只读 Fichero.DescripcionError 字段,O(1),
    /// 可安全跑在 2.3Hz 列表刷新热路径;包级别不扫子文件(展开看各文件原因),外层 Try 已兜底。
    /// </summary>
        private static string ShortErrorReason(IDescarga ele)
        {
            Fichero fic = ele as Fichero;
            if (fic is null)
                return null;
            string msg = fic.DescripcionError;
            if (string.IsNullOrEmpty(msg))
                return null;
            int cut = msg.IndexOf(Constants.vbLf);
            if (cut >= 0)
                msg = msg.Substring(0, cut);
            msg = msg.Trim().Replace(Constants.vbCr, " ").Replace(Constants.vbTab, " ");
            if (msg.Length > 40)
                msg = msg.Substring(0, 40) + "...";
            if (msg.Length == 0)
                return null;
            return msg;
        }

        // P2-11b:左导航分组 + 计数。计数与过滤共用 DownloadEstadoFilter.MatchesScope,所见即所数。
        // 计数单位是包(列表顶层行),不是文件:此前连包带文件各算一遍,一个 12 文件的完成包
        // 就贡献 13,导航"已完成(25)"而列表只有 2 个包——口径与所见不一致,现只数包。
        private DownloadEstadoFilter.NavScope _navScope = DownloadEstadoFilter.NavScope.All;
        private bool _updatingNav = false;

        private void InitNavList()
        {
            // RC:ItemHeight 跟随 DPI/字体(此前写死 20,大字体下截断)。
            // 注意:DrawMode=Normal 时 ItemHeight 被忽略,真实行高由字体决定,
            // 高度必须按 GetItemHeight 实测(见 GetNavActualItemHeight),否则 5 项放不下出竖滚。
            try
            {
                navListBox.IntegralHeight = false;
                navListBox.HorizontalScrollbar = false;
                navListBox.ScrollAlwaysVisible = false;
            }
            catch
            {
            }
            try
            {
                navListBox.ItemHeight = Math.Max((int)Math.Round(40f * GetUiScale()), navListBox.Font.Height + 14);
            }
            catch
            {
            }
            UpdateNavCounts();
            if (navListBox.SelectedIndex < 0)
                navListBox.SelectedIndex = 0;
            ApplyNavListColors();
        }

        /// <summary>左导航真实行高。Normal 模式下 ItemHeight 属性无效,必须用 GetItemHeight 实测,
    /// 否则按 ItemHeight×5 算出的高度偏小,第 5 项被截断需要手动滚动。</summary>
        private int GetNavActualItemHeight()
        {
            try
            {
                if (navListBox is not null && !navListBox.IsDisposed && navListBox.Items.Count > 0)
                {
                    int h = navListBox.GetItemHeight(0);
                    if (h > 0)
                        return h;
                }
            }
            catch
            {
            }
            try
            {
                if (navListBox is not null && navListBox.Font is not null)
                {
                    return Math.Max(20, navListBox.Font.Height + 6);
                }
            }
            catch
            {
            }
            return 20;
        }

        /// <summary>左导航免滚动适配:高度按真实行高×项数一次给够(无竖滚),
    /// 宽度占满 navPanel;文本超宽时优先加宽面板(≤220),平时无任何滚动条,
    /// 窄窗口实在加不动才出横滚兜底(此时高度补上横滚条高度,横滚不再顶出竖滚)。</summary>
        private void FitNavListBoxNoScroll()
        {
            if (navListBox is null || navPanel is null)
                return;
            int pad = (int)Math.Round(8f * GetUiScale());
            navListBox.HorizontalScrollbar = false;
            navListBox.Width = Math.Max(40, navPanel.ClientSize.Width - pad * 2);
            navListBox.Height = GetNavActualItemHeight() * navListBox.Items.Count;
        }

        private void ApplyNavListColors()
        {
            try
            {
                if (navListBox is null || navListBox.IsDisposed)
                    return;
                // ListBox 不在 ThemeManager 递归覆盖范围,手动同步
                navListBox.BackColor = ThemeManager.GetColor("ControlBack");
                navListBox.ForeColor = ThemeManager.GetColor("Fore");
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ApplyNavListColors failed: " + Log.SafeException(ex));
            }
        }

        private void navListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingNav)
                return;
            try
            {
                var scope = DownloadEstadoFilter.NavScope.All;
                switch (navListBox.SelectedIndex)
                {
                    case 1:
                        scope = DownloadEstadoFilter.NavScope.Downloading;
                        break;
                    case 2:
                        scope = DownloadEstadoFilter.NavScope.Waiting;
                        break;
                    case 3:
                        scope = DownloadEstadoFilter.NavScope.Failed;
                        break;
                    case 4:
                        scope = DownloadEstadoFilter.NavScope.Completed;
                        break;
                }
                if (scope == _navScope)
                    return;
                _navScope = scope;
                RebuildWorkbenchRoots();
            }
            catch (Exception ex)
            {
                Log.WriteError("navListBox_SelectedIndexChanged failed: " + ex.ToString());
            }
        }

        private void UpdateNavCounts()
        {
            if (navListBox is null || navListBox.IsDisposed)
                return;
            var snapshot = System.Threading.Volatile.Read(ref queueOverviewSnapshot);
            var totals = new int[5];
            if (snapshot is not null)
            {
                Array.Copy(snapshot.Totals, totals, totals.Length);
            }
            else if (ListaPaquetes is not null)
            {
                lock (Mutex.ListaDescargas)
                {
                    foreach (Paquete package in ListaPaquetes)
                        CountNavObject(package, totals);
                }
            }
            bool changed = navListBox.Items.Count != 5;
            var texts = new string[5];
            string[] keys = new string[] { "Nav_All", "Nav_Downloading", "Nav_Waiting", "Nav_Failed", "Nav_Completed" };
            for (int i = 0; i <= 4; i++)
            {
                navTitles[i] = Language.GetText(keys[i]);
                texts[i] = navTitles[i] + " (" + totals[i].ToString() + ")";
                if (navListBox.Items.Count <= i || !Equals(navListBox.Items[i], texts[i]))
                    changed = true;
            }
            navTotals = totals;
            if (changed)
            {
                _updatingNav = true;
                navListBox.BeginUpdate();
                try
                {
                    while (navListBox.Items.Count < 5)
                        navListBox.Items.Add("");
                    for (int i = 0; i <= 4; i++)
                    {
                        if (!Equals(navListBox.Items[i], texts[i]))
                            navListBox.Items[i] = texts[i];
                    }
                    if (navListBox.SelectedIndex < 0)
                        navListBox.SelectedIndex = 0;
                }
                finally
                {
                    navListBox.EndUpdate();
                    _updatingNav = false;
                }
                navListBox.Invalidate();
            }
            UpdateWorkbenchHeading();
        }

        // Top-level package counts use the same scope rules as the task list.


        private static void CountNavObject(object obj, int[] totals)
        {
            totals[0] += 1;
            for (int s = 1; s <= 4; s++)
            {
                if (DownloadEstadoFilter.MatchesScope(obj, (DownloadEstadoFilter.NavScope)s))
                    totals[s] += 1;
            }
        }

        /// <summary>
    /// P2-11b:按当前分组重设 Roots(包命中或子文件穿透才留)。SetObjects 会重置 Roots
    /// 为全量,故每次重建后调用;False 刷新沿用当前 Roots。禁用 OLV UseFiltering。
    /// </summary>
        private void ApplyNavRoots()
        {
            if (ListaDescargas is null || ListaPaquetes is null || !ListaDescargas.IsHandleCreated || ListaDescargas.IsDisposed)
                return;
            lock (Mutex.ListaDescargas)
            {
                if (_navScope == DownloadEstadoFilter.NavScope.All && queueQuery.Length == 0)
                {
                    ListaDescargas.Roots = ListaPaquetes;
                    queueVisibleCount = ListaPaquetes.Count;
                }
                else
                {
                    var shown = new List<Paquete>();
                    foreach (Paquete package in ListaPaquetes)
                    {
                        if (MatchesQueuePackage(package))
                            shown.Add(package);
                    }
                    ListaDescargas.Roots = shown;
                    queueVisibleCount = shown.Count;
                }
            }
            UpdateWorkbenchHeading();
        }

        // Explicit geometry prevents anchor drift during window resizing.


        private void LayoutDownloadArea()
        {
            try
            {
                if (workbenchReady)
                {
                    LayoutWorkbenchArea();
                    return;
                }
                if (ListaDescargas is null || ListaDescargas.IsDisposed)
                    return;
                if (navPanel is null || detailPanel is null)
                    return;
                if (TableLayoutPanel1 is null || StatusStrip1 is null)
                    return;
                int top = TableLayoutPanel1.Bottom + 2;
                if (quotaBannerPanel is not null && quotaBannerPanel.Visible)
                {
                    top += quotaBannerPanel.Height;
                }
                if (failureBannerPanel is not null && failureBannerPanel.Visible)
                {
                    top += failureBannerPanel.Height;
                }
                int bottom = StatusStrip1.Top - 4;
                int h = Math.Max(50, bottom - top);
                ListaDescargas.Top = top;
                ListaDescargas.Height = h;
                navPanel.Top = top;
                navPanel.Height = h;
                detailPanel.Top = top;
                detailPanel.Height = h;

                // 左栏加宽:列表 Left/Width 同步跟随,否则列表会压到加宽后的侧栏上。
                // 窄窗口(接近 MinimumSize 640)时按可用空间回退,保证中部列表不被挤没。
                ApplyNavRailWidth();

                // 左栏纵向分段定位(总览/快捷入口贴在过滤器下方,不随窗口高度拉伸)
                LayoutNavRail();

                // 右栏详情内部几何跟随宽度变化(标签/值两列按客户区宽度算)
                if (detailContentPanel is not null && !detailContentPanel.IsDisposed && detailContentPanel.Visible)
                {
                    LayoutDetailContent();
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("LayoutDownloadArea failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>左栏宽度单一来源。保证最小宽度下中部列表仍有可用空间(≥320px)。</summary>
        private void ApplyNavRailWidth()
        {
            try
            {
                if (navPanel is null || ListaDescargas is null)
                    return;
                int desired = NavRailWidth;
                // 右侧 detailPanel 与各边距固定占用约 210px(见 Designer 的 x 布局),留 320px 给列表。
                int maxAllowed = ClientSize.Width - 210 - 320;
                if (maxAllowed < 130)
                    maxAllowed = 130;
                int finalWidth = Math.Min(desired, maxAllowed);

                if (navPanel.Width != finalWidth)
                    navPanel.Width = finalWidth;
                int listLeft = navPanel.Left + finalWidth + 6;
                if (ListaDescargas.Left != listLeft)
                    ListaDescargas.Left = listLeft;
                ListaDescargas.Width = Math.Max(50, detailPanel.Left - 6 - listLeft);
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ApplyNavRailWidth failed: " + Log.SafeException(ex));
            }
        }

        private void InitDownloadAreaLayout()
        {
            try
            {
                // 去掉 Bottom 锚点:高度只由 LayoutDownloadArea 决定,Anchor 不再插手 Height,从根上杜绝漂移。
                ListaDescargas.Anchor = (System.Windows.Forms.AnchorStyles)Conversions.ToInteger(AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
                navPanel.Anchor = (System.Windows.Forms.AnchorStyles)Conversions.ToInteger(AnchorStyles.Top | AnchorStyles.Left);
                detailPanel.Anchor = (System.Windows.Forms.AnchorStyles)Conversions.ToInteger(AnchorStyles.Top | AnchorStyles.Right);
            }
            catch (Exception ex)
            {
                Log.WriteDebug("InitDownloadAreaLayout anchor failed: " + Log.SafeException(ex));
            }
            LayoutDownloadArea();
        }

        private void ListaDescargas_SelectionChanged(object sender, EventArgs e)
        {
            if (workbenchReady)
                LayoutDownloadArea();
            UpdateDetailPanel();
        }

        private void UpdateDetailPanel()
        {
            try
            {
                if (workbenchReady && !detailPanel.Visible)
                    return;
                IDescarga sel = null;
                if (ListaDescargas is not null && ListaDescargas.SelectedObjects is not null && ListaDescargas.SelectedObjects.Count > 0)
                {
                    sel = ListaDescargas.SelectedObjects[0] as IDescarga;
                }

                if (sel is null)
                {
                    ShowDetailEmptyState();
                }
                else
                {
                    UpdateDetailContent(sel);
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("UpdateDetailPanel failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>状态本地化文本(ColEstado AspectGetter 的只读镜像,供详情面板复用;改键时两处同步,热路径本身不动)。</summary>
        private static string EstadoDisplayText(Estado st)
        {
            switch (st)
            {
                case Estado.EnCola:
                    return Language.GetText("In queue");
                case Estado.CreandoLocal:
                    return Language.GetText("Creating files");
                case Estado.Verificando:
                    return Language.GetText("Verifying");
                case Estado.Erroneo:
                    return Language.GetText("Error capital leters");
                case Estado.Pausado:
                    return Language.GetText("Paused");
                case Estado.Descomprimiendo:
                    return Language.GetText("Extracting");
                case Estado.EsperandoVolumenes:
                    return Language.GetText("Waiting for volumes");
                case Estado.Descargando:
                    return Language.GetText("Downloading");
                case Estado.ComprobandoMD5:
                    return Language.GetText("Hashing MD5");
                case Estado.Completado:
                    return Language.GetText("Completed");
                default:
                    return "---";
            }
        }
        #endregion

        #region Gestion lista paquetes y descargas


        /// <summary>
    /// Agregamos paquetes desde la pantalla "Addlinks" o desde la carga inicial
    /// </summary>
    /// <param name="Paquete"></param>
    /// <remarks></remarks>
        internal void AgregarPaquete(Paquete Paquete, bool AgregadoDesdeServidorWeb, bool saveInBackground = false)
        {

            lock (Mutex.ListaDescargas)
            {
                ListaPaquetes.Add(Paquete);
                if (string.IsNullOrEmpty(Paquete.Nombre))
                {
                    Paquete.Nombre = Language.GetText("Package") + " #" + ListaPaquetes.Count.ToString();
                }
            }
            Log.WriteError("Package added: " + Paquete.Nombre);

            ReordenarPrioridadPaquetes(true);

            if (saveInBackground)
            {
                SaveImportedQueueInBackground();
            }
            else
            {
                GuardarFicheroDescargas();
            }
            // Small packages expand immediately. Large imports stay collapsed to avoid
            // building thousands of visible tree rows on the confirmation path.
            if (Paquete.ListaFicheros is null || Paquete.ListaFicheros.Count <= 500)
                ExpandPaquete(Paquete);
            // P0-4 UI:任务入库后给一次非打断确认(之前只有日志,用户不知道"加上没")。
            // Web 推送且主窗最小化时不弹(用户不在跟前,状态栏/托盘已有其它反馈位)。
            if (!(AgregadoDesdeServidorWeb && WindowState == FormWindowState.Minimized))
            {
                ToastForm.ShowToast(this, Language.GetText("Toast_PackageAdded").Replace("%N%", Paquete.Nombre));
            }
            if (!AgregadoDesdeServidorWeb)
                RestaurarVentana();

        }

        internal bool IsPackageQueued(Paquete package)
        {
            lock (Mutex.ListaDescargas)
                return ListaPaquetes is not null && ListaPaquetes.Contains(package);
        }

        // P-13: batch imports used to fire one Task (and one full-queue serialization)
        // per package. Coalesce bursts into a single background save; a request that
        // arrives while a save is running schedules exactly one more pass.
        private int _importSavePending = 0;
        private int _importSaveRunning = 0;

        internal void SaveImportedQueueInBackground()
        {
            System.Threading.Interlocked.Exchange(ref _importSavePending, 1);
            if (System.Threading.Interlocked.CompareExchange(ref _importSaveRunning, 1, 0) != 0)
                return;
            Task.Run(() =>
                {
                    try
                    {
                        do
                        {
                            System.Threading.Interlocked.Exchange(ref _importSavePending, 0);
                            try
                            {
                                GuardarFicheroDescargas();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteError("Saving imported package failed: " + Log.SafeException(ex));
                            }
                            // Small coalescing window so consecutive imports share one pass.
                            System.Threading.Thread.Sleep(150);
                        }
                        while (System.Threading.Interlocked.Exchange(ref _importSavePending, 0) == 1);
                    }
                    finally
                    {
                        // Clear the running flag FIRST, then re-read pending without consuming
                        // it. A request arriving in the tiny window between the loop's last
                        // check and this point still sees running=0 and starts a new pass;
                        // re-entering through the public method keeps the invariant intact.
                        // Reading (CompareExchange with 0,0) instead of consuming the flag
                        // avoids losing a request that lands after the read.
                        System.Threading.Interlocked.Exchange(ref _importSaveRunning, 0);
                        if (System.Threading.Interlocked.CompareExchange(ref _importSavePending, 0, 0) == 1)
                        {
                            try
                            {
                                SaveImportedQueueInBackground();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteError("Restarting coalesced import save failed: " + Log.SafeException(ex));
                            }
                        }
                    }
                });
        }

        /// <summary>在 UI 线程展开指定包(Web 推送走后台线程,必须编组;关闭中直接跳过)。</summary>
        private void ExpandPaquete(Paquete Paquete)
        {
            try
            {
                if (Paquete is null || IsDisposed || !IsHandleCreated)
                    return;
                if (ListaDescargas.InvokeRequired)
                {
                    Invoke(new Action<Paquete>(ExpandPaquete), Paquete);
                    return;
                }
                if (IsDisposed || ListaDescargas.IsDisposed)
                    return;
                ListaDescargas.Expand(Paquete);
            }
            catch (Exception ex)
            {
                Log.WriteError("ExpandPaquete failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>
    /// Guarda en disco el listado de paquetes y descargas.
    /// P-01: only a shallow reference copy happens under the global queue
    /// lock. All serialization, DPAPI/AES work and disk IO now run outside it, so a
    /// 5000-file save no longer blocks the 430ms refresh loop and download workers.
    /// A structure signature taken before/after the write detects additions or
    /// removals during the write and schedules a corrective save, preventing a
    /// stale queue from becoming the persisted state.</summary>
        private static long QueueStructureSignature(List<Paquete> packages)
        {
            if (packages is null)
                return 0L;
            // Final review: sig = sig * 31 + count overflows Long after ~13 packages
            // under VB integer-overflow checks. That exception escaped into
            // _bgwActualizadorDatosDisco and permanently stopped the disk-save worker,
            // so no queue or config change was ever persisted again. Use only Xor and
            // shifts (bitwise ops are never overflow-checked) while keeping the
            // position-sensitive change detection.
            long sig = packages.Count;
            int shift = 0;
            foreach (Paquete p in packages)
            {
                long count = p.ListaFicheros is null ? 0L : p.ListaFicheros.Count;
                sig = sig ^ count << shift;
                shift = (shift + 7) % 32;
            }
            return sig;
        }

        private void GuardarFicheroDescargas()
        {
            if (System.Threading.Volatile.Read(ref _startupQueueLoading))
                return;
            List<Paquete> snapshot;
            lock (Mutex.ListaDescargas)
            {
                if (ListaPaquetes is null)
                    return;
                snapshot = new List<Paquete>(ListaPaquetes);
            }
            long before = QueueStructureSignature(snapshot);

            Paquete.GuardarEnFichero(snapshot);
            UltimoGuardadoFichero = DateTime.Now;

            long after;
            lock (Mutex.ListaDescargas)
                after = QueueStructureSignature(ListaPaquetes);
            if (after != before)
                PeticionGuardadoFichero = DateTime.Now;
        }


        #endregion

        #region Background Workers


        /// <summary>
    /// Marshals an error dialog to the UI thread. BackgroundWorker.DoWork handlers run on
    /// thread-pool threads; calling MsgBox directly can throw InvalidOperationException
    /// if the form is closing/closed, and produces a parentless dialog. This helper
    /// checks IsDisposed/IsHandleCreated and uses Invoke; if marshalling is impossible
    /// (form gone or InvokeRequired check itself fails) the error is logged only.
    /// </summary>
        private void SafeShowError(string message)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    Log.WriteError("SafeShowError: form not available; message was: " + message);
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(new Action<string>(SafeShowError), message);
                    return;
                }
                MessageBox.Show(this, message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (InvalidOperationException ex)
            {
                Log.WriteError("SafeShowError: could not marshal to UI thread: " + ex.ToString() + " (original message: " + message + ")");
            }
            catch (Exception ex)
            {
                Log.WriteError("SafeShowError: unexpected failure: " + ex.ToString() + " (original message: " + message + ")");
            }
        }

        private void _bgwComprobarMaxConexiones_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            try
            {
                Log.WriteWarning("Starting worker _bgwComprobarMaxConexiones");
                while (!worker.CancellationPending)
                {

                    // Comprobamos conexiones máximas
                    if (DateTime.Now >= ProximaComprobacionMaxConexiones)
                    {
                        // Log.WriteWarning("Checking max connection number")

                        // 遗留回退：内部配置 VERSION_PERIODO_REFRESCO_SEG（默认 3600）；用户配置
                        // UpdateCheckIntervalHours 在下面覆盖。保留原回退避免破坏旧用户/测试夹具。
                        int SegundosProximaActualizacion = 3600;
                        if (!int.TryParse(InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_PERIODO_REFRESCO_SEG"), out SegundosProximaActualizacion))

                        {
                            SegundosProximaActualizacion = 3600;
                        }

                        // Aprovechamos para ver si hay una nueva versión

                        // 6:周期优先用用户可配置值（UpdateCheckIntervalHours，默认 1h）；内部配置
                        // VERSION_PERIODO_REFRESCO_SEG 仅作为遗留回退，0 视为关闭定时检查（仅手动）。
                        bool UseUserInterval = Config.CheckUpdates && !Config.UpdateManualOnly;
                        int CheckIntervalSec = SegundosProximaActualizacion;
                        if (UseUserInterval && Config.UpdateCheckIntervalHours > 0)
                        {
                            CheckIntervalSec = Math.Max(60, Config.UpdateCheckIntervalHours * 3600);
                        }
                        if (UseUserInterval)
                        {
                            ProximaComprobacionMaxConexiones = DateTime.Now.AddSeconds(CheckIntervalSec);
                        }
                        else if (Config.CheckUpdates && Config.UpdateManualOnly)
                        {
                            // 仅手动：不倒计时，下一轮由用户点菜单触发；这里把下次检查推到明天避免空转。
                            ProximaComprobacionMaxConexiones = DateTime.Now.AddDays(1d);
                        }

                        string url = null;
                        string version = null;
                        if (UseUserInterval)
                        {
                            // Network I/O remains on the worker and outside the connection lock.
                            Updater.ComprobarVersionMegadownloader(ref url, ref version);
                            Log.WriteWarning("Version checked; next check in " + CheckIntervalSec + " seconds");
                        }
                        if (!worker.CancellationPending && !Cerrando && IsHandleCreated)
                        {
                            BeginInvoke(new Action<string, string>(ApplyUpdateCheckResult), url, version);
                        }
                    }

                    // Forced GC and working-set trimming were removed: they frozen the UI
                    // for seconds on large queues. The runtime manages the heap on its own.

                    // Flush the log buffer.
                    Log.Flush(false);

                    System.Threading.Thread.Sleep(1000);
                }

                Log.WriteWarning("Stopping worker _bgwComprobarMaxConexiones");
            }
            catch (Exception ex)
            {
                Log.WriteError("Error in worker _bgwComprobarMaxConexiones: " + ex.ToString());
                SafeShowError(ex.Message); // 完整堆栈已写入上一行日志,给用户只看消息
            }
            finally
            {
                _bgwComprobarMaxConexionesCompleted = true;
            }
        }

        private void _bgwActualizadorDatosDisco_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            try
            {

                Log.WriteWarning("Starting worker _bgwActualizadorDatosDisco");
                // 验信息并行泵：在途 HTTP 可被 Abort 打断（SendJSON ct 穿透），退出时统一取消。
                var verifyCts = new System.Threading.CancellationTokenSource();
                try
                {
                    while (!worker.CancellationPending)
                    {

                        int TiempoDormir = 250;

                        // v2.5 beta: 配额期内不做新文件信息校验(每次校验=一次 API 调用,会延长惩罚窗口)。
                        bool quotaHoldVerify = MegaQuotaManager.IsQuarantined();
                        if (quotaHoldVerify)
                            TiempoDormir = 5000;

                        if (!NecesitaCambiarUsuarioYPassword && !quotaHoldVerify)
                        {
                            // 原来单 worker 串行逐个验：几百文件的文件夹 × 每次 API 往返 = 十几分钟"转圈"。
                            // 现跨包最多 4 并发；同包靠投递序号保命名确定性（与串行时代"首个验完命名"一致）。
                            PumpVerifyQueue(verifyCts.Token);
                            if (_verifyInflight.Count > 0)
                                TiempoDormir = Math.Min(TiempoDormir, 100);
                        }

                        // Miramos si hay que guardar el fichero de downloads
                        // Se guarda cada 5 segundos siempre O
                        // Si al menos han pasado 400ms sin peticiones de guardado
                        // (si un proceso pide 10 veces seguidas que se guarde, solo se guardará una vez,
                        // no 10 veces, lo cual es innecesario)
                        // 落盘不受熔断/凭据待换影响(此前整段包在 If 内,60min→2h→6h 的熔断窗口里
                        // 队列与配置完全不落盘,窗口内崩溃/断电/强杀会丢 FailedByQuota 标记与
                        // 已 fsync 的分块进度,重启后重复下载再耗配额)。只有验文件信息要让路。

                        if (UltimoGuardadoFichero.AddSeconds(5d) < DateTime.Now | UltimoGuardadoFichero < PeticionGuardadoFichero & PeticionGuardadoFichero.AddMilliseconds(400d) < DateTime.Now)

                        {

                            // One failed save must not terminate the worker: its outer
                            // Catch ends the While loop, so every later queue/config
                            // change would silently stop being persisted until restart.
                            try
                            {
                                GuardarFicheroDescargas();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteError("Saving the download list failed (will retry): " + Log.SafeException(ex));
                            }

                        }

                        if (UltimoGuardadoConfig.AddSeconds(5d) < DateTime.Now | UltimoGuardadoConfig < PeticionGuardadoConfig & PeticionGuardadoConfig.AddMilliseconds(400d) < DateTime.Now)

                        {

                            // Same worker-lifetime guard as the download list above.
                            try
                            {
                                Config.GuardarXML(false);
                                UltimoGuardadoConfig = DateTime.Now;
                            }
                            catch (Exception ex)
                            {
                                Log.WriteError("Saving the configuration failed (will retry): " + Log.SafeException(ex));
                            }

                        }
                        System.Threading.Thread.Sleep(TiempoDormir);
                    }
                }
                finally
                {
                    // 退出时打断在途校验 HTTP（Abort 穿透），最多等 5 秒，不卡关闭流程
                    try
                    {
                        verifyCts.Cancel();
                    }
                    catch
                    {
                    }
                    try
                    {
                        var pending = new List<Task>();
                        foreach (VerifyJob job in _verifyInflight.Values)
                        {
                            if (job.Task is not null)
                                pending.Add(job.Task);
                        }
                        if (pending.Count > 0)
                            Task.WaitAll(pending.ToArray(), 5000);
                    }
                    catch
                    {
                    }
                    _verifyInflight.Clear();
                    try
                    {
                        verifyCts.Dispose();
                    }
                    catch
                    {
                    }
                }
                Log.WriteWarning("Stopping worker _bgwActualizadorDatosDisco");
            }
            catch (Exception ex)
            {
                Log.WriteError("Error in worker _bgwActualizadorDatosDisco: " + ex.ToString());
                SafeShowError(ex.Message); // 完整堆栈已写入上一行日志,给用户只看消息
            }
            finally
            {
                _bgwActualizadorDatosDiscoCompleted = true;
            }
        }

        private const int VerifyMaxParallel = 4;

        private class VerifyJob
        {
            public Fichero Fic;
            public Paquete Paq;
            public long Seq;
            public Conexion.TipoError Err = Conexion.TipoError.SinErrores;
            public Exception Fault = null;
            public Task Task = null;
        }

        private Dictionary<Fichero, VerifyJob> _verifyInflight = new Dictionary<Fichero, VerifyJob>();
        private long _verifySeq = 0L;

        /// <summary>
    /// 验信息泵：挑新文件投递到池线程，收割已完成并串行后处理。
    /// 投递按列表顺序编号；包命名只认"同包最早投递且无更早在途者"的完成，
    /// 与串行时代"首个验完命名"结果一致。字典只在 worker 线程碰，无需加锁。
    /// </summary>
        private void PumpVerifyQueue(System.Threading.CancellationToken ct)
        {
            try
            {
                if (_verifyInflight.Count > 0)
                {
                    foreach (VerifyJob job in new List<VerifyJob>(_verifyInflight.Values))
                    {
                        if (job.Task is not null && job.Task.IsCompleted)
                        {
                            _verifyInflight.Remove(job.Fic);
                            ProcessVerifyResult(job);
                        }
                    }
                }
                if (_verifyInflight.Count >= VerifyMaxParallel)
                    return;
                VerifyJob picked = null;
                lock (Mutex.ListaDescargas)
                {
                    foreach (Paquete paq in ListaPaquetes)
                    {
                        foreach (Fichero fic in paq.ListaFicheros)
                        {
                            if (fic.DescargaProcesada == false && fic.DescargaEstado() != Estado.Erroneo && !_verifyInflight.ContainsKey(fic))
                            {
                                picked = new VerifyJob();
                                picked.Fic = fic;
                                picked.Paq = paq;
                                picked.Seq = _verifySeq;
                                _verifySeq += 1L;
                                _verifyInflight[fic] = picked;
                                break;
                            }
                        }
                        if (picked is not null)
                            break;
                    }
                }
                if (picked is null)
                    return;
                Log.WriteInfo("Updating file info " + picked.Fic.FileID);
                var jobRef = picked;
                picked.Task = Task.Run(() =>
                    {
                        try
                        {
                            var e = Conexion.TipoError.SinErrores;
                            jobRef.Fic.ActualizarInformacionFichero(Config, ref e, false, ct);
                            jobRef.Err = e;
                        }
                        catch (Exception ex)
                        {
                            jobRef.Fault = ex;
                        }
                    });
            }
            catch (Exception ex)
            {
                Log.WriteError("PumpVerifyQueue failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>单个验完的后处理（worker 线程串行执行）：与原来单文件分支完全相同的语义。</summary>
        private void ProcessVerifyResult(VerifyJob job)
        {
            try
            {
                if (job.Fault is not null)
                {
                    if (job.Fault is OperationCanceledException)
                    {
                        Log.WriteDebug("Verify aborted for " + job.Fic.FileID);
                    }
                    else
                    {
                        Log.WriteError("Error retrieving file info " + job.Fic.FileID + ": " + Log.SafeException(job.Fault));
                        // 必须落到错误态：否则 DescargaProcesada仍为 False 且状态不是 Erroneo，
                        // PumpVerifyQueue 下一拍会再选中同一个文件，整个队列永久卡在"验证中"且无提示。
                        job.Fic.EstablecerError("The file could not be verified." + Constants.vbNewLine + " * File code: " + job.Fic.FileID + Constants.vbNewLine + " * Error type: Unexpected" + Constants.vbNewLine + " * Internal info: " + job.Fault.Message);


                    }
                    return;
                }
                var Err = job.Err;
                if (Err == Conexion.TipoError.UsuarioInvalido)
                {
                    // Si el usuario está mal no vamos a pedir lo mismo 1000 veces seguidas,
                    // abortamos hasta que se cambie el usuario
                    NecesitaCambiarUsuarioYPassword = true;
                    Log.WriteWarning("Error retrieving file info " + job.Fic.FileID + ": " + Err.ToString());
                }
                else if (Err == Conexion.TipoError.SinErrores)
                {
                    // Guardamos!!
                    PeticionGuardadoFichero = DateTime.Now;

                    bool canRename = true;
                    foreach (VerifyJob other in _verifyInflight.Values)
                    {
                        if (ReferenceEquals(other.Paq, job.Paq) && other.Seq < job.Seq)
                        {
                            canRename = false;
                            break;
                        }
                    }
                    if (job.Fic.DescargaProcesada && job.Paq.PendienteNombrePaquete && canRename)
                    {
                        // Issue #4: 多文件包(文件夹/批量)不得取首个文件名当包名建子目录,
                        // 否则"一个文件名变成文件夹,所有文件挤进去",且原相对子目录被拍平、留下空目录。
                        // 只有单文件包才保留历史行为(无名包按文件名命名)。
                        int filesInPack = 0;
                        try
                        {
                            filesInPack = job.Paq.SnapshotFicheros().Count;
                        }
                        catch
                        {
                            filesInPack = 0;
                        }
                        job.Paq.PendienteNombrePaquete = false;
                        if (filesInPack == 1)
                        {
                            string singleName = job.Fic.ObtenerNombreSinExtension();
                            if (string.IsNullOrEmpty(singleName))
                                singleName = Language.GetText("New package");
                            job.Paq.Nombre = singleName;
                            if (job.Paq.CrearSubdirectorio)
                            {
                                try
                                {
                                    job.Paq.RutaLocal = System.IO.Path.Combine(job.Paq.RutaLocal, job.Paq.Nombre);
                                    System.IO.Directory.CreateDirectory(job.Paq.RutaLocal);
                                    foreach (Fichero fic in job.Paq.SnapshotFicheros())
                                    {
                                        if (!fic.DescargaComenzada)
                                        {
                                            string rel = fic.RutaRelativa ?? string.Empty;
                                            if (string.IsNullOrEmpty(rel))
                                            {
                                                fic.RutaLocal = job.Paq.RutaLocal;
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    fic.RutaLocal = PathGuard.GetSafePathUnderRoot(job.Paq.RutaLocal, rel, allowRoot: true);
                                                    System.IO.Directory.CreateDirectory(fic.RutaLocal);
                                                }
                                                catch
                                                {
                                                    fic.RutaLocal = job.Paq.RutaLocal;
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.WriteError("Error while creating directory for package " + job.Paq.Nombre + ": " + ex.ToString());
                                    // 后台线程(DoWork)不能直接弹窗:无属主窗体会藏到主窗体后面,用户会以为卡死。
                                    // 走 SafeShowError 编组回 UI 线程显示
                                    SafeShowError("Error creating directory: " + ex.Message);
                                }
                            }
                        }
                        // 多文件包:包名不再跟随首个文件,文件保持 FinishAddPackage 时
                        // 已按 RutaRelativa 落好的位置,不再搬移/拍平。
                        else if (string.IsNullOrEmpty(job.Paq.Nombre))
                        {
                            try
                            {
                                job.Paq.Nombre = Language.GetText("New package");
                            }
                            catch
                            {
                                job.Paq.Nombre = "New package";
                            }
                        }
                    }
                }
                else
                {
                    Log.WriteWarning("Error retrieving file info " + job.Fic.FileID + ": " + Err.ToString());
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("ProcessVerifyResult failed: " + Log.SafeException(ex));
            }
        }

        private void _bgwActualizadorListaDescargas_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            var sw = new Stopwatch();
            string Flujo = "";
            ProcessResourceMonitor resourceMonitor = null;
            bool monitorFailureLogged = false;

            try
            {
                Log.WriteWarning("Starting worker _bgwActualizadorListaDescargas");
                var ramMegabytes = default(double);
                var cpuPercentage = default(double?);
                while (!worker.CancellationPending)
                {
                    // 单点故障防护:单次迭代异常只记日志继续循环,不再整条 worker 死亡
                    // (此前 Try 包在 While 外面,一次异常=列表刷新+状态栏+横幅+倒计时+自动恢复全停摆,且无重启)。
                    try
                    {

                        sw.Start();
                        Flujo = "Checking status" + Constants.vbNewLine;

                        // Cambio estado
                        if (EstadoAplicacion == TipoEstadoAplicacion.Descargando)
                        {
                            PonerFicherosADescargar();
                        }
                        else if (EstadoAplicacion == TipoEstadoAplicacion.Pausa)
                        {
                            PonerFicherosEnPausa();
                        }
                        else if (EstadoAplicacion == TipoEstadoAplicacion.Parado)
                        {
                            PararDescargaFicheros();
                        }

                        Flujo += "Calculating speed, state, etc" + Constants.vbNewLine;

                        // Calculo de velocidad, estado y demás datos
                        decimal VelocidadGlobal = 0m;

                        if (worker.CancellationPending)
                            break;
                        // 卡顿治理：先短锁拷贝包与文件引用（指针拷贝，O(包+文件)），逐文件的重活
                        // （Downloader 属性读取、ETA 字符串格式化，各自只短取细锁）在锁外执行；
                        // 最后短锁聚合计数。全局锁持有时间从"几百个文件的全程"降为两次浅拷贝+纯字段读取，
                        // 下载 worker 不再被 430ms 节拍挡在锁外。快照与实时列表最多差一个节拍，
                        // 计数类 UI 本就按节拍刷新，可接受。
                        var snapshot = new List<KeyValuePair<Paquete, List<Fichero>>>();
                        lock (Mutex.ListaDescargas)
                        {
                            foreach (Paquete paq in ListaPaquetes)
                                snapshot.Add(new KeyValuePair<Paquete, List<Fichero>>(paq, paq.SnapshotFicheros()));
                        }
                        foreach (KeyValuePair<Paquete, List<Fichero>> kv in snapshot)
                        {
                            foreach (Fichero fic in kv.Value)
                                fic.ActualizarDatosDescarga();
                            // P-03/P-18: package aggregation only walks its own files and reads
                            // plain fields; it does not need Mutex.ListaDescargas. Moving it out
                            // removes one full-table pass from inside the global lock.
                            kv.Key.ActualizarDatosDescarga();
                            VelocidadGlobal += kv.Key.DescargaVelocidadKBs();
                        }
                        lock (Mutex.ListaDescargas)
                        {
                            NumDescargasActivas = 0;
                            NumDescargasEnCola = 0;
                            NumDescargasErroneas = 0;
                            NumDescargasCompletadas = 0;
                            foreach (KeyValuePair<Paquete, List<Fichero>> kv in snapshot)
                            {
                                foreach (Fichero fic in kv.Value)
                                {
                                    if (fic.EstadoDescarga == Estado.Descargando | fic.EstadoDescarga == Estado.CreandoLocal | fic.EstadoDescarga == Estado.Verificando)

                                    {
                                        NumDescargasActivas += 1;
                                    }
                                    else if (fic.EstadoDescarga == Estado.EnCola | fic.EstadoDescarga == Estado.Pausado)
                                    {
                                        NumDescargasEnCola += 1;
                                    }
                                    else if (fic.EstadoDescarga == Estado.Erroneo)
                                    {
                                        NumDescargasErroneas += 1;
                                    }
                                    else if (fic.EstadoDescarga == Estado.Completado | fic.EstadoDescarga == Estado.ComprobandoMD5 | fic.EstadoDescarga == Estado.Descomprimiendo | fic.EstadoDescarga == Estado.EsperandoVolumenes)


                                    {
                                        NumDescargasCompletadas += 1;

                                    }
                                }
                            }
                        }

                        PublishQueueOverview(snapshot);
                        CheckDownloadNotifications(snapshot);

                        if (worker.CancellationPending)
                            break;

                        Flujo += "Updating download list" + Constants.vbNewLine;

                        RefreshListaDescargas(false);

                        VelocidadGlobalDescarga = VelocidadGlobal;
                        TextoIconoMinimizado(NumDescargasActivas + " " + Language.GetText("active downloads") + " - " + PintarVelocidadDescarga(VelocidadGlobal));


                        if (worker.CancellationPending)
                            break;

                        Flujo += "Checking if we have finished and we have to turn off the PC" + Constants.vbNewLine;

                        // Revisamos si hemos terminado y hay que apagar el PC
                        RevisarSiHayQueApagarPC();


                        Flujo += "Calculating data (speed, processor and RAM usage, etc)" + Constants.vbNewLine;
                        string EstadoTxt = Language.GetText("Stopped");
                        string VelocidadTxt = "";
                        switch (EstadoAplicacion)
                        {
                            case TipoEstadoAplicacion.Descargando:
                                EstadoTxt = Language.GetText("Downloading");
                                if (VelocidadGlobalDescarga.HasValue && VelocidadGlobalDescarga.Value > 0m)
                                {
                                    VelocidadTxt = " " + PintarVelocidadDescarga(VelocidadGlobalDescarga.Value);
                                }

                                break;
                            case TipoEstadoAplicacion.Pausa:
                                EstadoTxt = Language.GetText("Paused");
                                break;
                        }


                        // Calculo procesador y RAM
                        string RAMStr = "-";
                        string ProcesadorStr = "-";
                        if (System.Threading.Volatile.Read(ref _resourceMonitoringEnabled) != 0)
                        {
                            try
                            {
                                if (resourceMonitor is null)
                                    resourceMonitor = new ProcessResourceMonitor();
                                resourceMonitor.ReadSnapshot(ref ramMegabytes, ref cpuPercentage);
                                RAMStr = ramMegabytes.ToString("F2");
                                if (cpuPercentage.HasValue)
                                    ProcesadorStr = cpuPercentage.Value.ToString("F2");
                                monitorFailureLogged = false;
                            }
                            catch (Exception exMonitor)
                            {
                                // A statistics failure must not interrupt queue updates or spam each tick.
                                if (!monitorFailureLogged)
                                {
                                    Log.WriteWarning("Process resource monitoring failed: " + Log.SafeException(exMonitor));
                                    monitorFailureLogged = true;
                                }
                            }
                        }

                        Flujo += "Displaying data in status bar" + Constants.vbNewLine;

                        if (!Config.DownloadSchedule.AllowsDownload(DateTime.Now))
                            EstadoTxt = Language.GetText("Schedule_Waiting");
                        SetStatusBar(RAMStr + "MB", ProcesadorStr + "%", EstadoTxt, VelocidadTxt, Config.ConexionesPorFichero + "/" + Config.DescargasSimultaneas);

                        // 左栏任务总览:与状态栏同频(430ms)刷新,数据源一致,不额外起线程。
                        UpdateNavRailOverview();

                        UpdateQuotaUI();
                        RefreshFailureBanner();

                        if (worker.CancellationPending)
                            break;

                        // Dirty trick!!
                        Flujo += "Getting mega:// parameters" + Constants.vbNewLine;
                        ApplicationInstanceManager.GetParameters();

                        Flujo += "Sleeping" + Constants.vbNewLine;

                        // Final review: an idle-only 1s heartbeat delayed user-visible
                        // actions (start/resume, scheduling, UI state) by up to a second,
                        // because PonerFicherosADescargar runs at the top of this same tick.
                        // Keep the original 430ms cadence. P-03's real gain (moving package
                        // aggregation out of Mutex.ListaDescargas) is unaffected.
                        System.Threading.Thread.Sleep(430);

                        Flujo += "Finishing cycle" + Constants.vbNewLine;

                        sw.Stop();
                        sw.Reset();
                    }

                    catch (Exception exIter)
                    {
                        Log.WriteError("_bgwActualizadorListaDescargas iteration failed, continuing (" + Flujo.Replace(Constants.vbNewLine, " ").Trim() + "): " + Log.SafeException(exIter));
                        try
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                        catch
                        {
                        }
                    }

                }
                Log.WriteWarning("Stopping worker _bgwActualizadorListaDescargas");
            }
            catch (Exception ex)
            {
                Log.WriteError("Error in worker _bgwActualizadorListaDescargas: " + ex.ToString());
                SafeShowError(ex.Message); // 完整堆栈已写入上一行日志,给用户只看消息
            }
            finally
            {
                if (sw.ElapsedMilliseconds > 5000L)
                {
                    Log.WriteError("_bgwActualizadorListaDescargas was too slow (" + sw.ElapsedMilliseconds + " ms): " + Constants.vbNewLine + Flujo);
                }
                if (resourceMonitor is not null)
                    resourceMonitor.Dispose();
                _bgwActualizadorListaDescargasCompleted = true;
            }
        }




        /// <summary>
    /// 完成/失败跃迁检测（跑在 430ms 后台循环，无需加锁：所用字段仅本循环读写）。
    /// 包完成弹一次；文件失败按 tick 合并（单条/汇总），60 秒内已弹 ≥3 条则只记日志防刷屏。
    /// 任何异常只记 Debug，绝不能掀翻刷新循环。
    /// </summary>
        private void CheckDownloadNotifications(List<KeyValuePair<Paquete, List<Fichero>>> snapshot)
        {
            try
            {
                var now = DateTime.Now;
                while (_recentBalloonTimes.Count > 0 && now.Subtract(_recentBalloonTimes[0]).TotalSeconds > 60d)
                    _recentBalloonTimes.RemoveAt(0);

                var newStates = new Dictionary<Fichero, Estado>();
                var newFailures = new List<Fichero>();
                foreach (KeyValuePair<Paquete, List<Fichero>> kv in snapshot)
                {
                    foreach (Fichero fic in kv.Value)
                    {
                        var st = fic.EstadoDescarga;
                        newStates[fic] = st;
                        if (_lastFileEstado.ContainsKey(fic) && _lastFileEstado[fic] != Estado.Erroneo && st == Estado.Erroneo)
                        {
                            newFailures.Add(fic);
                        }
                    }
                }
                _lastFileEstado = newStates;

                var stillComplete = new Dictionary<Paquete, bool>();
                foreach (KeyValuePair<Paquete, List<Fichero>> kv in snapshot)
                {
                    if (kv.Value.Count == 0)
                        continue;
                    bool allDone = true;
                    foreach (Fichero fic in kv.Value)
                    {
                        if (fic.EstadoDescarga != Estado.Completado)
                        {
                            allDone = false;
                            break;
                        }
                    }
                    if (allDone)
                    {
                        stillComplete[kv.Key] = true;
                        if (!_notifiedPackages.ContainsKey(kv.Key))
                        {
                            _notifiedPackages[kv.Key] = true;
                            _recentBalloonTimes.Add(now);
                            ShowBalloonTip(Language.GetText("Notify_PackageDone_Title"), Language.GetText("Notify_PackageDone_Body").Replace("%N%", kv.Key.Nombre), ToolTipIcon.Info);
                        }
                    }
                }
                _notifiedPackages = stillComplete;

                if (newFailures.Count == 1 && _recentBalloonTimes.Count < 3)
                {
                    var fic = newFailures[0];
                    _recentBalloonTimes.Add(now);
                    ShowBalloonTip(Language.GetText("Notify_Failed_Title"), fic.NombreFichero + Constants.vbCrLf + DownloadAdvice.Describe(fic.DescripcionError, fic.FailedByQuota, fic.EsErrorPermanente), ToolTipIcon.Warning);
                }
                else if (newFailures.Count > 1 && _recentBalloonTimes.Count < 3)
                {
                    _recentBalloonTimes.Add(now);
                    ShowBalloonTip(Language.GetText("Notify_Failed_Title"), Language.GetText("Notify_FailedMany_Body").Replace("%N%", newFailures.Count.ToString()), ToolTipIcon.Warning);
                }
                else if (newFailures.Count > 0)
                {
                    Log.WriteWarning(newFailures.Count.ToString() + " new download failures suppressed from balloon (flood guard).");
                }
            }
            catch (Exception ex)
            {
                Log.WriteDebug("CheckDownloadNotifications failed: " + Log.SafeException(ex));
            }
        }

        /// <summary>托盘气泡（调用方可为后台线程，内部编组到 UI）。超长截断，窗体销毁中直接丢弃。</summary>
        private void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (IsDisposed || Disposing || !IsHandleCreated)
                    return;
                if (InvokeRequired)
                {
                    this.BeginInvoke(new Action<string, string, ToolTipIcon>(ShowBalloonTip), title, text, icon);
                    return;
                }
                if (string.IsNullOrEmpty(text))
                    return;
                if (text.Length > 250)
                    text = text.Substring(0, 250) + "...";
                IconoMinimizado.ShowBalloonTip(10000, title, text, icon);
            }
            catch (Exception ex)
            {
                Log.WriteDebug("ShowBalloonTip failed: " + Log.SafeException(ex));
            }
        }

        private void IconoMinimizado_BalloonTipClicked(object sender, EventArgs e)
        {
            try
            {
                RestaurarVentana();
            }
            catch (Exception ex)
            {
                Log.WriteDebug("BalloonTipClicked restore failed: " + Log.SafeException(ex));
            }
        }

        private void _bgwActualizadorListaDescargas_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _bgwActualizadorListaDescargasCompleted = true;
            // 看门狗:非关闭流程中的 worker 结束=异常穿透(内层已尽力自保),3 秒后自救重启。
            // 关闭时 Cerrando/_ForzarCierre/Disposed 必有一真,不重启,不卡退出流程。
            try
            {
                if (e.Cancelled)
                    return;
                if (Cerrando || _ForzarCierre || IsDisposed || Disposing || !IsHandleCreated)
                    return;
                Log.WriteError("_bgwActualizadorListaDescargas ended unexpectedly; restarting in 3s.");
                Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(3000);
                        try
                        {
                            if (Cerrando || _ForzarCierre || IsDisposed || Disposing || !IsHandleCreated)
                                return;
                            if (_bgwActualizadorListaDescargas.IsBusy)
                                return;
                            _bgwActualizadorListaDescargasCompleted = false;
                            _bgwActualizadorListaDescargas.RunWorkerAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("_bgwActualizadorListaDescargas restart failed: " + Log.SafeException(ex));
                        }
                    });
            }
            catch (Exception ex)
            {
                Log.WriteError("_bgwActualizadorListaDescargas watchdog failed: " + Log.SafeException(ex));
            }
        }
        private void _bgwActualizadorDatosDisco_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _bgwActualizadorDatosDiscoCompleted = true;
        }
        private void _bgwComprobarMaxConexiones_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _bgwComprobarMaxConexionesCompleted = true;
        }

        private void _bgwDescompresor_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // P1-2:解压 worker 看门狗。该 worker 是常驻循环(While Not CancellationPending),
            // 正常只在关闭(CancelAsync)时结束;非关闭流程中结束 = 异常穿透 = worker 已死,
            // 此后整个解压队列无人消费,直到重启进程。此前此处理只置标志不重启。
            // 3 秒后自救重启,与 _bgwActualizadorListaDescargas 看门狗同构。
            // 关闭时 Cerrando/_ForzarCierre/Disposed 必有一真,不重启,不卡退出流程。
            try
            {
                if (e.Cancelled)
                    return;
                if (Cerrando || _ForzarCierre || IsDisposed || Disposing || !IsHandleCreated)
                    return;

                // P1-2:重启防风暴。上一轮存活不足 30s 视为崩溃,连续 5 次放弃重启。
                if (_descompresorLastStartUtc != DateTime.MinValue && (DateTime.UtcNow - _descompresorLastStartUtc).TotalSeconds < 30d)
                {
                    _descompresorCrashCount += 1;
                    if (_descompresorCrashCount >= 5)
                    {
                        Log.WriteError("_bgwDescompresor crashed " + _descompresorCrashCount.ToString() + " times in a row; giving up on auto-restart. The extraction queue stays stalled until the app is restarted.");
                        return;
                    }
                }
                else
                {
                    _descompresorCrashCount = 0;
                }

                Log.WriteError("_bgwDescompresor ended unexpectedly; restarting in 3s.");
                Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(3000);
                        try
                        {
                            if (Cerrando || _ForzarCierre || IsDisposed || Disposing || !IsHandleCreated)
                                return;
                            if (_bgwDescompresor.IsBusy)
                                return;
                            _descompresorLastStartUtc = DateTime.UtcNow;
                            _bgwDescompresor.RunWorkerAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("_bgwDescompresor restart failed: " + Log.SafeException(ex));
                        }
                    });
            }
            catch (Exception ex)
            {
                Log.WriteError("_bgwDescompresor watchdog failed: " + Log.SafeException(ex));
            }
        }
        // P1:配额到期唤醒必须独立于自愈开关。WakeQuotaFailedItems 的唯一旧出口藏在下面
        // If ResetearErrores 分支里,用户关掉"失败自愈"后横幅消失、倒计时归零,失败项却永久停在
        // Erroneo——发布说明承诺的行为静默失效。此处熔断解除边沿直接复用 WakeQuotaFailedItems。
        private bool _PonerFicherosADescargar_quotaHoldPrev = false;

        private void PonerFicherosADescargar()
        {
            if (!ScheduleAllowsStart())
                return;
            var ColaDescarga = new List<Fichero>(); // Lista de ficheros en cola
            var ColaDescargaPausa = new List<Fichero>(); // Lista de ficheros pausados en cola
            var ColaReseteo = new List<Fichero>(); // Lista de ficheros a resetear
            int NumFicherosDescargando = 0;
            int NumConexionesAbiertas = 0;

            int configConexionesPorFichero = Config.ConexionesPorFichero;
            bool ResetearErrores = Config.ResetearErrores;
            int ResetearErroresPeriodo = Config.ResetearErroresPeriodoMinutos;
            // v2.5 beta: 配额熔断状态。配额期内不唤醒配额失败项、不开新任务；到期后配额失败项立即唤醒。
            bool quotaHold = MegaQuotaManager.IsQuarantined();
            try
            {
                if (_PonerFicherosADescargar_quotaHoldPrev && !quotaHold)
                    WakeQuotaFailedItems();
            }
            catch (Exception ex)
            {
                Log.WriteError("Quota auto-wake failed: " + Log.SafeException(ex));
            }
            _PonerFicherosADescargar_quotaHoldPrev = quotaHold;

            // Reset de descargas erroneas
            // 突破模式下配额失败项不依赖自愈开关(否则关掉自愈=突破模式静默失效);
            // 普通失败仍只跟自愈开关,用户关掉就不再碰。
            if (ResetearErrores || MegaQuotaManager.IsBreakthroughMode())
            {
                lock (Mutex.ListaDescargas)
                {
                    foreach (Paquete paq in ListaPaquetes)
                    {
                        foreach (Fichero Fichero in paq.ListaFicheros)
                        {
                            if (Fichero.DescargaEstado() == Estado.Erroneo && !Fichero.EsErrorPermanente)
                            {
                                if (Fichero.FailedByQuota)
                                {
                                    if (MegaQuotaManager.IsBreakthroughMode())
                                    {
                                        // 突破模式:无熔断,按普通失败的自愈节拍复活(旧版行为)。
                                        // 禁止紧循环:此前无时间门,每调度节拍复活一次,锤得比旧版凶数百倍。
                                        if (!Fichero.FechaUltimoError.HasValue || Fichero.FechaUltimoError.Value.AddMinutes(ResetearErroresPeriodo) < DateTime.Now)
                                            ColaReseteo.Add(Fichero);
                                    }
                                    else if (!quotaHold)
                                    {
                                        ColaReseteo.Add(Fichero);
                                    }
                                }
                                else if (ResetearErrores && (!Fichero.FechaUltimoError.HasValue || Fichero.FechaUltimoError.Value.AddMinutes(ResetearErroresPeriodo) < DateTime.Now))
                                {
                                    ColaReseteo.Add(Fichero);
                                }
                            }
                        }
                    }
                }
                foreach (var Fichero in ColaReseteo)
                {
                    // P0-1:自愈保留断点续传,不删 .part/不清分块(否则大文件超时后无限重下)。
                    Fichero.ResetearDescarga(true);
                    Fichero.SetDescargaEstado = Estado.EnCola;
                    Log.WriteInfo("Reseting file " + Fichero.FileID + " automatically.");
                }
            }


            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero Fichero in paq.ListaFicheros)
                    {

                        if (Fichero.DescargaEstado() == Estado.EnCola & Fichero.DescargaProcesada && !Fichero.PausaIndividual && !quotaHold)
                        {
                            ColaDescarga.Add(Fichero);
                        }
                        else if (Fichero.DescargaEstado() == Estado.Pausado && !Fichero.PausaIndividual && !quotaHold)
                        {
                            ColaDescargaPausa.Add(Fichero);
                        }
                        else if (Fichero.DescargaEstado() == Estado.Descargando | Fichero.DescargaEstado() == Estado.Pausado & Fichero.PausaIndividual)
                        {
                            // Las pausas individuales significa que el resto de ficheros está descargando, pero el fichero pausado no, por tanto no abrimos nuevas conexiones
                            NumFicherosDescargando += 1;
                            int NumConAbiertas = Fichero.NumeroConexionesAbiertas;
                            if (NumConAbiertas == 0)
                            {
                                NumConAbiertas = configConexionesPorFichero; // Todavía no está descargando pero empezará en breve
                            }
                            NumConexionesAbiertas += NumConAbiertas;
                        }
                        else if (Fichero.DescargaEstado() == Estado.CreandoLocal | Fichero.DescargaEstado() == Estado.Verificando)
                        {
                            NumFicherosDescargando += 1;
                            NumConexionesAbiertas += configConexionesPorFichero;
                        }

                    }
                }
            }

            int ConexionesPorAbrir;
            lock (Mutex.NumeroConexionesMaxima)
                ConexionesPorAbrir = NumeroConexionesMaxima - NumConexionesAbiertas;
            int FicherosPorAbrir = Config.DescargasSimultaneas - NumFicherosDescargando;

            // If (ColaDescargaPausa.Count > 0 Or ColaDescarga.Count > 0) And ConexionesPorAbrir > 0 Then
            // Log.WriteInfo("We have " & ConexionesPorAbrir & " connections left")
            // End If

            // Mutex.ListaDescargas.WaitOne()
            try
            {
                foreach (Fichero Fichero in ColaDescargaPausa) // Primero reanudamos los ficheros en pausa y luego el resto
                {
                    if (ConexionesPorAbrir > 0 & FicherosPorAbrir > 0)
                    {
                        Log.WriteInfo("Continuing paused file download " + Fichero.NombreFichero);
                        Fichero.Resume();
                        ConexionesPorAbrir -= Fichero.NumeroConexionesAbiertas;
                        FicherosPorAbrir -= 1;
                    }
                }
                foreach (Fichero Fichero in ColaDescarga)
                {
                    if (ConexionesPorAbrir > 0 & FicherosPorAbrir > 0)
                    {
                        int ConexionesParaEstaDescarga = configConexionesPorFichero;
                        if (ConexionesPorAbrir < configConexionesPorFichero)
                            ConexionesParaEstaDescarga = ConexionesPorAbrir;
                        Log.WriteInfo("Starting file download " + Fichero.NombreFichero);
                        Fichero.Start(ref Config, ConexionesParaEstaDescarga);
                        ConexionesPorAbrir -= ConexionesParaEstaDescarga;
                        FicherosPorAbrir -= 1;
                    }
                }
            }
            finally
            {
                // Mutex.ListaDescargas.ReleaseMutex()
            }
        }

        private void ForzarDescarga(Fichero Fichero)
        {
            if (!ScheduleAllowsStart(true))
                return;
            Log.WriteInfo("Forcing download" + Fichero.NombreFichero);
            lock (Mutex.ListaDescargas)
            {
                if (Fichero.DescargaEstado() == Estado.Pausado)
                {
                    Fichero.PausaIndividual = false;
                    Fichero.DescargaIndividual = true;
                    Fichero.Resume();
                }
                else if (Fichero.DescargaEstado() == Estado.EnCola & Fichero.DescargaProcesada)
                {
                    Fichero.PausaIndividual = false;
                    Fichero.DescargaIndividual = true;
                    Fichero.Start(ref Config, Config.ConexionesPorFichero);
                }
                else if (Fichero.DescargaEstado() == Estado.Erroneo && !Fichero.EsErrorPermanente)
                {
                    // ⑧:红字强制=手动全量重置后按单个强制起(删 .part 从头来,顺带修尺寸不符类错误)。
                    Fichero.ResetearDescarga();
                    Fichero.PausaIndividual = false;
                    Fichero.SetDescargaEstado = Estado.EnCola;
                    Fichero.DescargaIndividual = true;
                    Fichero.Start(ref Config, Config.ConexionesPorFichero);
                }
            }
        }
        private void QuitarDescargasIndividuales()
        {
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero Fichero in paq.ListaFicheros)
                        Fichero.DescargaIndividual = false;
                }
            }
        }

        private void PonerFicherosEnPausa()
        {
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero Fichero in paq.ListaFicheros)
                    {
                        if (Fichero.DescargaEstado() == Estado.Descargando & !Fichero.DescargaIndividual | Fichero.DescargaEstado() == Estado.CreandoLocal)
                        {
                            Log.WriteInfo("Pausing file " + Fichero.NombreFichero);
                            Fichero.Pause();
                        }
                    }
                }
            }
        }
        private void QuitarPausasIndividuales()
        {
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero Fichero in paq.ListaFicheros)
                        Fichero.PausaIndividual = false;
                }
            }
        }
        private void PonerFicheroEnPausa(Fichero Fichero)
        {
            lock (Mutex.ListaDescargas)
            {
                if (Fichero.DescargaEstado() == Estado.Descargando | Fichero.DescargaEstado() == Estado.CreandoLocal)
                {
                    Log.WriteInfo("Pausing file " + Fichero.NombreFichero);
                    Fichero.PausaIndividual = true;
                    Fichero.Pause();
                    ThrottledStreamController.GetController().Abortar(Fichero.FileID);
                }
            }
        }

        private void PararDescargaFicheros()
        {
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero Fichero in paq.ListaFicheros)
                    {
                        if (Fichero.DescargaEstado() == Estado.Descargando & !Fichero.DescargaIndividual | Fichero.DescargaEstado() == Estado.Pausado | Fichero.DescargaEstado() == Estado.CreandoLocal)

                        {
                            Log.WriteInfo("Stopping file " + Fichero.NombreFichero);
                            Fichero.Stop();
                        }
                    }
                }
            }
        }

        private void EsperarParadaDescargasYWorkers()
        {
            bool TodosFinalizados = false;
            int TimeoutSeg = 15;
            int.TryParse(InternalConfiguration.ObtenerValueFromInternalConfig("TIMEOUT_CLOSE"), out TimeoutSeg);
            var Ini = DateTime.Now;
            while (!TodosFinalizados)
            {

                TodosFinalizados = true;
                lock (Mutex.ListaDescargas)
                {
                    foreach (Paquete paq in ListaPaquetes)
                    {
                        foreach (Fichero Fichero in paq.ListaFicheros)
                        {
                            // B2-⑩:关闭必须等瞬态(此前只等 Descargando/Pausado,CreandoLocal/
                            // Verificando/解压/MD5 回补中的任务会被撕裂存盘且关后复活)。
                            // P2-2:Pausado 不再入等待集——它是稳态(下载器已被
                            // PararDescargaFicheros 停止,分块进度由磁盘 worker 落盘),
                            // 且重启时 Paquete.MarcarFicherosComoParados 会把它归一化回
                            // EnCola;留着它只会让每次带暂停任务的关闭都白等满超时。
                            if (Fichero.DescargaEstado() == Estado.Descargando | Fichero.DescargaEstado() == Estado.CreandoLocal | Fichero.DescargaEstado() == Estado.Verificando | Fichero.DescargaEstado() == Estado.ComprobandoMD5)


                            {
                                TodosFinalizados = false;
                            }
                        }
                    }
                }

                if (!_bgwActualizadorDatosDiscoCompleted)
                    TodosFinalizados = false;
                if (!_bgwComprobarMaxConexionesCompleted)
                    TodosFinalizados = false;
                if (!_bgwActualizadorListaDescargasCompleted)
                    TodosFinalizados = false;
                if (DescompresorController.GetController().IsExtracting || _bgwDescompresor.IsBusy)
                    TodosFinalizados = false;
                // P2-2: pump the message loop instead of Thread.Sleep. Sleeping on the UI
                // thread stopped the pump, so BackgroundWorker completions marshaled back
                // to the UI thread (several "Completed" flags above are set from
                // RunWorkerCompleted) could never fire — the loop then always burned the
                // full timeout with a frozen UI. DoEvents lets those handlers run so the
                // loop exits as soon as everything is genuinely done. Reentrancy during
                // shutdown is already guarded by Cerrando/_ForzarCierre in the workers.
                // R2: DoEvents alone is NOT a wait - with an empty queue it returns immediately,
                // which measured ~174,000 iterations/sec here, each one taking
                // Mutex.ListaDescargas above and walking the whole download list. That burns a
                // core for the full TIMEOUT_CLOSE and starves the very threads we are waiting for
                // (they need the same lock, e.g. DescompresionFinalizada_EventHandler). Sleep(50)
                // keeps the pump - RunWorkerCompleted only needs ~20 Hz - without the spin.
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(50);

                if (Ini.AddSeconds(TimeoutSeg) < DateTime.Now)
                    TodosFinalizados = true;
            }
            if (Ini.AddSeconds(TimeoutSeg) < DateTime.Now)
            {
                Log.WriteInfo("We have waited " + TimeoutSeg + " seconds... we can't wait more!");
            }


        }

        private void RevisarSiHayQueApagarPC()
        {
            if (!Config.ApagarPC | _Timer is not null)
                return;


            bool TodosCompletadosOErroneos = true;
            bool HayErroneos = false;
            bool HayCola = false;

            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero Fichero in paq.ListaFicheros)
                    {

                        HayCola = true;

                        // Only fully successful tasks count as completed for auto-shutdown
                        if (Fichero.DescargaEstado() != Estado.Completado)
                        {
                            TodosCompletadosOErroneos = false;
                        }
                        if (Fichero.DescargaEstado() == Estado.Erroneo)
                        {
                            HayErroneos = true;
                            TodosCompletadosOErroneos = false;
                        }

                    }
                }
            }

            if (HayCola & TodosCompletadosOErroneos & !HayErroneos)
            {
                if (!Config.ResetearErrores | !HayErroneos)
                {
                    // Apagamos el PC!!!

                    bool AbortClose = false;
                    foreach (Form frm in My.MyProject.Application.OpenForms)
                    {
                        if (frm is Configuration) // No cerramos hasta que el usuario quite la pantalla de configuración
                        {
                            AbortClose = true;
                        }
                    }

                    if (!AbortClose)
                    {
                        _Timer = new System.Timers.Timer(); // Creamos un timer para que este worker no se quede bloqueado
                        _Timer.Elapsed += ApagarPC_Event;
                        _Timer.Interval = 500d;
                        _Timer.Enabled = true;
                    }
                }
            }

        }

        private System.Timers.Timer _Timer; // Temporizador que apaga el PC
        private void ApagarPC_Event(object source, System.Timers.ElapsedEventArgs e)
        {
            if (_Timer is not null)
            {
                _Timer.Enabled = false;
                _Timer = null;
                _ForzarCierre = true;

                Log.WriteWarning("Turning off PC automatically");

                Process.Start("shutdown", "/s /t 60 /c \"" + Language.GetText("Turning off in 60 seconds") + "\"");

                CerrarAplicacion();
            }
        }

        #endregion

        #region Manejo orden y prioridad de cola

        /// <summary>
    /// Reordena la prioridad en base al orden interno (el orden de la lista de paquetes y descarga se toma como 
    /// referencia a la hora de pintar la prioridad)
    /// </summary>
    /// <remarks>Se debe llamar a esta función cada vez que se modifica la lista de paquetes y descarga</remarks>
        private void ReordenarPrioridadPaquetes(bool RefrescarFicheros)
        {
            int i = 0;
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paquete in ListaPaquetes)
                {
                    i += 1;
                    paquete.SetDescargaPrioridad = i;
                    if (paquete.ListaFicheros is not null)
                    {
                        foreach (Fichero fichero in paquete.ListaFicheros)
                        {
                            i += 1;
                            fichero.SetDescargaPrioridad = i;
                        }
                    }
                }
            }
            if (RefrescarFicheros)
            {
                RefreshListaDescargas(RefrescarFicheros);
            }
        }



        /// <summary>
    /// Define el comportamiento del listView cuando se arrastra un elemento (drag and drop)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void ListaDescargas_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetData("BrightIdeasSoftware.OLVListItem") is not null)
            {
            }
            // Nothing, dropSink_ModelDropped will handle
            else
            {
                Main_DragDrop(sender, e);
            } // File drag & drop
        }

        /// <summary>
    /// Define el comportamiento del listView cuando se arrastra un elemento (drag and drop)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void ListaDescargas_CanDrop(object sender, OlvDropEventArgs e)
        {

            e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.Move);

            if (e.DragEventArgs.Data.GetData(DataFormats.FileDrop) is not null)
            {
                // File drag & drop
                string[] ficheros = (string[])e.DragEventArgs.Data.GetData(DataFormats.FileDrop);
                bool TodosExisten = true;
                foreach (string Fichero in ficheros)
                {
                    if (!System.IO.File.Exists(Fichero))
                    {
                        TodosExisten = false;
                    }
                }
                if (TodosExisten)
                {
                    e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.Copy);
                }
                else
                {
                    e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.None);
                }
            }

        }


        /// <summary>
    /// Define el comportamiento del listView cuando se arrastra un elemento (drag and drop)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void dropSink_ModelDropped(object sender, ModelDropEventArgs e)
        {

            foreach (IDescarga Source in e.SourceModels)
                RealizarMovimiento(Source, e.TargetModel, e.DropTargetLocation);
            ReordenarPrioridadPaquetes(true);
        }

        /// <summary>
    /// Realiza un movimiento dentro de la lista de paquetes
    /// </summary>
    /// <param name="Source"></param>
    /// <param name="Target"></param>
    /// <param name="TargetLocation"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        private bool RealizarMovimiento(object Source, object Target, DropTargetLocation TargetLocation)
        {
            if (Source is Fichero & Target is Fichero)
            {

                Fichero sourcet = (Fichero)Source;
                Fichero targett = (Fichero)Target;

                // Comprobamos que forman parte del mismo paquete
                lock (Mutex.ListaDescargas)
                {
                    foreach (Paquete paq in ListaPaquetes)
                    {

                        int indSource = paq.ListaFicheros.FindIndex(x => x.DescargaPrioridad() == sourcet.DescargaPrioridad());
                        int indTarget = paq.ListaFicheros.FindIndex(x => x.DescargaPrioridad() == targett.DescargaPrioridad());
                        if (indSource >= 0 & indTarget >= 0 & indTarget != indSource)
                        {
                            paq.ListaFicheros.RemoveRange(indSource, 1);
                            // Recalculamos el target (puede haber cambiado al eliminar el source!)
                            indTarget = paq.ListaFicheros.FindIndex(x => x.DescargaPrioridad() == targett.DescargaPrioridad());
                            paq.ListaFicheros.Insert(TargetLocation == DropTargetLocation.BelowItem ? indTarget + 1 : indTarget, sourcet);
                            return true;
                        }

                    }
                }
            }

            else if (Source is Paquete & Target is Paquete)
            {

                Paquete sourcet = (Paquete)Source;
                Paquete targett = (Paquete)Target;

                lock (Mutex.ListaDescargas)
                {
                    int indSource = ListaPaquetes.FindIndex(x => x.DescargaPrioridad() == sourcet.DescargaPrioridad());
                    int indTarget = ListaPaquetes.FindIndex(x => x.DescargaPrioridad() == targett.DescargaPrioridad());
                    if (indSource >= 0 & indTarget >= 0 & indTarget != indSource)
                    {
                        ListaPaquetes.RemoveRange(indSource, 1);
                        // Recalculamos el target (puede haber cambiado al eliminar el source!)
                        indTarget = ListaPaquetes.FindIndex(x => x.DescargaPrioridad() == targett.DescargaPrioridad());
                        ListaPaquetes.Insert(TargetLocation == DropTargetLocation.BelowItem ? indTarget + 1 : indTarget, sourcet);
                        return true;
                    }
                }

            }
            return false;
        }


        /// <summary>
    /// Sube la prioridad de un elemento en un punto
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void SubirPrioridad_Click(object sender, EventArgs e)
        {

            if (ListaDescargas.SelectedObjects is null)
                return;
            object ObjetoASeleccionar = null;
            foreach (object selobject in ListaDescargas.SelectedObjects)
            {


                if (selobject is Paquete)
                {
                    int Prioridad = ((Paquete)selobject).DescargaPrioridad();
                    int i = 0;
                    bool Encontrado = false;

                    lock (Mutex.ListaDescargas)
                    {
                        foreach (Paquete paq in ListaPaquetes)
                        {
                            if (paq.DescargaPrioridad() == Prioridad & i > 0)
                            {
                                Encontrado = true;
                                break;
                            }
                            i += 1;
                        }
                        if (Encontrado)
                        {
                            var paqTemp = ListaPaquetes[i - 1];
                            ListaPaquetes[i - 1] = (Paquete)selobject;
                            ListaPaquetes[i] = paqTemp;
                            ReordenarPrioridadPaquetes(true);
                            ObjetoASeleccionar = ListaPaquetes[i - 1];
                        }
                    }
                }

                else if (selobject is Fichero)
                {
                    int Prioridad = ((Fichero)selobject).DescargaPrioridad();

                    lock (Mutex.ListaDescargas)
                    {
                        foreach (Paquete paq in ListaPaquetes)
                        {
                            int i = 0;
                            bool Encontrado = false;
                            foreach (Fichero @file in paq.ListaFicheros)
                            {
                                if (@file.DescargaPrioridad() == Prioridad & i > 0)
                                {
                                    Encontrado = true;
                                    break;
                                }
                                i += 1;
                            }
                            if (Encontrado)
                            {
                                var ficTemp = paq.ListaFicheros[i - 1];
                                paq.ListaFicheros[i - 1] = (Fichero)selobject;
                                paq.ListaFicheros[i] = ficTemp;
                                ReordenarPrioridadPaquetes(true);
                                ObjetoASeleccionar = paq.ListaFicheros[i - 1];
                                break;
                            }
                        }
                    }

                }
            }
            if (ObjetoASeleccionar is not null)
                ListaDescargas.SelectedObject = ObjetoASeleccionar;
        }

        /// <summary>
    /// Baja la prioridad de un elemento en un punto
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void BajarPrioridadMenuItem_Click(object sender, EventArgs e)
        {

            if (ListaDescargas.SelectedObjects is null)
                return;
            foreach (object selobject in ListaDescargas.SelectedObjects)
            {


                if (selobject is Paquete)
                {
                    int Prioridad = ((Paquete)selobject).DescargaPrioridad();

                    Paquete paqSeleccionar = null;
                    lock (Mutex.ListaDescargas)
                    {
                        int numPaquetes = ListaPaquetes.Count - 1;
                        int i = 0;
                        bool Encontrado = false;
                        foreach (Paquete paq in ListaPaquetes)
                        {
                            if (paq.DescargaPrioridad() == Prioridad & i < numPaquetes)
                            {
                                Encontrado = true;
                                break;
                            }
                            i += 1;
                        }
                        if (Encontrado)
                        {
                            var paqTemp = ListaPaquetes[i + 1];
                            ListaPaquetes[i + 1] = (Paquete)selobject;
                            ListaPaquetes[i] = paqTemp;
                            paqSeleccionar = ListaPaquetes[i + 1];
                        }
                    }
                    if (paqSeleccionar is not null)
                    {
                        ReordenarPrioridadPaquetes(true);
                        ListaDescargas.SelectedObject = paqSeleccionar;
                    }
                }


                else if (selobject is Fichero)
                {
                    int Prioridad = ((Fichero)selobject).DescargaPrioridad();

                    Fichero FicheroASeleccionar = null;
                    lock (Mutex.ListaDescargas)
                    {
                        foreach (Paquete paq in ListaPaquetes)
                        {
                            int numFicheros = paq.ListaFicheros.Count - 1;
                            int i = 0;
                            bool Encontrado = false;
                            foreach (Fichero @file in paq.ListaFicheros)
                            {
                                if (@file.DescargaPrioridad() == Prioridad & i < numFicheros)
                                {
                                    Encontrado = true;
                                    break;
                                }
                                i += 1;
                            }
                            if (Encontrado)
                            {
                                var ficTemp = paq.ListaFicheros[i + 1];
                                paq.ListaFicheros[i + 1] = (Fichero)selobject;
                                paq.ListaFicheros[i] = ficTemp;
                                FicheroASeleccionar = paq.ListaFicheros[i + 1];
                                break;
                            }
                        }
                    }

                    if (FicheroASeleccionar is not null)
                    {
                        ReordenarPrioridadPaquetes(true);
                        ListaDescargas.SelectedObject = FicheroASeleccionar;
                    }
                }
            }
        }

        #endregion

        #region Eliminar descarga


        private void EliminarMenuItem_Click(object sender, EventArgs e)
        {
            if (ListaDescargas.SelectedObjects is null)
                return;

            if (MessageBox.Show(Language.GetText("Do you want to delete the element(s)?") + Constants.vbNewLine + Language.GetText("Note: files will NOT be deleted"), Language.GetText("Confirmation"), MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
            {
                return;
            }

            foreach (object selobject in ListaDescargas.SelectedObjects)
            {
                if (selobject is IDescarga)
                {
                    Log.WriteError("Deleting from list " + ((IDescarga)selobject).DescargaNombre());
                    Eliminar((IDescarga)selobject, false, false);
                }
            }
            RefreshListaDescargas(true);
        }

        private void EliminarYBorrarMenuItem_Click(object sender, EventArgs e)
        {
            if (ListaDescargas.SelectedObjects is null)
                return;

            if (MessageBox.Show(Language.GetText("Do you want to delete the element(s)?") + Constants.vbNewLine + Language.GetText("Note: files will BE deleted"), Language.GetText("Confirmation"), MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
            {
                return;
            }

            var seleccion = new List<IDescarga>();
            var candidatos = new List<Fichero>();
            foreach (object selobject in ListaDescargas.SelectedObjects)
            {
                if (selobject is IDescarga)
                {
                    IDescarga desc = (IDescarga)selobject;
                    seleccion.Add(desc);
                    if (desc is Fichero)
                    {
                        candidatos.Add((Fichero)desc);
                    }
                    else if (desc is Paquete)
                    {
                        candidatos.AddRange(((Paquete)desc).ListaFicheros);
                    }
                }
            }
            if (seleccion.Count == 0)
                return;

            // B3: 旧队列条目的归属校验要整文件算 MetaMAC(1GB 约 36~48s),原实现在 UI
            // 线程、且持 Mutex.DeletingFiles 全局锁同步执行,大文件一次删除就卡死界面。
            // 先在后台线程预热出缓存,再回 UI 线程执行删除(此时归属判定已是 O(1))。
            var preparador = new BackgroundWorker();
            preparador.DoWork += (s, ev) =>
    {
        foreach (Fichero fic in candidatos)
            fic.PrepareOutputOwnership();
    };
            preparador.RunWorkerCompleted += (s, ev) =>
    {
        if (ev.Error is not null)
        {
            Log.WriteError("Output ownership precheck failed: " + Log.SafeException(ev.Error));
        }
        // B4: 4 词公链 key 没有 MetaMAC,无法自动确认归属。批量只询问一次,
        // 不再像原实现那样静默把成品留在磁盘上、用户却毫不知情。
        var sinConfirmar = new List<Fichero>();
        foreach (Fichero fic in candidatos)
        {
            if (fic.OutputNeedsConfirmation && !string.IsNullOrEmpty(fic.OutputConfirmationPath))
            {
                sinConfirmar.Add(fic);
            }
        }
        if (sinConfirmar.Count > 0)
        {
            string texto = Language.GetText("Delete_UnverifiedOutputConfirm").Replace("{COUNT}", sinConfirmar.Count.ToString());
            if (MessageBox.Show(this, texto, Language.GetText("Confirmation"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            {
                foreach (Fichero fic in sinConfirmar)
                    fic.ConfirmUnverifiedOutputDeletion = true;
            }
            else
            {
                // "No" cancels the whole remove+delete operation. Otherwise the
                // tasks would disappear from the queue while their unverified
                // output stayed on disk as orphaned files.
                ToastForm.ShowToast(this, Language.GetText("Delete_UnverifiedOutputKept"));
                return;
            }
        }
        foreach (IDescarga desc in seleccion)
        {
            Log.WriteError("Deleting from list and disk " + desc.DescargaNombre());
            Eliminar(desc, true, false);
        }
        RefreshListaDescargas(true);
    };
            preparador.RunWorkerAsync();
        }

        private void Eliminar(IDescarga Objeto, bool BorrarFicheros, bool RefrescarFicheros)
        {

            // Quitamos el objeto de la lista y así no lo pintamos ni procesamos más
            Paquete paqueteAEliminar = null;
            Fichero ficheroAEliminar = null;

            lock (Mutex.ListaDescargas)
            {
                do
                {
                    try
                    {
                        // P1-1: locate the object by reference identity, never by DescargaPrioridad.
                        // The priority is a mutable 1..N index that ReordenarPrioridadPaquetes renumbers
                        // after every single deletion, so in a multi-select delete (or the "remove all
                        // failed"/"remove completed" loops) the 2nd..Nth lookups matched whatever object
                        // had been renumbered into the stale slot — unrelated tasks got deleted (with
                        // BorrarFicheros=True their files too) while the real targets were skipped.
                        // SelectedObjects and the collectors hand us the live model instances, so
                        // ReferenceEquals is exact for every caller.
                        bool exitTry1 = false;
                        foreach (Paquete paq in ListaPaquetes)
                        {
                            if (ReferenceEquals(paq, Objeto))
                            {
                                paqueteAEliminar = paq;
                                exitTry1 = true;
                                break;
                            }
                            bool exitTry = false;
                            foreach (Fichero fic in paq.ListaFicheros)
                            {
                                if (ReferenceEquals(fic, Objeto))
                                {
                                    ficheroAEliminar = fic;
                                    paqueteAEliminar = paq;
                                    exitTry1 = exitTry = true;
                                    break;
                                }
                            }

                            if (exitTry)
                            {
                                break;
                            }
                        }

                        if (exitTry1)
                        {
                            break;
                        }
                    }
                    finally
                    {
                    }
                }
                while (false);
                if (ficheroAEliminar is null && paqueteAEliminar is null)
                {
                    // 选中"包及其子文件"时子项会走到这里:它已随包被一并移除,
                    // 跳过后续 Stop/Dispose,避免 CancellationComplete 双挂导致 BorrarFicheroLocal/Dispose 执行两遍
                    return;
                }
                if (ficheroAEliminar is not null)
                {
                    paqueteAEliminar.ListaFicheros.Remove(ficheroAEliminar);
                    ThrottledStreamController.GetController().RemoveId(ficheroAEliminar.FileID);
                    if (paqueteAEliminar.ListaFicheros.Count == 0)
                    {
                        ListaPaquetes.Remove(paqueteAEliminar);
                    }
                }
                else if (paqueteAEliminar is not null)
                {
                    ListaPaquetes.Remove(paqueteAEliminar);
                    foreach (Fichero fic in paqueteAEliminar.ListaFicheros)
                        ThrottledStreamController.GetController().RemoveId(fic.FileID);
                }
                PeticionGuardadoFichero = DateTime.Now;
            }
            ReordenarPrioridadPaquetes(RefrescarFicheros);


            // Ya podemos trabajar con el objeto "tranquilamente"
            if (Objeto is Fichero)
            {
                Fichero fic = (Fichero)Objeto;
                fic.RemovePendingExtraction();
                switch (Objeto.DescargaEstado())
                {
                    case Estado.ComprobandoMD5:
                    case Estado.Descargando:
                    case Estado.Pausado:
                    case Estado.Descomprimiendo:
                    case Estado.EsperandoVolumenes:
                    case Estado.CreandoLocal:
                        fic.CancellationComplete += DisposeFichero;
                        fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros;
                        if (BorrarFicheros)
                            fic.LimpiezaPaquete = paqueteAEliminar;
                        fic.Stop();
                        Log.WriteDebug("File download stopped " + fic.NombreFichero);
                        break;
                    default:
                        fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros;
                        fic.BorrarFicheroLocal();
                        fic.Dispose();
                        if (BorrarFicheros)
                            LimpiarDirectoriosVacios(paqueteAEliminar, new Fichero[] { fic });
                        break;
                }
            }

            else if (Objeto is Paquete)
            {

                Paquete paqObj = (Paquete)Objeto;
                var limpios = new List<Fichero>();
                foreach (Fichero fic in paqObj.ListaFicheros)
                {
                    fic.RemovePendingExtraction();
                    switch (fic.DescargaEstado())
                    {
                        case Estado.ComprobandoMD5:
                        case Estado.Descargando:
                        case Estado.Pausado:
                        case Estado.Descomprimiendo:
                        case Estado.EsperandoVolumenes:
                        case Estado.CreandoLocal:
                            fic.CancellationComplete += DisposeFichero;
                            fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros;
                            if (BorrarFicheros)
                                fic.LimpiezaPaquete = paqObj;
                            fic.Stop();
                            Log.WriteDebug("File download stopped " + fic.NombreFichero);
                            break;
                        default:
                            fic.MarcadoParaBorrarFicheroLocal = BorrarFicheros;
                            fic.BorrarFicheroLocal();
                            fic.Dispose();
                            if (BorrarFicheros)
                                limpios.Add(fic);
                            break;
                    }
                }
                if (BorrarFicheros)
                    LimpiarDirectoriosVacios(paqObj, limpios);
                paqObj.ListaFicheros.Clear();
            }

        }

        /// <summary>删盘后顺手清理变空的目录:从各文件所在目录向上,空则删;
    /// 绝不越过包根;包根本身仅当 CrearSubdirectorio(程序自建)且已空才删,
    /// 用户自选下载目录(未建子目录)永远保留。只删空目录,有文件/子目录即停。</summary>
        private void LimpiarDirectoriosVacios(Paquete paquete, IEnumerable<Fichero> ficheros)
        {
            try
            {
                if (paquete is null || string.IsNullOrWhiteSpace(paquete.RutaLocal))
                    return;
                char sep = System.IO.Path.DirectorySeparatorChar;
                string pkgRoot = System.IO.Path.GetFullPath(paquete.RutaLocal).TrimEnd(sep, System.IO.Path.AltDirectorySeparatorChar);
                var dirs = new List<string>();
                if (ficheros is not null)
                {
                    foreach (Fichero f in ficheros)
                    {
                        try
                        {
                            if (f is null || string.IsNullOrWhiteSpace(f.RutaLocal))
                                continue;
                            string d = System.IO.Path.GetFullPath(f.RutaLocal).TrimEnd(sep, System.IO.Path.AltDirectorySeparatorChar);
                            if (d.Equals(pkgRoot, StringComparison.OrdinalIgnoreCase) || d.StartsWith(pkgRoot + sep, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!dirs.Contains(d, StringComparer.OrdinalIgnoreCase))
                                    dirs.Add(d);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                // 深的先删,父目录随后变空才能继续删
                dirs.Sort(new Comparison<string>((x, y) => y.Length.CompareTo(x.Length)));
                foreach (string d in dirs)
                {
                    try
                    {
                        if (!d.Equals(pkgRoot, StringComparison.OrdinalIgnoreCase) && System.IO.Directory.Exists(d) && System.IO.Directory.GetFileSystemEntries(d).Length == 0)
                        {
                            System.IO.Directory.Delete(d);
                            Log.WriteInfo("Deleted empty directory: " + d);
                        }
                    }
                    catch
                    {
                    }
                }
                try
                {
                    if (paquete.CrearSubdirectorio && System.IO.Directory.Exists(pkgRoot) && System.IO.Directory.GetFileSystemEntries(pkgRoot).Length == 0)
                    {
                        System.IO.Directory.Delete(pkgRoot);
                        Log.WriteInfo("Deleted empty package directory: " + pkgRoot);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Could not delete empty package directory " + pkgRoot + ": " + Log.SafeException(ex));
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("LimpiarDirectoriosVacios failed: " + Log.SafeException(ex));
            }
        }

        private void DisposeFichero(object sender, EventArgs e)
        {
            if (sender is Fichero)
            {
                Fichero fic = (Fichero)sender;
                if (fic is not null)
                {
                    // B7: 回调可能在同一轮取消里到达两次。先摘钩,再用每实例门闩保证
                    // BorrarFicheroLocal / Dispose 只执行一次(原代码会执行两遍)。
                    fic.CancellationComplete -= DisposeFichero;
                    if (System.Threading.Interlocked.Exchange(ref fic.DisposalHandled, 1) != 0)
                        return;
                    Log.WriteDebug("Download file stopped " + fic.NombreFichero + ", pending delete");
                    if (fic.MarcadoParaBorrarFicheroLocal)
                    {
                        fic.BorrarFicheroLocal();
                    }
                    var paqLimpieza = fic.LimpiezaPaquete;
                    fic.LimpiezaPaquete = null;
                    if (paqLimpieza is not null)
                    {
                        LimpiarDirectoriosVacios(paqLimpieza, new Fichero[] { fic });
                    }
                    fic.Dispose();
                }
            }
        }

        #endregion

        #region Funciones control remoto

        public decimal? ControlRemotoObtenerVelocidad()
        {
            return VelocidadGlobalDescarga;
        }

        public int? ControlRemotoObtenerDescargasActivas()
        {
            return NumDescargasActivas;
        }

        public int? ControlRemotoObtenerDescargasCompletadas()
        {
            return NumDescargasCompletadas;
        }

        public int? ControlRemotoObtenerDescargasErroneas()
        {
            return NumDescargasErroneas;
        }

        public int? ControlRemotoObtenerDescargasEnCola()
        {
            return NumDescargasEnCola;
        }

        internal TipoEstadoAplicacion ControlRemotoObtenerEstado()
        {
            return EstadoAplicacion;
        }

        // IDownloaderService 实现：Web 层只经接口取状态，不再依赖 Main.TipoEstadoAplicacion。
        // 成员顺序与 TipoEstadoAplicacion 一致（Descargando=0/Pausa=1/Parado=2），CType 直接映射。
        public DownloaderEstado ControlRemotoObtenerEstadoSvc()
        {
            return (DownloaderEstado)EstadoAplicacion;
        }

        DownloaderEstado IDownloaderService.ControlRemotoObtenerEstado() => ControlRemotoObtenerEstadoSvc();

        public void ControlRemotoDescargar()
        {
            btnPlay_Click(null, null);
        }
        public void ControlRemotoParar()
        {
            btnStop_Click(null, null);
        }

        public string ControlRemotoAgregarLinks(string Links, string NombrePaquete, bool CrearDirectorio)
        {
            if (System.Threading.Volatile.Read(ref _startupQueueLoading))
                return Language.GetText("Loading please wait");
            if (Paquete.HasTransientLoadError)
                return Language.GetText("Queue_LoadFailed");
            var URLs = URLExtractor.ExtraerURLs(Links);
            if (URLs.Count == 0)
            {
                return Language.GetText("No valid URLs have been inserted");
            }
            else if (string.IsNullOrEmpty(Config.RutaDefecto) || !System.IO.Directory.Exists(Config.RutaDefecto))
            {
                return Language.GetText("Can not add remote links without a default path");
            }
            else
            {
                // B1-①:Web 推送与手动加链走同链路——先经 URLProcessor 展开文件夹/ELC
                // (此前逐 URL 直建 Fichero,文件夹链变成单个 FileID=根ID 的坏任务,API 报 ENOENT 永久错误);
                // pckname 强净化(此前字符串拼接 .RutaLocal & "\" & .Nombre,已认证任意目录创建+下载落盘越狱)。
                List<URLProcessor.FileURL> expanded = null;
                try
                {
                    expanded = URLProcessor.ProcessURLs(URLs, ref Config);
                }
                catch (Exception ex)
                {
                    Log.WriteError("ControlRemotoAgregarLinks: failed to resolve links: " + Log.SafeException(ex));
                    return ex.Message;
                }
                if (expanded is null || expanded.Count == 0)
                {
                    return Language.GetText("No valid URLs have been inserted");
                }
                var oPaquete = new Paquete();
                oPaquete.Nombre = NombrePaquete;
                if (string.IsNullOrEmpty(oPaquete.Nombre))
                {
                    // 文件夹分享用真名(MegaFolderHelper 展开时已解密带出),其余仍兜底"新建包"。
                    string folderName = URLProcessor.GetCommonFolderName(expanded);
                    oPaquete.Nombre = string.IsNullOrEmpty(folderName) ? Language.GetText("New package") : folderName;
                }
                oPaquete.RutaLocal = Config.RutaDefecto;
                oPaquete.CrearSubdirectorio = CrearDirectorio;

                // Creamos el directorio
                if (oPaquete.CrearSubdirectorio)
                {
                    string packageSegment = PathGuard.SanitizeFileName(oPaquete.Nombre, Language.GetText("New package"));
                    try
                    {
                        oPaquete.RutaLocal = PathGuard.GetSafePathUnderRoot(oPaquete.RutaLocal, packageSegment, allowRoot: false);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("ControlRemotoAgregarLinks: invalid package name: " + Log.SafeException(ex));
                        return Language.GetText("Invalid package name");
                    }
                    System.IO.Directory.CreateDirectory(oPaquete.RutaLocal);
                }

                Log.WriteWarning("Adding package in " + oPaquete.RutaLocal);

                oPaquete.set_SetDescargaExtraccionAutomatica(null, Config.ExtraerAutomaticamente);
                foreach (URLProcessor.FileURL FileURL in expanded)
                {

                    string ruta = oPaquete.RutaLocal;
                    if (!string.IsNullOrEmpty(FileURL.Path))
                    {
                        try
                        {
                            ruta = PathGuard.GetSafePathUnderRoot(oPaquete.RutaLocal, FileURL.Path, allowRoot: true);
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("ControlRemotoAgregarLinks: invalid subfolder path, using package root: " + Log.SafeException(ex));
                            ruta = oPaquete.RutaLocal;
                        }
                        System.IO.Directory.CreateDirectory(ruta);
                    }

                    string URLFile = FileURL.URL;
                    bool Visible = true;
                    if (!string.IsNullOrEmpty(URLFile) && URLFile.Contains(Fichero.HIDDEN_LINK))
                    {
                        Visible = false;
                        URLFile = URLFile.Replace(Fichero.HIDDEN_LINK, "");
                    }

                    var oFichero = new Fichero(URLFile);
                    oFichero.LinkVisible = Visible;
                    oFichero.RutaLocal = ruta;
                    oFichero.RutaRelativa = FileURL.Path ?? string.Empty;
                    oFichero.NombreFichero = Visible ? URLFile : Fichero.HIDDEN_LINK_DESC;
                    oFichero.FileID = Fichero.ExtraerFileID(URLFile);
                    oFichero.FileKey = Fichero.ExtraerFileKey(URLFile);
                    oFichero.set_SetDescargaExtraccionAutomatica(null, Config.ExtraerAutomaticamente);
                    Log.WriteWarning("Adding files to package: " + oFichero.FileID);
                    oPaquete.AgregarFichero(oFichero);

                }
                AgregarPaquete(oPaquete, true);

                return "";
            }
        }


        #endregion

        #region Eventos varios


        /// <summary>
    /// Define el comportamiento del evento del portapapeles
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void clipChange_ClipboardChanged(object sender, EventArgs e)
        {
            // Application.DoEvents()

            if (_startupReady && Config is not null && Config.AnalizarPortapapeles)
            {
                LeerPortapapelesYAgregarLinks();
            }
        }

        /// <summary>
    /// Reads the clipboard text and pushes it through ComprobarYAgregarLinks.
    /// Web browsers (Chrome/Edge/Firefox) use DELAYED RENDERING: when the clipboard
    /// change notification arrives the data has not been written yet, and an
    /// immediate read returns empty or throws CLIPBRD_E_CANT_OPEN because the
    /// source process still owns the clipboard. Retry for a short window before
    /// giving up (fixes "clipboard monitoring misses copies from web pages").
    /// </summary>
        private async void LeerPortapapelesYAgregarLinks()
        {
            const int MaxIntentos = 5;
            const int RetrasoMs = 150;

            for (int intento = 1; intento <= MaxIntentos; intento++)
            {
                string textoPortapapeles = null;
                try
                {
                    IDataObject data = Clipboard.GetDataObject();
                    if (data is not null)
                    {
                        textoPortapapeles = data.GetData(typeof(string)) as string;
                    }
                }
                catch (ExternalException ex)
                {
                    // CLIPBRD_E_CANT_OPEN: another process holds the clipboard open — retry shortly
                    Log.WriteWarning("Clipboard busy on change notification (attempt " + intento + "): " + Log.SafeException(ex));
                }
                catch (Exception ex)
                {
                    Log.WriteWarning("Clipboard read failed (attempt " + intento + "): " + Log.SafeException(ex));
                }

                if (!string.IsNullOrEmpty(textoPortapapeles))
                {
                    ComprobarYAgregarLinks(textoPortapapeles, true, false);
                    return;
                }

                // Empty read: either delayed rendering (data not materialized yet) or
                // a non-text copy. Wait briefly and try again before discarding.
                if (intento < MaxIntentos)
                {
                    await Task.Delay(RetrasoMs);
                }
            }
        }

        public static Form IsFormAlreadyOpen(Type FormType)
        {
            foreach (Form OpenForm in Application.OpenForms)
            {
                if (OpenForm.GetType().FullName == FormType.FullName)
                {
                    return OpenForm;
                }
            }
            return default;
        }



        // Hace un "trim" de la memoria, equivalente a minimizar la ventana... muy útil cuando
        // This delegate enables asynchronous calls for setting
        // the text property on a TextBox control.
        public delegate void MsgBoxCallback(string text, string caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon);
        private void MsgBox(string text, string caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (StatusStrip1.InvokeRequired)
            {
                var d = new MsgBoxCallback(MsgBox);
                Invoke(d, new object[] { text, caption, buttons, icon });
            }
            else
            {
                MessageBox.Show(text, caption, buttons, icon);
            }
        }

        // This delegate enables asynchronous calls for setting
        // the text property on a TextBox control.
        public delegate void SetStatusBarCallback(string RAM, string Proc, string Estado, string velocidad, string configuracionConexiones);
        private void SetStatusBar(string RAM, string Proc, string Estado, string velocidad, string configuracionConexiones)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            try
            {
                if (Cerrando)
                    return;
                if (StatusStrip1.InvokeRequired)
                {
                    var d = new SetStatusBarCallback(SetStatusBar);
                    Invoke(d, new object[] { RAM, Proc, Estado, velocidad, configuracionConexiones });
                }
                else
                {
                    RAMProcToolStripStatusLabel.Text = Language.GetText("RAM") + ": " + RAM + " / " + Language.GetText("Proc") + ": " + Proc;
                    string quotaSuffix = "";
                    try
                    {
                        var qrem = MegaQuotaManager.GetRemaining();
                        if (qrem.HasValue)
                        {
                            quotaSuffix = "    " + Language.GetText("Quota_Status").Replace("%T%", FormatQuotaRemaining(qrem.Value));
                        }
                    }
                    catch
                    {
                    }
                    StatusToolStripStatusLabel.Text = Language.GetText("Status") + ": " + Estado + velocidad + "    " + Language.GetText("Connection conf") + ": " + configuracionConexiones + quotaSuffix;
                }
            }
            catch (Exception ex)
            {
                // No hacemos nada
            }
        }

        // Permite refrescar el listado de descargas
        // This delegate enables asynchronous calls for refreshing ListaDescargas
        public delegate void RefreshListaDescargasCallback(bool SetObjects);
        private int _periodicListRefreshPending;

        private void QueuePeriodicListRefresh()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _periodicListRefreshPending, 1, 0) != 0)
                return;
            try
            {
                BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!Cerrando && !IsDisposed && Visible && WindowState != FormWindowState.Minimized)
                            {
                                RefreshListaDescargas(false);
                            }
                        }
                        finally
                        {
                            System.Threading.Interlocked.Exchange(ref _periodicListRefreshPending, 0);
                        }
                    }));
            }
            catch (InvalidOperationException ex)
            {
                System.Threading.Interlocked.Exchange(ref _periodicListRefreshPending, 0);
            }
        }

        private void RefreshListaDescargas(bool SetObjects)
        {
            if (Cerrando || IsDisposed || ListaDescargas.IsDisposed)
                return;
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (ListaDescargas.InvokeRequired)
            {
                if (SetObjects)
                {
                    var d = new RefreshListaDescargasCallback(RefreshListaDescargas);
                    Invoke(d, new object[] { true });
                }
                else
                {
                    QueuePeriodicListRefresh();
                }
            }
            else
            {
                var overview = System.Threading.Volatile.Read(ref queueOverviewSnapshot);
                bool scopeChanged = overview is not null && overview.ScopeVersion != appliedScopeVersion;
                lock (Mutex.ListaDescargas)
                {
                    ListaDescargas.BeginUpdate();
                    try
                    {
                        // ObjectListView.SetObjects asserts when called before the native handle is
                        // created (the VB project's startup timing never hit it, .NET 8 does). ApplyNavRoots
                        // publishes the same queue into Roots once the handle exists.
                        // The legacy OLV build tolerates SetObjects before the native handle
                        // exists; the .NET 8 build asserts in VirtualListSize. Defer to Roots,
                        // which ApplyNavRoots sets on the UI thread once the handle is ready.
                        if (SetObjects && !ListaDescargas.IsDisposed && ListaDescargas.IsHandleCreated)
                            ListaDescargas.SetObjects(ListaPaquetes);
                        if ((SetObjects || _navScope != DownloadEstadoFilter.NavScope.All && scopeChanged) && ListaDescargas.IsHandleCreated && !ListaDescargas.IsDisposed)
                        {
                            ApplyNavRoots();
                            ListaDescargas.BuildList();
                        }
                        if (ListaDescargas.IsHandleCreated && !ListaDescargas.IsDisposed)
                            ListaDescargas.RefreshObjects((IList)ListaDescargas.Roots);
                    }
                    finally
                    {
                        ListaDescargas.EndUpdate();
                    }
                    UpdateNavCounts();
                    UpdateDetailPanel();
                }
                if (overview is not null)
                    appliedScopeVersion = overview.ScopeVersion;
                if (workbenchReady && workbenchSearch.TextLength > 0 && (SetObjects || overview is not null && overview.SearchVersion != requestedSearchVersion))
                {
                    if (overview is not null)
                        requestedSearchVersion = overview.SearchVersion;
                    System.Threading.Interlocked.Increment(ref queueSearchGeneration);
                    workbenchSearchTimer.Stop();
                    workbenchSearchTimer.Start();
                }

            }
        }

        // This delegate enables asynchronous calls for setting
        // the text property on a TextBox control.
        public delegate void TextoIconoMinimizadoCallback(string txt);
        private void TextoIconoMinimizado(string txt)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (btnPause.InvokeRequired)
            {
                var d = new TextoIconoMinimizadoCallback(TextoIconoMinimizado);
                Invoke(d, new object[] { txt });
            }
            else
            {
                IconoMinimizado.Text = txt;
            }
        }

        // This delegate enables asynchronous calls for setting
        // the text property on a TextBox control.
        public delegate void CerrarAplicacionCallback();
        private void CerrarAplicacion()
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (btnPause.InvokeRequired)
            {
                var d = new CerrarAplicacionCallback(CerrarAplicacion);
                Invoke(d, new object[] { });
            }
            else
            {
                Close();
            }
        }


        /// <summary>
    /// P2(审查):空态 hint 承诺"拖入链接",但此前只收 FileDrop(.dlc/.elc),
    /// 浏览器拖过来的链接文本被静默丢弃;且侧栏子控件 AllowDrop=False 会吞事件。
    /// 此处补文本分支(复用 ComprobarYAgregarLinks,与剪贴板同一入口),
    /// 子面板由 EnableLinkDrop 递归打开并转发到这两个处理器(文件逻辑不动)。
    /// </summary>
        private void EnableLinkDrop(Control target)
        {
            if (target is null || target.IsDisposed)
                return;
            try
            {
                target.AllowDrop = true;
                target.DragEnter -= Main_DragEnter;
                target.DragDrop -= Main_DragDrop;
                target.DragEnter += Main_DragEnter;
                target.DragDrop += Main_DragDrop;
                foreach (Control c in target.Controls)
                    EnableLinkDrop(c);
            }
            catch
            {
            }
        }

        private void Main_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            string dropText = e.Data.GetData(DataFormats.UnicodeText) as string;
            if (string.IsNullOrEmpty(dropText))
                dropText = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(dropText))
            {
                ComprobarYAgregarLinks(dropText, true, false, false);
                return;
            }
            if (e.Data.GetData(DataFormats.FileDrop) is not null)
            {
                // File drag & drop

                string[] ficheros = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool TodosExisten = true;
                foreach (string Fichero in ficheros)
                {
                    if (!System.IO.File.Exists(Fichero)) // No permitimos directorios
                    {
                        TodosExisten = false;
                    }
                }
                if (TodosExisten)
                {

                    // ⑥:多个 .elc/.dlc 逐个排队导入,此前 Exit Sub 只进第一个。
                    int handled = 0;
                    foreach (string Fichero in ficheros)
                    {
                        if (Fichero.ToUpper().EndsWith(".DLC") | Fichero.ToUpper().EndsWith(".ELC"))
                        {
                            AddDLC(Fichero);
                            handled += 1;
                        }
                    }
                    if (handled > 0)
                        return;

                    // 拖入的是其它类型文件:给出提示而不是静默丢弃。
                    // 文件夹走不到这里 —— DragEnter 已置 None,OLE 不会再调 Drop,
                    // 用户看到的是"禁止放置"光标,这是 Windows 的标准反馈。
                    MessageBox.Show(Language.GetText("Only ELC and DLC files are supported by drag and drop"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void Main_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text))
            {
                e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.Copy);
                return;
            }
            if (e.Data.GetData(DataFormats.FileDrop) is not null)
            {
                // File drag & drop
                string[] ficheros = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool TodosExisten = true;
                foreach (string Fichero in ficheros)
                {
                    if (!System.IO.File.Exists(Fichero))  // No permitimos directorios
                    {
                        TodosExisten = false;
                    }
                }
                if (TodosExisten)
                {
                    e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.Copy);
                }
                else
                {
                    // 必须显式置 None:e.Effect 进来时是源允许的效果,不写回就等于告诉用户
                    // "可以放",松手后却什么都不发生(拖文件夹是最常见的情形)。
                    e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.None);
                }
            }
        }

        // Private Sub Main_DragDrop(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragDrop
        // Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
        // For Each Fichero As String In ficheros
        // If Fichero.ToUpper.EndsWith(".SSK") And Me.Config.PermitirSkins Then
        // SkinEngine.SkinFile = Fichero
        // Me.Config.ConfigUI.RutaSkin = Fichero
        // If Not SkinEngine.Active Then
        // SkinEngine.Active = True
        // End If
        // End If
        // Next
        // End Sub

        // Private Sub Main_DragEnter(sender As Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragEnter
        // If e.Data.GetDataPresent(DataFormats.FileDrop) And Me.Config.PermitirSkins Then
        // Dim ficheros() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
        // For Each Fichero As String In ficheros
        // If Fichero.ToUpper.EndsWith(".SSK") Then
        // e.Effect = DragDropEffects.Copy
        // End If
        // Next
        // End If
        // End Sub

        private DateTime? ProximoAvisoActualizacion;

        // Update availability and the reminder deadline belong to the UI thread.
        private void ApplyUpdateCheckResult(string url, string version)
        {
            if (Cerrando || IsDisposed || Config is null)
                return;
            bool available = Config.CheckUpdates && !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(version);
            bool sameVersion = string.Equals(VersionNuevaVersionMegadownloader, version, StringComparison.Ordinal);
            UrlNuevaVersionMegadownloader = available ? url : null;
            VersionNuevaVersionMegadownloader = available ? version : null;
            btnUpdate.Visible = available;
            if (!available || string.Equals(Config.UpdateSkipVersion, version, StringComparison.Ordinal))
            {
                ProximoAvisoActualizacion = default;
            }
            else if (!sameVersion)
            {
                ProximoAvisoActualizacion = DateTime.Now.AddSeconds(15d);
            }
        }

        private void UpdateReminder_Tick(object sender, EventArgs e)
        {
            if (Cerrando || IsDisposed || Config is null || !Config.CheckUpdates || !Visible || !object.ReferenceEquals(Form.ActiveForm, this) || WindowState == FormWindowState.Minimized || !ProximoAvisoActualizacion.HasValue || DateTime.Now < ProximoAvisoActualizacion.Value)
                return;

            ProximoAvisoActualizacion = default;
            string version = VersionNuevaVersionMegadownloader;
            if (string.IsNullOrEmpty(UrlNuevaVersionMegadownloader) || string.Equals(Config.UpdateSkipVersion, version, StringComparison.Ordinal))
                return;
            string mensaje = Language.GetText("New version do you want to download it? Recommended!") + Constants.vbNewLine + Constants.vbNewLine + "Version: " + version + Constants.vbNewLine + Language.GetText("Update prompt hint");
            DialogResult respuesta = MessageBox.Show(this, mensaje, Language.GetText("New version available"), MessageBoxButtons.YesNoCancel);
            switch (respuesta)
            {
                case System.Windows.Forms.DialogResult.Yes:
                    btnUpdate_Click(null, null);
                    break;
                case System.Windows.Forms.DialogResult.No:
                    if (string.Equals(VersionNuevaVersionMegadownloader, version, StringComparison.Ordinal))
                    {
                        ProximoAvisoActualizacion = DateTime.Now.AddHours(3d);
                    }

                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                    Config.UpdateSkipVersion = version;
                    Config.GuardarXML(false);
                    Log.WriteInfo("User skipped version reminder: " + version);
                    break;
            }
        }


        #endregion

        #region Botones y menús

        /// <summary>
    /// Definimos el menú que saldrá según donde se haga click derecho
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void ListaDescargas_CellRightClick(object sender, CellRightClickEventArgs e)
        {

            if (ListaDescargas.SelectedObjects is null || ListaDescargas.SelectedObjects.Count == 0)
            {
                e.MenuStrip = MenuPanel;
            }
            else
            {
                VerErrorToolStripMenuItem.Visible = false;
                VerProgresoDescompresionToolStripMenuItem.Visible = false;
                ResetToolStripMenuItem.Visible = false;
                PropiedadesToolStripMenuItem.Enabled = false;
                PausarStripMenuItem.Visible = false;
                ForceDownloadStripMenuItem.Visible = false;
                RetryAllFailedMenuItem.Visible = false;
                RemoveAllFailedMenuItem.Visible = false;
                if (CollectErroneoFiles().Count > 0)
                {
                    RetryAllFailedMenuItem.Visible = true;
                    RemoveAllFailedMenuItem.Visible = true;
                }
                // 批量组隐藏时连带藏起它的分隔线,避免"恢复列宽"上方出现双线。
                sepBatchMenuItem.Visible = RetryAllFailedMenuItem.Visible;
                if (ListaDescargas.SelectedObjects.Count == 1)
                {
                    PropiedadesToolStripMenuItem.Enabled = true;
                }
                foreach (object o in ListaDescargas.SelectedObjects)
                {
                    if (o is IDescarga && ((IDescarga)o).DescargaEstado() == Estado.Erroneo)
                    {
                        VerErrorToolStripMenuItem.Visible = true;
                        ResetToolStripMenuItem.Visible = true;
                    }
                    else if (o is Fichero && ((Fichero)o).DescargaEstado() == Estado.Descargando)
                    {
                        PausarStripMenuItem.Visible = true;
                    }
                    else if (o is Fichero && ((Fichero)o).DescargaEstado() == Estado.EnCola)
                    {
                        ForceDownloadStripMenuItem.Visible = true;
                    }
                    else if (o is Fichero && ((Fichero)o).DescargaEstado() == Estado.Pausado)
                    {
                        ForceDownloadStripMenuItem.Visible = true;
                    }
                    else if (o is Fichero && ((Fichero)o).DescargaEstado() == Estado.Descomprimiendo)
                    {
                        VerProgresoDescompresionToolStripMenuItem.Visible = true;
                    }
                }
                e.MenuStrip = MenuDescarga;
            }
        }


        /// <summary>
    /// Agregamos links con el menú contextual
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void AgregarLinksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AgregarLink();
        }

        private void AgregarLinkStripMenuItem_Click(object sender, EventArgs e)
        {
            AgregarLink();
        }


        private void btnAddLink_Click(object sender, EventArgs e)
        {
            AgregarLink();
        }


        private void LimpiarCompletados2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LimpiarCompletados();
        }
        private void LimpiarCompletadosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LimpiarCompletados();
        }

        private void LimpiarCompletados()
        {
            var listaFicherosEliminar = new List<Fichero>();
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete paq in ListaPaquetes)
                {
                    foreach (Fichero fic in paq.ListaFicheros)
                    {
                        if (fic.EstadoDescarga == Estado.Completado)
                        {
                            listaFicherosEliminar.Add(fic);
                        }
                    }
                }
            }
            foreach (Fichero fic in listaFicherosEliminar)
            {
                Log.WriteDebug("Deleting file " + fic.NombreFichero);
                Eliminar(fic, false, false);
            }
            RefreshListaDescargas(true);
        }


        private void About_Click(object sender, EventArgs e)
        {

            var ventanaError = new Credits();
            ventanaError.Text = Language.GetText("About");
            ventanaError.ShowDialog();
            ventanaError.Dispose();

        }

        internal void FAQ_Click(object sender, EventArgs e)
        {
            string codIdi = Language.GetCurrentLanguageCode().ToUpperInvariant();
            if (codIdi.StartsWith("ES"))
            {
                Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("FAQ_LINK_ES"));
            }
            else if (codIdi.StartsWith("FR"))
            {
                Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("FAQ_LINK_FR"));
            }
            else
            {
                Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("FAQ_LINK_EN"));
            }
        }

        private void CheckUpdates_Click(object sender, EventArgs e)
        {
            // F8: manual check must actually query and show the result (was: open releases page).
            Task.Run(() =>
                {
                    string url = null;
                    string version = null;
                    Updater.ComprobarVersionMegadownloader(ref url, ref version);
                    Log.WriteWarning("Manual version check requested");

                    BeginInvoke(new Action<string, string>((resUrl, resVersion) =>
                    {
                        // F8: bypass ApplyUpdateCheckResult's Config.CheckUpdates gate so a manual
                        // click always shows something, even when periodic checks are disabled.
                        bool available = !string.IsNullOrEmpty(resUrl) && !string.IsNullOrEmpty(resVersion);
                        if (available)
                        {
                            UrlNuevaVersionMegadownloader = resUrl;
                            VersionNuevaVersionMegadownloader = resVersion;
                            btnUpdate.Visible = true;
                            // Reuse the periodic reminder (15s) but ignore UpdateSkipVersion for a manual click.
                            ProximoAvisoActualizacion = DateTime.Now.AddSeconds(15d);
                        }
                        else
                        {
                            // No existing "already up to date" key in the XML: reuse the "Check for updates" title.
                            MessageBox.Show(this, Language.GetText("Check for updates"), Language.GetText("Information"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }), url, version);
                });
        }


        private void Collaborate_Click(object sender, EventArgs e)
        {
            string codIdi = Language.GetCurrentLanguageCode().ToUpperInvariant();
            if (codIdi.StartsWith("ES"))
            {
                Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("COLLABORATE_LINK_ES"));
            }
            else
            {
                Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("COLLABORATE_LINK_EN"));
            }
        }

        /// <summary>
    /// Botón de "Configuración"
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <remarks></remarks>
        private void btnConfig_Click(object sender, EventArgs e)
        {
            var frmName = new Configuration();
            frmName.MainForm = this;
            frmName.Config = Config;
            frmName.RequiereConfiguracion = false;
            frmName.ShowDialog();
            // Hasta que no se cierre la ventana no continuamos la ejecución
            frmName.Dispose();
        }


        private void VerStreaming_Click(object sender, EventArgs e)
        {
            if (IsFormAlreadyOpen(typeof(StreamingForm)) is null)
            {
                var frmName = new StreamingForm();
                frmName.MainForm = this;
                frmName.Config = Config;
                frmName.Show();
            }
        }
        private void CreateStegano_Click(object sender, EventArgs e)
        {
            if (IsFormAlreadyOpen(typeof(Stegano.SteganoWizardSave)) is null)
            {
                var frmName = new Stegano.SteganoWizardSave();
                frmName.MainForm = this;
                frmName.Config = Config;
                frmName.Show();
            }
        }

        private void UseStegano_Click(object sender, EventArgs e)
        {
            OpenSteganoWizard();
        }

        public Stegano.SteganoWizardLoad OpenSteganoWizard()
        {
            Stegano.SteganoWizardLoad f = (Stegano.SteganoWizardLoad)IsFormAlreadyOpen(typeof(Stegano.SteganoWizardLoad));
            if (f is null)
            {
                f = new Stegano.SteganoWizardLoad();
                f.MainForm = this;
                f.Config = Config;
                f.Show();
            }
            return f;
        }


        private void LibraryManager_Click(object sender, EventArgs e)
        {
            if (!Config.ServidorStreamingActivo)
            {
                MessageBox.Show(Language.GetText("Streaming server not activated"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                Process.Start(StreamingHelper.LibraryManagerURL(Config.ServidorStreamingPuerto, true));
            }
        }

        private void SeeLibraryManager_Click(object sender, EventArgs e)
        {
            if (!Config.ServidorStreamingActivo)
            {
                MessageBox.Show(Language.GetText("Streaming server not activated"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                Process.Start(StreamingHelper.LibraryManagerURL(Config.ServidorStreamingPuerto, false));
            }
        }

        /// <summary>左栏快捷入口:直接复用既有菜单命令的事件处理器,不复制业务逻辑,
    /// 保证侧栏与菜单两条路径行为完全一致(避免逻辑分叉)。</summary>
        private void navQuickExtractButton_Click(object sender, EventArgs e)
        {
            VerDescompresor_Click(sender, e);
        }

        private void navQuickLibraryButton_Click(object sender, EventArgs e)
        {
            SeeLibraryManager_Click(sender, e);
        }

        private void navQuickLogsButton_Click(object sender, EventArgs e)
        {
            VerLogs_Click(sender, e);
        }

        private void VerLogs_Click(object sender, EventArgs e)
        {
            string PathLog = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader");

            if (!System.IO.Directory.Exists(PathLog))
            {
                System.IO.Directory.CreateDirectory(PathLog);
            }

            Process.Start(PathLog);
        }

        private void CodificarEnlaces_Click(object sender, EventArgs e)
        {
            if (IsFormAlreadyOpen(typeof(EncodeLinksForm)) is null)
            {
                var frmName = new EncodeLinksForm();
                frmName.MainForm = this;
                frmName.Show();
            }
        }

        private void GenerateELC_Click(object sender, EventArgs e)
        {
            // P2-4: 类型不匹配 —— 这里查重用的是 EncodeLinksForm,开的却是 ELCForm。
            // 后果:开着"Encode links"时点"Generate ELC",条件恒为假 → 窗口永远打不开,
            // 且没有任何提示;已经开着 ELC 时再点一次,查重的仍是 EncodeLinksForm
            // (永远是 Nothing)→ 又开一个 ELC 窗口,每个都起一条常驻后台线程。
            // 全仓 11 处 IsFormAlreadyOpen(GetType(...)) 里唯一的一处错配,改为按实际
            // 要打开的窗体类型查重。
            if (IsFormAlreadyOpen(typeof(ELCForm)) is null)
            {
                var frmName = new ELCForm();
                frmName.MainForm = this;
                frmName.Show();
            }
        }

        public void StartDownload()
        {
            if (!ScheduleAllowsStart(true))
                return;
            // P3-11: 配额熔断守卫此前只挂在工具栏 Play 上,AddLinks 的"立即下载"直接
            // 调 StartDownload 绕过了它 —— 熔断期里一个任务都不会开,状态栏却显示
            // "Downloading",配额 Toast 也不弹,用户完全得不到解释。
            // 守卫下沉到 StartDownload,所有入口(工具栏/AddLinks/其它调用方)行为一致。
            if (MegaQuotaManager.IsQuarantined())
            {
                ShowQuotaBlockedToast();
                return;
            }
            lock (scheduleActionLock)
            {
                scheduleController.MarkManualStart();
                QuitarPausasIndividuales();
                EstadoAplicacion = TipoEstadoAplicacion.Descargando;
                ThrottledStreamController.GetController().Continuar();
            }
        }
        public void PauseDownload()
        {
            if (System.Threading.Volatile.Read(ref _startupQueueLoading))
                return;
            lock (scheduleActionLock)
            {
                scheduleController.MarkManualPause(Config.DownloadSchedule, DateTime.Now);
                QuitarDescargasIndividuales();
                EstadoAplicacion = TipoEstadoAplicacion.Pausa;
                ThrottledStreamController.GetController().Abortar();
            }
        }
        public void StopDownload()
        {
            if (System.Threading.Volatile.Read(ref _startupQueueLoading))
                return;
            lock (scheduleActionLock)
            {
                scheduleController.MarkManualPause(Config.DownloadSchedule, DateTime.Now);
                QuitarDescargasIndividuales();
                EstadoAplicacion = TipoEstadoAplicacion.Parado;
                ThrottledStreamController.GetController().Abortar();
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            // 熔断期点开始此前零反馈(调度器静默不开新任务)。给明确提示,不改状态。
            if (MegaQuotaManager.IsQuarantined())
            {
                ShowQuotaBlockedToast();
                return;
            }
            StartDownload();
        }

        /// <summary>配额熔断提示。工具栏 Play 与"立即下载"(AddLinks)共用同一出口,
    /// 免得两边的文案/倒计时格式走样。</summary>
        private void ShowQuotaBlockedToast()
        {
            var qrem = MegaQuotaManager.GetRemaining();
            string msg = Language.GetText("Quota_Error");
            if (qrem.HasValue)
            {
                msg += " (" + Language.GetText("Quota_Status").Replace("%T%", FormatQuotaRemaining(qrem.Value)) + ")";
            }
            ToastForm.ShowToast(this, msg);
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            PauseDownload();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopDownload();
        }


        private void btnUpdate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(UrlNuevaVersionMegadownloader))
                return;
            // B3:更新 URL 只允许 https。version.xml 若被投毒/中间人替换,http 明文链接
            // 会把用户送到钓鱼站;Process.Start 直接打开,必須掐掉降级。
            if (!UrlNuevaVersionMegadownloader.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                Log.WriteError("Update URL rejected (not https): " + Log.Redact(UrlNuevaVersionMegadownloader));
                return;
            }
            string Key = Fichero.ExtraerFileKey(UrlNuevaVersionMegadownloader);
            string LinkToAdd = UrlNuevaVersionMegadownloader;
            if (string.IsNullOrEmpty(Key) && URLExtractor.EsUrlAcortador(UrlNuevaVersionMegadownloader))
            {
                // P2-5: resolve the short link and use the RESOLVED URL for both the key
                // extraction and the download. The old code assigned `url` but then
                // re-extracted the key from the original short link (which never contains
                // one), so the key stayed empty and clicking Update opened the browser
                // instead of queueing the download. Keep the https-only policy for the
                // resolved URL too.
                string url = Conexion.ObtenerUrlDesdeAcortador(UrlNuevaVersionMegadownloader);
                if (!string.IsNullOrEmpty(url) && url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    LinkToAdd = url;
                    Key = Fichero.ExtraerFileKey(url);
                }
            }

            if (string.IsNullOrEmpty(Key))
            {
                // Parece que no es un link de mega, será un link directo al ejecutable...
                Process.Start(UrlNuevaVersionMegadownloader);
            }
            else
            {
                AgregarLink(LinkToAdd, "MegaDownloader v" + VersionNuevaVersionMegadownloader, true, false);
            }
        }

        private void AbrirEnCarpetaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ListaDescargas.SelectedObjects is null)
                return;
            string Ruta = "";
            foreach (object selobject in ListaDescargas.SelectedObjects)
            {
                if (selobject is Fichero)
                {
                    Ruta = ((Fichero)selobject).RutaLocal;
                }
                else if (selobject is Paquete)
                {
                    Ruta = ((Paquete)selobject).RutaLocal;
                }
            }
            // 未选中任何条目(或条目尚无本地路径)时直接退出,避免弹出误导性的空目录错误
            if (string.IsNullOrEmpty(Ruta))
                return;
            if (System.IO.Directory.Exists(Ruta))
            {
                Process.Start(Ruta);
            }
            else
            {
                MessageBox.Show(Language.GetText("Directory %D% does not exist").Replace("%D%", Ruta), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void ResetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (object obj in ListaDescargas.SelectedObjects)
            {
                if (obj is Paquete)
                {
                    foreach (Fichero fic in ((Paquete)obj).ListaFicheros)
                    {
                        if (fic.DescargaEstado() == Estado.Erroneo)
                        {
                            Log.WriteDebug("Reseting file " + fic.NombreFichero);
                            fic.ResetearDescarga();
                            fic.SetDescargaEstado = Estado.EnCola;
                        }
                    }
                }
                else if (obj is Fichero)
                {
                    Fichero fic = (Fichero)obj;
                    if (fic.DescargaEstado() == Estado.Erroneo)
                    {
                        Log.WriteDebug("Reseting file " + fic.NombreFichero);
                        // 与包分支保持一致:单文件也必须 ResetearDescarga 清理分块/已下字节/错误描述,
                        // 否则只改回 EnCola,残留的 .part 与错误状态会让它很快又变回 Erroneo
                        fic.ResetearDescarga();
                        fic.SetDescargaEstado = Estado.EnCola;
                    }
                }
            }

        }


        private void VerProgresoDescompresionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsFormAlreadyOpen(typeof(Descompresor)) is null)
            {
                var frmName = new Descompresor();
                frmName.Show();
            }
        }

        private void VerErrorToolStripMenuItem_Click(object sender, EventArgs e)
        {

            var ht = new HashSet<string>(); // Evitamos repetidos (por ejemplo si seleccionamos un paquete y sus ficheros, saldrían los errores 2 veces)
            foreach (object obj in ListaDescargas.SelectedObjects)
            {
                if (obj is Paquete)
                {
                    foreach (Fichero fic in ((Paquete)obj).ListaFicheros)
                    {
                        if (fic.DescargaEstado() == Estado.Erroneo && !string.IsNullOrWhiteSpace(fic.DescripcionError))
                        {
                            ht.Add(fic.DescripcionError);
                        }
                    }
                }
                else if (obj is Fichero)
                {
                    Fichero fic = (Fichero)obj;
                    if (fic.DescargaEstado() == Estado.Erroneo && !string.IsNullOrWhiteSpace(fic.DescripcionError))
                    {
                        ht.Add(fic.DescripcionError);
                    }

                }
            }
            string msg = "";
            foreach (string Str in ht)
                msg += Str + Constants.vbNewLine + Constants.vbNewLine;
            msg = msg.Trim();
            // RC:老队列重启后描述为空时不再弹空白窗,给可操作 fallback。
            if (string.IsNullOrWhiteSpace(msg))
            {
                msg = Language.GetText("Quota_Error");
                if (string.IsNullOrWhiteSpace(msg) || msg == "Quota_Error")
                {
                    msg = "No error details available (e.g. queue saved by an older version). Please retry the download; a fresh error will include details.";
                }
            }


            var ventanaError = new PantallaMsg();
            ventanaError.Text = Language.GetText("Error information");
            ventanaError.TextoError = msg;
            ventanaError.ShowDialog();
            ventanaError.Dispose();
        }

        private void VerLinksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            VerLinks(false, false);
        }
        private void VerLinksDescToolStripMenuItem_Click(object sender, EventArgs e)
        {
            VerLinks(true, false);
        }
        private void OcultarEnlacesImagenMenuItem_Click(object sender, EventArgs e)
        {
            VerLinks(true, true);
        }

        private void VerLinks(bool DescripcionFichero, bool stegano)
        {
            var ht = new HashSet<string>(); // Evitamos repetidos (por ejemplo si seleccionamos un paquete y sus ficheros, saldrían los links 2 veces)
            foreach (object obj in ListaDescargas.SelectedObjects)
            {
                if (obj is Paquete)
                {
                    foreach (Fichero fic in ((Paquete)obj).ListaFicheros)
                    {
                        if (DescripcionFichero)
                        {
                            var ficForDesc = fic; ht.Add(GetFullFileDesc(ref ficForDesc));
                        }
                        else
                        {
                            ht.Add(fic.LinkVisible ? fic.URL : Fichero.HIDDEN_LINK_DESC);
                        }
                    }
                }
                else if (obj is Fichero)
                {
                    Fichero fic = (Fichero)obj;
                    if (DescripcionFichero)
                    {
                        var ficForDesc = fic; ht.Add(GetFullFileDesc(ref ficForDesc));
                    }
                    else
                    {
                        ht.Add(fic.LinkVisible ? fic.URL : Fichero.HIDDEN_LINK_DESC);
                    }
                }
            }
            var msg = new System.Text.StringBuilder();

            bool order = DescripcionFichero;

            if (order)
            {
                foreach (string Str in from s in ht
                                       orderby s
                                       select s)
                    msg.Append(Str + Constants.vbNewLine + (DescripcionFichero ? Constants.vbNewLine : string.Empty));
            }
            else
            {
                foreach (string Str in ht)
                    msg.Append(Str + Constants.vbNewLine + (DescripcionFichero ? Constants.vbNewLine : string.Empty));
            }

            if (stegano)
            {

                var frm = IsFormAlreadyOpen(typeof(Stegano.SteganoWizardSave));
                // Si ya está abierta la pantalla no la volvemos a abrir
                if (frm is null)
                {
                    var frmStegano = new Stegano.SteganoWizardSave();
                    frmStegano.MainForm = this;
                    frmStegano.Config = Config;
                    frmStegano.txtLinks.Text = msg.ToString().Trim();
                    frmStegano.Show();
                }
                else
                {
                    Stegano.SteganoWizardSave frmStegano = (Stegano.SteganoWizardSave)frm;
                    if (string.IsNullOrEmpty(frmStegano.txtLinks.Text))
                    {
                        frmStegano.txtLinks.Text = msg.ToString().Trim();
                    }
                    else
                    {
                        frmStegano.txtLinks.Text += Constants.vbNewLine + msg.ToString().Trim();
                    }
                    frmStegano.Focus();
                }
            }

            else
            {

                var ventanaError = new PantallaMsg();
                ventanaError.Text = Language.GetText("Links");
                ventanaError.TextoError = msg.ToString().Trim();
                ventanaError.MostrarCodificarEnlaces = true;
                ventanaError.ShowDialog(this);
                ventanaError.Dispose();

            }

        }

        private string GetFullFileDesc(ref Fichero fic)
        {
            var str = new System.Text.StringBuilder();
            str.Append(fic.DescargaNombre());
            if (fic.TamanoBytes > 0L)
            {
                str.Append(" (").Append(PintarTamano(fic.TamanoBytes)).Append(")");
            }
            str.Append(Constants.vbNewLine);
            if (fic.LinkVisible)
            {
                str.Append(fic.URL);
            }
            else
            {
                str.Append(Fichero.HIDDEN_LINK_DESC);
            }
            return str.ToString();
        }

        private void PropiedadesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ListaDescargas.SelectedObject is IDescarga)
            {
                var v = new PropiedadesDescarga();
                v.Descarga = (IDescarga)ListaDescargas.SelectedObject;
                v.ShowDialog();

                v.Dispose();
            }
        }

        private void PausarStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (object o in ListaDescargas.SelectedObjects)
            {
                if (o is Fichero && ((Fichero)o).DescargaEstado() == Estado.Descargando)
                {
                    PonerFicheroEnPausa((Fichero)o);
                }
            }
        }

        private void ForceDownloadStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (object o in ListaDescargas.SelectedObjects)
            {
                // ⑧:红字(Erroneo)此前被拦,点 Force 零反馈。放行并由 ForzarDescarga 重置后强制起。
                if (o is Fichero && ((Fichero)o).DescargaEstado() == Estado.Pausado | ((Fichero)o).DescargaEstado() == Estado.EnCola | ((Fichero)o).DescargaEstado() == Estado.Erroneo)


                {
                    ForzarDescarga((Fichero)o);
                }
            }
        }

        #endregion

        #region Agregar enlaces

        public async void ComprobarYAgregarLinks(string Texto, bool ExtraerURLs, bool EsconderLinks, bool clipboardInput = true)
        {

            if (IsDisposed || Cerrando)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ComprobarYAgregarLinks(Texto, ExtraerURLs, EsconderLinks, clipboardInput)));
                return;
            }
            try
            {
                // Capture the copy-time focus before background parsing can yield to another app.
                AddLinks inbox = IsFormAlreadyOpen(typeof(AddLinks)) as AddLinks;
                if (clipboardInput && inbox is not null && inbox.ContainsFocus)
                    return;
                if (!await WaitForStartupQueueAsync())
                    return;
                var parsed = await Task.Run(() => Tuple.Create(URLExtractor.ExtraerConfiguracionELC(Texto), URLExtractor.ExtraerURLs(Texto)));
                if (IsDisposed || Cerrando)
                    return;

                var frm = IsFormAlreadyOpen(typeof(AddLinks));
                // Si ya está abierta la pantalla de agregar link no la volvemos a abrir
                if (frm is null)
                {

                    // Si tenemos abierta la pantalla de "ver links", seguramente copiemos los links así que no queremos que salte
                    // ToastForm deliberately NOT listed: it is a 2-second notification with no links
                    // in it, and blocking on it silently swallowed whatever the user copied right
                    // after adding a package (which is exactly when the toast is showing).
                    var formsDiscarded = new List<Type>();
                    formsDiscarded.Add(typeof(PantallaMsg));
                    formsDiscarded.Add(typeof(ELCForm));
                    formsDiscarded.Add(typeof(EncodeLinksForm));
                    if (clipboardInput)
                    {
                        foreach (Type t in formsDiscarded)
                        {
                            frm = IsFormAlreadyOpen(t);
                            if (frm is not null)
                            {
                                return;
                            }
                        }
                    }

                    var ConfigsELC = parsed.Item1;
                    if (ConfigsELC is not null && ConfigsELC.Count > 0)
                    {
                        var Helper = new ELCAccountHelper(ref Config);
                        var argMainForm = this;
                        if (Helper.ImportConfig(ConfigsELC))
                        {
                            Helper.SaveToConfig(ref Config);
                            // P0-1: SaveToConfig only updates the in-memory list. Without a forced
                            // save the imported accounts never reach disk (the background periodic
                            // GuardarXML(False) compares a snapshot taken before the ELCAccounts
                            // node is appended, so it sees "no change" and skips the write).
                            Config.GuardarXML(true);
                        }
                        Helper.Dispose();
                    }


                    var URLs = parsed.Item2;
                    if (URLs is not null && URLs.Count > 0)
                    {

                        // Damos foco a la ventana
                        Activate();

                        AgregarLink(Texto, string.Empty, ExtraerURLs, EsconderLinks, parsed.Item2);
                    }
                }

                else if (frm.GetType().FullName == typeof(AddLinks).FullName)
                {
                    // Añadimos links a la ventana ya abierta
                    AddLinks frmName = (AddLinks)frm;
                    if (frmName.AgregarEnlaces(Texto, false, ExtraerURLs, EsconderLinks, parsed.Item2))
                    {
                        frmName.PonerFoco();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Importing clipboard links failed: " + Log.SafeException(ex));
            }
        }

        private void AgregarLink()
        {
            AgregarLink(string.Empty, string.Empty, true, false);
        }

        private async void AgregarLink(string Url, string NombrePaquete, bool ExtraerURLs, bool EsconderLinks, List<string> parsed = null)
        {
            if (!await WaitForStartupQueueAsync())
                return;
            // This method is Async Sub: when the startup-completion task is not yet set
            // (for example command-line URLs processed during CompleteStartup, before
            // _startupQueueCompletion is signalled), the Await above yields and the rest of
            // this body runs later on the UI thread, OUTSIDE the caller's Try/Catch. Any
            // exception here would then escape the Async Sub as an unhandled async-void
            // exception (logged, but destabilising the message loop). Keep it contained.
            try
            {
                Paquete importedPackage;
                bool openStegano;
                using (var frmName = new AddLinks())
                {
                    frmName.Main = this;

                    frmName.Config = Config;
                    if (!string.IsNullOrEmpty(Url))
                    {
                        frmName.AgregarEnlaces(Url, true, ExtraerURLs, EsconderLinks, parsed);
                    }
                    if (!string.IsNullOrEmpty(NombrePaquete))
                    {
                        frmName.txtNombre.Text = NombrePaquete;
                    }

                    frmName.ShowDialog(this);

                    importedPackage = frmName.LastImportedPackage;
                    openStegano = frmName.OpenSteganoLoadOnExit;
                }

                if (importedPackage is not null)
                    RevealImportedPackage(importedPackage);


                // Ñapa para abrir el cuadro de carga esteganografica... necesario porque AddLinks contiene un botón a ese cuadro :/
                if (openStegano)
                    OpenSteganoWizard();
            }
            catch (Exception ex)
            {
                Log.WriteError("Adding links failed: " + Log.SafeException(ex));
            }
        }


        // Append params
        public void ProcessArgs(string[] args)
        {
            if (args is null || args.Length == 0)
                return;
            if (DeferStartupArguments(args))
                return;

            // 只有首个参数确实是本程序的可执行文件路径时才剥掉。此前用 Contains(assemblyName),
            // 于是 "D:\MegaDownloader\links.dlc" 这类路径也被当成程序名剥掉 —— 第二实例传进来的
            // .dlc/.elc 被静默丢弃(GetParameters 已经剥过一次 exe)。
            string exePath = System.Windows.Forms.Application.ExecutablePath;
            if (string.Equals(args[0], exePath, StringComparison.OrdinalIgnoreCase))
            {
                // First argument is app, ignore
                args = args.ToList().Skip(1).ToArray();
            }
            if (args.Length == 0)
                return;


            var URLlist = new HashSet<string>();
            var DLCList = new HashSet<string>();
            foreach (string arg in args)
            {

                var ConfigsELC = URLExtractor.ExtraerConfiguracionELC(arg);
                if (ConfigsELC is not null && ConfigsELC.Count > 0)
                {
                    var Helper = new ELCAccountHelper(ref Config);
                    var argMainForm = this;
                    if (Helper.ImportConfig(ConfigsELC))
                    {
                        Helper.SaveToConfig(ref Config);
                        // P0-1: same as ComprobarYAgregarLinks — persist imported accounts now.
                        Config.GuardarXML(true);
                    }
                    Helper.Dispose();
                }

                foreach (string url in URLExtractor.ExtraerURLs(arg))
                    URLlist.Add(url);
                // ⑦:双击/命令行 .elc 此前无分支静默无操作;多 .dlc 只取 [0]。统一排队逐个导入。
                if (System.IO.File.Exists(arg) && (arg.ToUpper().EndsWith(".DLC") || arg.ToUpper().EndsWith(".ELC")))
                {
                    DLCList.Add(arg);
                }
            }
            if (URLlist.Count > 0)
            {
                ComprobarYAgregarLinks(string.Join(Constants.vbNewLine, URLlist.ToArray()), true, false, false);
            }
            foreach (string dlcPath in DLCList)
                AddDLC(dlcPath);

        }



        #endregion

        #region Agregar DLCs

        private bool DLCProcessing = false; // Indica si ya hay un proceso tratando el DLC
        private string DLCPath = string.Empty;
        private List<string> DLCResults = null;
        private Exception DLCErrorProcessing = null;
        // ⑥⑦:多文件排队。AddDLC 原来 DLCProcessing=True 时直接丢弃,拖多个/传多个只进第一个。
        private Queue<string> DLCQueue = new Queue<string>();


        private void AddDLC(string DLCFilePath)
        {
            // 用户在文件对话框点了"取消"时传空串:静默退出,不弹"The path is not valid"假错误
            if (string.IsNullOrWhiteSpace(DLCFilePath))
                return;
            DLCQueue.Enqueue(DLCFilePath);
            PumpDLCQueue();
        }

        /// <summary>⑥⑦:空闲且队列非空时取下一个开工;保证多 .elc/.dlc 逐个导入,不再吞文件。</summary>
        private void PumpDLCQueue()
        {
            if (DLCProcessing)
                return;
            if (DLCQueue.Count == 0)
                return;
            DLCProcessing = true;
            var Thread = new System.Threading.Thread(StartProcessDLC);
            // 第18项:后台线程,进程退出时不被前台线程吊住(否则队列处理中退出会挂死)
            Thread.IsBackground = true;
            DLCPath = DLCQueue.Dequeue();
            Thread.Start();
        }

        private void FinishProcessing()
        {

            var ErrorProcessing = DLCErrorProcessing;
            var LocalURLList = DLCResults;
            DLCResults = null;
            DLCErrorProcessing = null;

            if (ErrorProcessing is not null)
            {
                MessageBox.Show(Language.GetText("The DLC could not be loaded. Reason: %REASON").Replace("%REASON", ErrorProcessing.Message), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (LocalURLList is not null)
            {
                string URLstr = string.Join(Constants.vbNewLine, LocalURLList);
                if (!string.IsNullOrEmpty(URLstr))
                {
                    // Use the same asynchronous inbox for explicit container imports.
                    // An existing file-selection page keeps this batch deferred.
                    ComprobarYAgregarLinks(URLstr, true, false, false);
                }
                else if (ErrorProcessing is null) // A decoder error was already shown above.
                {
                    MessageBox.Show(Language.GetText("The DLC has no valid Mega links"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            // ⑥⑦:当前处理完顺手起下一个排队文件。
            PumpDLCQueue();
        }
        private void StartProcessDLC()
        {
            var Thread = new System.Threading.Thread(ProcessDLC);
            Thread.IsBackground = true;
            Thread.Start();
            Action<bool> d;

            if (!Thread.Join(new TimeSpan(0, 0, 0, 30))) // 30 seconds timeout
            {
                // Never call Thread.Abort(): it can interrupt I/O mid-flight and corrupt state.
                // Mark the result as failed; the worker (a local file read) finishes on its own.
                DLCProcessing = false;
                DLCResults = null;
                DLCErrorProcessing = new ApplicationException("30s timeout");
            }
            DLCPath = string.Empty;
            d = new Action<bool>((x) => FinishProcessing());
            // 第18项:窗口关闭后 handle 可能已销毁,裸 Invoke 会抛
            // ObjectDisposedException/InvalidOperationException 且无人接管;窗口没了直接跳过 UI。
            try
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    Log.WriteWarning("StartProcessDLC: form handle gone, skipping UI finish");
                    DLCProcessing = false;
                }
                else
                {
                    Invoke(d, true);
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("StartProcessDLC: Invoke failed (" + ex.Message + "), skipping UI finish");
                DLCProcessing = false;
            }
        }
        private void ProcessDLC()
        {
            try
            {
                string Path = DLCPath;

                if (string.IsNullOrEmpty(Path))
                {
                    throw new ApplicationException(Language.GetText("The path is not valid"));
                }

                if (!System.IO.File.Exists(Path))
                {
                    throw new ApplicationException(Language.GetText("The path is not valid"));
                }

                if (Path.ToLower().EndsWith(".elc"))
                {
                    var ELC = new System.Text.StringBuilder();
                    using (var t = new System.IO.StreamReader(Path))
                    {
                        ELC.Append(t.ReadToEnd());
                    }
                    if (ELC.Length == 0)
                    {
                        throw new ApplicationException(Language.GetText("ELC file is empty"));
                    }
                    DLCResults = new List<string>();
                    DLCResults.Add("mega://" + URLExtractor.SERVERENCODEDPREFIX + ELC.ToString());
                }
                else
                {
                    // DLC decryption service (dcrypt.it) has been discontinued.
                    throw new ApplicationException(Language.GetText("DLC format is no longer supported (dcrypt.it service discontinued). Please use .elc files instead."));
                }
            }

            catch (Exception ex)
            {
                Log.WriteError("Error processing DLC: " + ex.ToString());
                DLCErrorProcessing = ex;
            }
            finally
            {
                if (DLCProcessing)
                    DLCProcessing = false;
            }
        }

        #endregion


    }
}