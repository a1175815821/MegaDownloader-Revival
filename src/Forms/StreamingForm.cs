using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MegaDownloader
{

    public partial class StreamingForm
    {

        public Configuracion Config;
        public Main MainForm;


        private BackgroundWorker _bckActualizador;

        private BackgroundWorker bckActualizador
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _bckActualizador;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_bckActualizador != null)
                {
                    _bckActualizador.DoWork -= bckActualizador_DoWork;
                }

                _bckActualizador = value;
                if (_bckActualizador != null)
                {
                    _bckActualizador.DoWork += bckActualizador_DoWork;
                }
            }
        }

        public StreamingForm()
        {
            InitializeComponent();
        }

        private void StreamingForm_Load(object sender, EventArgs e)
        {
            ThemeManager.ApplyTheme(this);
            Translate();

            if (!Config.ServidorStreamingActivo)
            {
                txtUrlMEGA.Enabled = false;
                txtUrlStreaming.Enabled = false;
                lblInfo.Text = Language.GetText("Streaming server not activated");
                // RC:此前写死纯红,深色下对比与主题脱节,改用主题 ErrorFore。
                lblInfo.ForeColor = ThemeManager.GetColor("ErrorFore");
            }


            bckActualizador = new BackgroundWorker();
            bckActualizador.WorkerSupportsCancellation = true;
            bckActualizador.RunWorkerAsync();

            // Centramos la pantalla
            // http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
            var scr = Screen.FromPoint(Location);
            Location = new Point((int)((scr.WorkingArea.Right - Width) / 2), (int)((scr.WorkingArea.Bottom - Height) / 2));

        }


        private void Translate()
        {
            Label1.Text = Language.GetText("MEGA Url") + ":";
            Label2.Text = Language.GetText("Streaming Url") + ":";
            GroupBox2.Text = Language.GetText("Streaming");
            GroupBox3.Text = Language.GetText("Information");
            lblInfo.Text = Language.GetText("Streaming Info");
            btnCerrar.Text = Language.GetText("Close");
            btnLanzarVLC.Text = Language.GetText("Start VLC");
            Text = Language.GetText("Streaming");
        }


        public void bckActualizador_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            while (!worker.CancellationPending)
            {

                System.Threading.Thread.Sleep(150);

                if (worker.CancellationPending)
                    break;

                ActualizarDatos();
            }
        }

        private void Cerrando()
        {
            try
            {
                if (bckActualizador is not null)
                    bckActualizador.CancelAsync();
            }
            catch
            {
            }
        }

        private string PreviousURLMega = string.Empty;
        private bool ValidURLMega = false;
        // P3-10: 解析失败后的重试节流(见 ActualizarDatos)。
        private DateTime _resolveRetryAfterUtc = DateTime.MinValue;
        private const int ResolveRetryIntervalSeconds = 5;
        // 第17项:解析流水号。联网解析移到线程池后是异步的,URL 快速改动时
        // 旧结果不得覆盖新结果;每次(重新)解析都递增,结果回 UI 时校验。
        private object _resolveGenLock = new object();
        private int _resolveGeneration = 0;

        public delegate void ActualizarDatosCallback();
        private void ActualizarDatos()
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (txtUrlMEGA.InvokeRequired)
            {
                try
                {
                    // 第17项:BeginInvoke 代替阻塞 Invoke,150ms 轮询线程不再被 UI 卡住
                    BeginInvoke(new ActualizarDatosCallback(ActualizarDatos));
                }
                catch (Exception ex)
                {
                    // A veces da error al cerrar la ventana
                }
                return;
            }

            if (!Config.ServidorStreamingActivo)
            {
                ValidURLMega = false;
                return;
            }

            string URLMega = txtUrlMEGA.Text;
            // P3-10: 解析失败后要能重试。此前这里在发起解析前就把 PreviousURLMega
            // 记成新 URL,失败后轮询认为"文本没变"就不再重试,用户不动输入框就永远
            // 拿不到正确结果。改为:文本变了立即解析;文本没变但仍无有效链接时,
            // 按 _resolveRetryAfterUtc 节流后重试(默认 5 秒一次,避免 150ms 轮询
            // 把解析请求打成网络风暴)。
            bool textoCambiado = (URLMega ?? "") != (PreviousURLMega ?? "");
            // 重试只在输入框非空时进行:空 URL 解析必然失败,否则会变成每 5 秒一次
            // 的无谓联网请求。
            bool tocaReintentar = !ValidURLMega && !string.IsNullOrWhiteSpace(URLMega) && DateTime.UtcNow >= _resolveRetryAfterUtc;

            if (textoCambiado || tocaReintentar)
            {
                PreviousURLMega = URLMega;
                ValidURLMega = false;
                _resolveRetryAfterUtc = DateTime.UtcNow.AddSeconds(ResolveRetryIntervalSeconds);

                // 第17项:CreateStreamingLink 会走 MEGA API 解析(联网),此前在 UI 线程
                // 同步执行导致输入卡死;移到线程池,结果经 BeginInvoke 回 UI。
                int gen;
                lock (_resolveGenLock)
                {
                    _resolveGeneration += 1;
                    gen = _resolveGeneration;
                }
                string urlCopy = URLMega;
                System.Threading.ThreadPool.QueueUserWorkItem((state) =>
    {
        string StreamingLink = string.Empty;
        try
        {
            StreamingLink = StreamingHelper.CreateStreamingLink(urlCopy, Config.ServidorStreamingPuerto, ref Config);
        }
        catch (Exception ex)
        {
            Log.WriteError("CreateStreamingLink failed: " + ex.ToString());
        }
        try
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            BeginInvoke(new Action(() =>
{
                lock (_resolveGenLock)
                {
                    if (gen != _resolveGeneration)
                        return; // 过期结果丢弃
                }
                if (!string.IsNullOrEmpty(StreamingLink))
                {
                    txtUrlStreaming.Text = StreamingLink;
                    ValidURLMega = true;
                }
                else
                {
                    ValidURLMega = false;
                }
            }));
        }
        catch
        {
            // 窗口正在关闭
        }
    });
            }
        }

        /// <summary>
    /// 第17项:同步解析一次并应用,仅按钮点击兜底用(粘贴后立即点、后台解析
    /// 尚未返回时不误报)。递增流水号作废在途的后台解析结果,防旧结果覆盖。
    /// </summary>
        private void ResolveNow()
        {
            string URLMega = txtUrlMEGA.Text;
            if (ValidURLMega && (URLMega ?? "") == (PreviousURLMega ?? ""))
                return;
            // P3-10: 此前在解析之前就把 PreviousURLMega 更新掉,于是后台轮询认为
            // "文本没变"不再重试;而这里一旦抛异常(用户明明填了合法 URL),
            // 唯一的提示却是"URL 是必填的",完全指错方向。
            // 改为:解析成功后才记录"已处理过的 URL";失败时保持原值,
            // 既让轮询能继续重试,也让点击路径能再次尝试。
            ValidURLMega = false;
            lock (_resolveGenLock)
                _resolveGeneration += 1;
            string StreamingLink = StreamingHelper.CreateStreamingLink(URLMega, Config.ServidorStreamingPuerto, ref Config);
            if (string.IsNullOrEmpty(StreamingLink))
            {
                Log.WriteWarning("Streaming link resolution returned an empty link for the entered MEGA URL.");
                return;
            }
            txtUrlStreaming.Text = StreamingLink;
            ValidURLMega = true;
            PreviousURLMega = URLMega;
        }

        private void btnLanzarVLC_Click(object sender, EventArgs e)
        {

            // RC:ValidURLMega 靠后台 150ms 轮询,粘贴后立即点会误判。先算一次再判。
            // 第17项:解析已移后台异步跑,点击时若链接有变/尚未解析完,同步兜底一次
            // (仅用户主动点击触发,不随轮询冻结 UI)。
            // P3-10: 此前空 Catch 把真实原因吞掉,随后统一弹"URL 是必填的"——可用户
            // 明明已经填了合法 URL。改为:解析失败时把真实原因告诉用户;没有异常但
            // 解析不出链接时,提示"无法解析该 MEGA 链接",不再谎报"URL 必填"。
            string errorMessage = "";
            try
            {
                ResolveNow();
            }
            catch (Exception ex)
            {
                Log.WriteError("Streaming link resolution failed: " + ex.ToString());
                errorMessage = ex.Message;
            }
            // RC:无效链接此前静默 Exit,用户以为按钮死了,给提示。
            if (!ValidURLMega)
            {
                string msg = Language.GetText("Streaming link could not be resolved");
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    msg += " (" + errorMessage + ")";
                }
                MessageBox.Show(msg, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!StreamingHelper.WatchOnline(Config.VLCPath, txtUrlStreaming.Text))
            {
                MessageBox.Show(Language.GetText("VLC could not be started"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void HelpButtonPressed()
        {
            My.MyProject.Forms.Main.FAQ_Click(null, null);
        }

        private void Cerrando(object sender, System.Windows.Forms.FormClosedEventArgs e) => Cerrando();
        private void HelpButtonPressed(object sender, CancelEventArgs e) => HelpButtonPressed();
    }
}