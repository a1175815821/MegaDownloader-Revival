using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace MegaDownloader
{

    public partial class AddLinks
    {

        public AddLinks()
        {
            // Build the complete hierarchy before the first automatic font/DPI scaling pass.
            SuspendLayout();
            try
            {
                InitializeComponent();
                UiStyles.InitializeForm(this);
                InitAddLinksLayout();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        public Main Main;
        public Configuracion Config;
        public string HiddenLinks = string.Empty;
        public Paquete LastImportedPackage { get; private set; }

        // P0-6 UI:链接计数 debounce(300ms,避免大文本每次击键跑正则)+输入框水印
        private Timer __linkCountTimer;

        private Timer _linkCountTimer
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return __linkCountTimer;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (__linkCountTimer != null)
                {
                    __linkCountTimer.Tick -= _linkCountTimer_Tick;
                }

                __linkCountTimer = value;
                if (__linkCountTimer != null)
                {
                    __linkCountTimer.Tick += _linkCountTimer_Tick;
                }
            }
        }
        private Label _cueFallbackLabel = default;

        private const int EM_SETCUEBANNER = 0x1501;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        private void AddLinks_Load(object sender, EventArgs e)
        {
            Translate();
            ThemeManager.ApplyTheme(this);

            txtRuta.Text = Config.RutaDefecto;
            chkCrearDirectorio.Checked = Config.CrearDirectorioPaquete;
            chkUnZip.Checked = Config.ExtraerAutomaticamente;
            chkStartDownload.Checked = true;

            // OpcionesPaquete 常显(历史条件已废弃,见 git)。
            OpcionesPaquete.Visible = true;

            if (Config.MantenerUltimaConfiguracion & UltimaConfiguracionUsada.ExisteUltimaConfiguracion)
            {
                chkCrearDirectorio.Checked = UltimaConfiguracionUsada.CrearDirectorioPaquete;
                chkUnZip.Checked = UltimaConfiguracionUsada.ExtraerAutomaticamente;
                txtRuta.Text = UltimaConfiguracionUsada.RutaDescarga;
                chkStartDownload.Checked = UltimaConfiguracionUsada.IniciarDescarga;
            }

            chkUnZip_CheckedChanged(null, null);

            // P0-6:计数 Timer 挂 components,随窗体释放(本窗体每次打开新建,防泄漏)
            if (components is not null)
            {
                _linkCountTimer = new System.Windows.Forms.Timer(components);
            }
            else
            {
                _linkCountTimer = new System.Windows.Forms.Timer();
            }
            _linkCountTimer.Interval = 300;

            FitAddLinksToWorkingArea(true);
            addLinksLayoutReady = true;
            UpdateAddLinksInputHeight();

        }

        public bool AgregarEnlaces(string texto, bool limpiarContenidoAnterior, bool ExtraerURLs, bool EsconderLinks, List<string> parsed = null)
        {

            if (importBusy || onSelectionPage)
            {
                deferredInputs.Add(Tuple.Create(texto, ExtraerURLs, EsconderLinks, parsed));
                if (preflightView is not null)
                    preflightView.ShowNotice(Language.GetText("AddLinks_Deferred"));
                return false; // Keep the current selection stable; merge new links on return.
            }
            if (parsed is not null && !limpiarContenidoAnterior && !EsconderLinks)
            {
                if (parsed.Count == 0)
                    return false;
                string addition = ExtraerURLs ? string.Join(Constants.vbNewLine, parsed) : texto;
                if (txtLinks.TextLength > 0)
                    txtLinks.AppendText(Constants.vbNewLine);
                txtLinks.AppendText(addition);
                return true;
            }

            string previousText = txtLinks.Text;
            if (!limpiarContenidoAnterior)
            {
                texto = txtLinks.Text + Constants.vbNewLine + texto;
            }

            var URLs = parsed is not null && !EsconderLinks ? parsed : URLExtractor.ExtraerURLs(texto);
            if (URLs is not null && URLs.Count > 0)
            {

                if (ExtraerURLs)
                {
                    texto = string.Join(Constants.vbNewLine, URLs);
                    if (parsed is not null && !limpiarContenidoAnterior)
                        texto = previousText + Constants.vbNewLine + texto;

                }

                if (EsconderLinks)
                {

                    // Lo guardamos como ELC para que los links no sean visibles
                    var lHidden = new List<ServerEncoderLinkHelper.MegaLink>();

                    URLs = URLExtractor.ExtraerURLs(texto);
                    if (URLs is not null && URLs.Count > 0)
                    {
                        foreach (var url in URLs)
                        {
                            texto = texto.Replace(url, Fichero.HIDDEN_LINK_DESC);

                            var h = new ServerEncoderLinkHelper.MegaLink();
                            h.FileID = URLExtractor.ExtraerFileID(url);
                            h.FileKey = URLExtractor.ExtraerFileKey(url);
                            h.MegaFolder = URLExtractor.IsMegaFolder(url);
                            h.SubFolderID = URLExtractor.ExtraerSubFolderID(url);
                            h.SubFileID = URLExtractor.ExtraerSubFileID(url);
                            lHidden.Add(h);

                        }
                    }

                    // P1-3:多批隐身链接必须换行分隔,否则拼成 mega://elc?AAAAmega://elc?BBBB,
                    // 正则把第二段吃掉导致静默丢失(同文件 URLstr 拼接即用 vbNewLine)。
                    if (HiddenLinks.Length > 0)
                        HiddenLinks += Constants.vbNewLine;
                    HiddenLinks += "mega://elc?" + ServerEncoderLinkHelper.ServerEncode("HIDDEN", lHidden, ref Config);

                }

                txtLinks.Text = texto;

                return true;
            }
            else
            {
                return false;
            }
        }

        private void Translate()
        {
            Text = Language.GetText("Add links");
            OpcionesPaquete.Text = Language.GetText("Options");
            chkUnZip.Text = Language.GetText("Automatic extraction");
            chkCrearDirectorio.Text = Language.GetText("Create directory");
            btnExaminar.Text = Language.GetText("Browse");
            Label2.Text = Language.GetText("Name") + ":";
            Label1.Text = Language.GetText("Path") + ":";
            btnAgregar.Text = Language.GetText("AddLinks_SelectFiles");
            btnWatchOnline.Text = Language.GetText("Watch Online");
            chkStartDownload.Text = Language.GetText("Start download");
            lblPassword.Text = Language.GetText("Password") + ":";
            linkStegano.Text = Language.GetText("Retrieve links from an image");
            // Me.LinkLabel1.Text = Language.GetText("Watch Online help link")
        }

        private void AddLinks_Shown(object sender, EventArgs e)
        {
            TrySetCueBanner();
            UpdateLinkCount();
            PonerFoco();
            if (!string.IsNullOrEmpty(txtLinks.Text))
            {
                btnAgregar.Focus();
            }
            else
            {
                txtLinks.Focus();
            }
        }

        public void PonerFoco()
        {
            // The modal owner keeps this window above Main without making it globally topmost.
            Activate();
        }

        /// <summary>P0-6:输入变化只重启 debounce 计时,真正的正则解析在 Tick 里跑一次。</summary>
        private void txtLinks_TextChanged(object sender, EventArgs e)
        {
            // RC:清空与 HiddenLinks 同步,不等 300ms debounce,否则窗口期内点添加仍用残留建包。
            if (string.IsNullOrWhiteSpace(txtLinks.Text))
                HiddenLinks = string.Empty;
            PreflightInputChanged();
            if (_linkCountTimer is not null)
            {
                _linkCountTimer.Stop();
                _linkCountTimer.Start();
            }
        }

        private void _linkCountTimer_Tick(object sender, EventArgs e)
        {
            _linkCountTimer.Stop();
            UpdateLinkCount();
        }

        private void AddLinks_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
        {
            DisposePreflight();
            try
            {
                if (_linkCountTimer is not null)
                    _linkCountTimer.Dispose();
            }
            catch
            {
            }
        }

        /// <summary>
	/// 	''' P0-6:RichTextBox 水印。EM_SETCUEBANNER 在部分 RichEdit 版本上返回 0,
	/// 	''' 此时使用带主题色的提示 Label，点击交还输入焦点，随 Shown 只建一次。
	/// 	''' </summary>
        private void TrySetCueBanner()
        {
            try
            {
                string cue = Language.GetText("AddLinks_CueBanner");
                bool ok = false;
                if (txtLinks.IsHandleCreated && !string.IsNullOrEmpty(cue))
                {
                    ok = SendMessage(txtLinks.Handle, EM_SETCUEBANNER, 0, cue) != IntPtr.Zero;
                }
                if (!ok)
                {
                    if (_cueFallbackLabel is null || _cueFallbackLabel.IsDisposed)
                    {
                        _cueFallbackLabel = new Label();
                        _cueFallbackLabel.AutoSize = true;
                        _cueFallbackLabel.Enabled = true;
                        _cueFallbackLabel.BackColor = txtLinks.BackColor;
                        // RC:深色下水印此前默认黑字不可见,固定灰字。
                        UiStyles.SetRole(_cueFallbackLabel, UiStyles.Role.Muted);
                        _cueFallbackLabel.Location = new Point(4, 3);
                        _cueFallbackLabel.Text = cue;
                        txtLinks.Controls.Add(_cueFallbackLabel);
                        _cueFallbackLabel.Click += (s, e) => txtLinks.Focus();
                        _cueFallbackLabel.BringToFront();
                    }
                    UpdateAddLinksCueBounds();
                    _cueFallbackLabel.Visible = txtLinks.TextLength == 0;
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("TrySetCueBanner failed: " + ex.ToString());
            }
        }


        private void btnExaminar_Click(object sender, EventArgs e)
        {

            var ExaminarDirectorio = new FolderBrowserDialog();
            ExaminarDirectorio.Description = Language.GetText("Select directory");

            if (ExaminarDirectorio.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtRuta.Text = ExaminarDirectorio.SelectedPath;
            }

            ExaminarDirectorio.Dispose();

        }

        private List<string> ExtraerURLs()
        {
            return URLExtractor.ExtraerURLs(txtLinks.Text + Constants.vbNewLine + HiddenLinks);
        }

        private void btnAgregar_Click(object sender, EventArgs e)
        {
            ShowSelectionPage();
        }

        /// <summary>v2.5 beta: 文件夹读取进度窗(常亮计数 + 取消=丢弃结果)。</summary>
        private class FolderResolveProgressForm : Form
        {

            public bool Cancelled = false;
            public readonly System.Threading.CancellationTokenSource CancelSource = new System.Threading.CancellationTokenSource();
            private readonly Label lbl = new Label();
            private readonly ProgressBar bar = new ProgressBar();
            // 卡住可感知：解析主体是同步阻塞 I/O（单次 60s+），计数可能几分钟不动。
            // 用 1s 滴答刷"已用秒数"，转圈 freeze 时用户能区分"还活着"与"死了"，取消键也敢点。
            // 后缀纯数字+s，无需进语言系统。
            private readonly DateTime _started = DateTime.Now;
            private int _lastCount = 0;
            private System.Windows.Forms.Timer _elapsedTimer;

            [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern void SetWindowTheme(IntPtr hWnd, string appName, string idList);

            public FolderResolveProgressForm()
            {
                _elapsedTimer = new System.Windows.Forms.Timer();
                this.Text = Language.GetText("Add links");
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.ShowInTaskbar = false;
                this.StartPosition = FormStartPosition.CenterParent;
                this.Size = new System.Drawing.Size(380, 130);
                lbl.Left = 12;
                lbl.Top = 12;
                lbl.Width = 340;
                lbl.Height = 40;
                bar.Left = 12;
                bar.Top = 58;
                bar.Width = 250;
                bar.Height = 23;
                bar.Style = ProgressBarStyle.Marquee;
                var btn = new Button();
                btn.Text = Language.GetText("Cancel");
                if (string.IsNullOrEmpty(btn.Text))
                    btn.Text = "Cancel";
                btn.Left = 270;
                btn.Top = 56;
                btn.Width = 82;
                btn.Height = 25;
                btn.Click += OnCancel;
                this.Controls.Add(lbl);
                this.Controls.Add(bar);
                this.Controls.Add(btn);
                ThemeManager.ApplyTheme(this);
                ApplyProgressTheme();
                SetCount(0);
                try
                {
                    _elapsedTimer.Interval = 1000;
                    _elapsedTimer.Start();
                }
                catch
                {
                }

                _elapsedTimer.Tick += _elapsedTimer_Tick;
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing)
                    {
                        try
                        {
                            _elapsedTimer.Stop();
                        }
                        catch
                        {
                        }
                        _elapsedTimer.Dispose();
                        try
                        {
                            CancelSource.Cancel();
                        }
                        catch
                        {
                        }
                        CancelSource.Dispose();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            /// <summary>原生 ProgressBar 在视觉样式下忽略 Back/ForeColor(主列表靠 OLV 自绘才解决)。
        /// 此处去视觉样式走经典绘制,颜色跟主题,Marquee 照常滚动。</summary>
            private void ApplyProgressTheme()
            {
                try
                {
                    if (bar.IsHandleCreated)
                    {
                        SetWindowTheme(bar.Handle, "", "");
                    }
                    bar.BackColor = ThemeManager.GetColor("ControlBack");
                    bar.ForeColor = ThemeManager.GetColor("Selection");
                }
                catch
                {
                }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                ApplyProgressTheme();
            }

            private void OnCancel(object sender, EventArgs e)
            {
                Cancelled = true;
                // 真取消:此前只关窗丢结果,后台把整个文件夹解析跑完,熔断期也在耗 API。
                try
                {
                    CancelSource.Cancel();
                }
                catch
                {
                }
                this.Close();
            }

            public void SetCount(int n)
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<int>(SetCount), n);
                    return;
                }
                _lastCount = n;
                RenderLabel();
            }

            private void _elapsedTimer_Tick(object sender, EventArgs e)
            {
                try
                {
                    if (this.IsDisposed || this.Disposing)
                        return;
                    RenderLabel();
                }
                catch
                {
                }
            }

            private void RenderLabel()
            {
                string @base;
                if (_lastCount <= 0)
                {
                    @base = Language.GetText("Folder_Reading");
                }
                else
                {
                    @base = Language.GetText("Folder_Reading_Count").Replace("%N%", _lastCount.ToString());
                }
                long secs = (long)Math.Round(DateTime.Now.Subtract(_started).TotalSeconds);
                if (secs < 0L)
                    secs = 0L;
                lbl.Text = @base + " (" + secs + "s)";
            }
        }

        /// <summary>P0-2:解析取消/完成的统一复位。ResolveUrlsAsync 被 Add 与 Watch 共用,
    /// 取消分支必须同时复位两个按钮 + _watchResolving,否则 Watch 取消一次就永久锁死。</summary>
        private void ResetResolveButtons()
        {
            _watchResolving = false;
            try
            {
                if (btnAgregar is not null && !btnAgregar.IsDisposed)
                {
                    btnAgregar.Enabled = true;
                    btnAgregar.Text = Language.GetText("AddLinks_SelectFiles");
                }
            }
            catch
            {
            }
            try
            {
                if (btnWatchOnline is not null && !btnWatchOnline.IsDisposed)
                {
                    btnWatchOnline.Enabled = true;
                }
            }
            catch
            {
            }
        }

        private void ResolveUrlsAsync(List<string> URLs, Action<List<URLProcessor.FileURL>, Exception, FolderResolveProgressForm> onDone)
        {
            var dlg = new FolderResolveProgressForm();
            dlg.Show(this);
            var prog = new Progress<int>(n => dlg.SetCount(n));
            var cfg = Config;
            var ct = dlg.CancelSource.Token;
            var ui = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() => URLProcessor.ProcessURLs(URLs, ref cfg, prog, ct), ct).ContinueWith(t =>
    {
        bool wasCancelled = dlg.Cancelled;
        try
        {
            dlg.Close();
        }
        catch
        {
        }
        dlg.Dispose();
        if (IsDisposed)
            return;
        if (wasCancelled)
        {
            ResetResolveButtons();
            return;
        }
        if (t.IsFaulted)
        {
            Exception inner = t.Exception is not null && t.Exception.InnerException is not null
                ? t.Exception.InnerException
                : (Exception)t.Exception;
            onDone((List<URLProcessor.FileURL>)null, inner, (FolderResolveProgressForm)null);
        }
        else if (t.IsCanceled)
        {
            ResetResolveButtons();
        }
        else
        {
            onDone(t.Result, default, default);
        }
    }, ui);
        }

        /// <summary>N-P2-1: in breakthrough mode nothing is quarantined and the Retry-now
    /// banner never appears, so the standard Quota_Error text would promise pause/resume/
    /// retry mechanisms that do not exist in that mode. Shared by OnResolveForAdd and
    /// OnResolveForWatch, where this block was duplicated verbatim.</summary>
        private static string GetQuotaErrorMessage()
        {
            if (!MegaQuotaManager.IsBreakthroughMode())
                return Language.GetText("Quota_Error");
            string quotaMsg = Language.GetText("Quota_Error_Breakthrough");
            if (string.IsNullOrEmpty(quotaMsg) || quotaMsg == "Quota_Error_Breakthrough")
            {
                quotaMsg = "MEGA transfer quota exceeded while reading the shared folder (HTTP 509 / -17). Breakthrough mode is on: the queue is not paused automatically - change IP or proxy and try again.";
            }
            return quotaMsg;
        }


        private async Task<bool> FinishAddPackageAsync(List<URLProcessor.FileURL> urls)
        {
            string name = txtNombre.Text;
            string path = txtRuta.Text;
            bool create = chkCrearDirectorio.Checked;
            bool extract = chkUnZip.Checked;
            string password = txtPassword.Text;
            var token = importCancellation.Token;
            var progress = new PreflightProgress.ItemReporter(count => System.Threading.Volatile.Write(ref importCompleted, count));
            var package = await Task.Run(() => DownloadPackageBuilder.Build(urls, name, path, create, extract, password, token, progress), token);
            importCommitting = true;
            RefreshImportProgress();
            // Give the final stage a paint turn before the synchronous queue publication.
            await Task.Yield();
            if (preflightDisposed || token.IsCancellationRequested)
            {
                foreach (Fichero @file in package.ListaFicheros)
                    @file.Dispose();
                token.ThrowIfCancellationRequested();
                return false;
            }
            try
            {
                Main.AgregarPaquete(package, false, true);
            }
            catch (Exception ex)
            {
                if (!Main.IsPackageQueued(package))
                {
                    foreach (Fichero @file in package.ListaFicheros)
                        @file.Dispose();
                    throw;
                }
                // Queue ownership has already transferred. A later refresh/notification
                // failure must not report this batch as uncommitted or dispose its files.
                Log.WriteError("Imported tasks were queued, but post-add updates failed: " + Log.SafeException(ex));
                Main.SaveImportedQueueInBackground();
            }
            LastImportedPackage = package;
            return true;
        }


        // 在线观看解析进行中标记：ResolveUrlsAsync 是异步的，连点/双击会并发跑两次
        // 解析，同样的链接被解析两次还可能弹两个 VLC。进行中直接忽略新的点击。
        private bool _watchResolving = false;

        private void btnWatchOnline_Click(object sender, EventArgs e)
        {
            if (!Config.ServidorStreamingActivo)
            {
                MessageBox.Show(Language.GetText("Streaming server not activated"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(Config.VLCPath))
            {
                MessageBox.Show(Language.GetText("Missing VLC Path"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var URLs = ExtraerURLs();
            if (URLs.Count == 0)
            {
                MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_watchResolving)
                return;
            _watchResolving = true;
            btnWatchOnline.Enabled = false;
            btnAgregar.Enabled = false;

            ResolveUrlsAsync(URLs, OnResolveForWatch);
        }

        private void OnResolveForWatch(List<URLProcessor.FileURL> URLs2, Exception err, FolderResolveProgressForm unused)
        {
            try
            {
                if (err is not null)
                {
                    if (err is MegaQuotaExceededException)
                    {
                        MessageBox.Show(GetQuotaErrorMessage(), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else if (err is ApplicationException)
                    {
                        // Localized ApplicationException (folder network errors etc.): safe to show.
                        Log.WriteError("Error resolving links for streaming: " + err.ToString());
                        MessageBox.Show(err.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        // Generic errors may contain raw English/technical text: log full detail,
                        // show sanitized first line (redacted) per DownloadAdvice pattern.
                        Log.WriteError("Error resolving links for streaming: " + err.ToString());
                        MessageBox.Show(DownloadAdvice.FirstLine(Log.Redact(err.Message)), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;
                }
                if (URLs2 is null || URLs2.Count == 0)
                {
                    MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string link = StreamingHelper.CreateStreamingLink(URLs2[0].URL, Config.ServidorStreamingPuerto, ref Config);
                if (string.IsNullOrEmpty(link))
                {
                    MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!StreamingHelper.WatchOnline(Config.VLCPath, link))
                {
                    MessageBox.Show(Language.GetText("VLC could not be started"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Close();
            }
            catch (Exception ex)
            {
                Log.WriteError("Error resolving links for streaming: " + ex.ToString());
                // Generic catch: don't leak raw exception text; show sanitized first line.
                MessageBox.Show(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _watchResolving = false;
                if (btnWatchOnline is not null && !btnWatchOnline.IsDisposed)
                    btnWatchOnline.Enabled = true;
                if (btnAgregar is not null && !btnAgregar.IsDisposed)
                    btnAgregar.Enabled = true;
            }
        }



        private ToolTip t;
        private string MsgVerOnline()
        {
            return Language.GetText("Watch Online Note");
        }
        private string MsgPasswordZip()
        {
            return Language.GetText("MsgPasswordZip");
        }

        private void LinkLabel1_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip();
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(LinkLabel1, MsgVerOnline());
        }

        private void LinkLabel1_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(LinkLabel1);
        }

        private void LinkLabel1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgVerOnline(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void chkUnZip_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.Enabled = chkUnZip.Checked;
            // Labels are not interactive; use themed muted text instead of native disabled ink.
            UiStyles.SetRole(lblPassword, chkUnZip.Checked ? UiStyles.Role.BodyText : UiStyles.Role.Muted);
        }

        private void LinkLabel2_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip();
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(LinkLabel2, MsgPasswordZip());
        }

        private void LinkLabel2_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(LinkLabel2);
        }

        private void LinkLabel2_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgPasswordZip(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void linkStegano_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenSteganoLoadOnExit = true;
            Close();
        }

        // Tell the Main form to open de stegano form
        public bool OpenSteganoLoadOnExit = false;
    }
}