using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public partial class Configuration
    {

        public Configuration()
        {
            SuspendLayout();
            InitializeComponent();
            UiStyles.InitializeForm(this);
            InitConfigurationLayout();
            ResumeLayout(true);
            TabControl1.SelectedIndexChanged += SettingsTabChanged;
            Shown += ConfigurationShown;
        }

        public Configuracion Config;
        public Main MainForm;
        public bool RequiereConfiguracion = false;
        internal bool StartupConfigurationSaved = false;

        public const string PASSWORDDEFECTO = "*****";

        // P1-9:设置搜索框。回车按当前语言文本定位 Tab(控件 Text 已翻译,10 语言零额外工作)。
        private const int EM_SETCUEBANNER = 0x1501;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        private void TrySetSearchCue()
        {
            try
            {
                var h = txtSearchConfig.Handle;
                SendMessage(h, EM_SETCUEBANNER, 0, Language.GetText("Config_SearchHint"));
            }
            catch (Exception ex)
            {
                Log.WriteError("TrySetSearchCue failed: " + ex.ToString());
            }
        }

        private void txtSearchConfig_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LocateConfigText(txtSearchConfig.Text);
            }
        }

        private void LocateConfigText(string raw)
        {
            try
            {
                string q = raw.Trim().ToLowerInvariant();
                if (q.Length < 2)
                    return;
                foreach (TabPage tp in TabControl1.TabPages)
                {
                    if (tp.Text.ToLowerInvariant().Contains(q))
                    {
                        TabControl1.SelectedTab = tp;
                        return;
                    }
                    var hit = FindConfigControl(tp, q);
                    if (hit is not null)
                    {
                        TabControl1.SelectedTab = tp;
                        tp.PerformLayout();
                        tp.ScrollControlIntoView(hit);
                        if (hit.CanSelect)
                        {
                            hit.Focus();
                        }
                        else if (hit.Parent is not null && hit.Parent.CanSelect)
                        {
                            hit.Parent.Focus();
                        }
                        else
                        {
                            TabControl1.Focus();
                        }
                        return;
                    }
                }
                // RC:无命中给声音反馈,此前零反馈以为按钮死了。
                try
                {
                    System.Media.SystemSounds.Beep.Play();
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("LocateConfigText failed: " + ex.ToString());
            }
        }

        private static Control FindConfigControl(Control parent, string q)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is TextBox || c is ComboBox || c is CheckBox || c is Button || c is LinkLabel || c is Label || c is GroupBox || c is NumericUpDown || c is ListBox || c is CheckedListBox || c is DataGridView)



                {
                    if (c.Text is not null && c.Text.ToLowerInvariant().Contains(q))
                        return c;
                    // ComboBox 下拉候选项存在 DataSource/Items,当前 Text 搜不到时继续搜候选项。
                    if (c is ComboBox)
                    {
                        try
                        {
                            ComboBox cb = (ComboBox)c;
                            if (cb.DataSource is System.Windows.Forms.BindingSource)
                            {
                                foreach (object v in (System.Windows.Forms.BindingSource)cb.DataSource)
                                {
                                    string s = "";
                                    if (v is KeyValuePair<string, string>)
                                    {
                                        s = ((KeyValuePair<string, string>)v).Value;
                                    }
                                    else
                                    {
                                        s = v is null ? "" : v.ToString();
                                    }
                                    if (!string.IsNullOrEmpty(s) && s.ToLowerInvariant().Contains(q))
                                        return c;
                                }
                            }
                            else
                            {
                                foreach (object it in cb.Items)
                                {
                                    string s = it is null ? "" : it.ToString();
                                    if (!string.IsNullOrEmpty(s) && s.ToLowerInvariant().Contains(q))
                                        return c;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                var inner = FindConfigControl(c, q);
                if (inner is not null)
                    return inner;
            }
            return default;
        }

        private List<string> ListaPreSharedKey;
        private bool configurationLoaded;
        private bool elcAccountsLoaded;

        private void Configuration_Load(object sender, EventArgs e)
        {

            var suspended = UiStyles.SuspendLayouts(this);
            try
            {
                ElcAccountControl.Config = Config;

                Translate();
                TrySetSearchCue();

                var ListaLogs = new Dictionary<string, string>();
                ListaLogs[Log.LevelLogType.Minimal.ToString()] = Language.GetText("Log_Minimum");
                ListaLogs[Log.LevelLogType.Normal.ToString()] = Language.GetText("Log_Normal");
                ListaLogs[Log.LevelLogType.Info.ToString()] = Language.GetText("Log_Informative");
                ListaLogs[Log.LevelLogType.Debug.ToString()] = Language.GetText("Log_Debug");
                comboLog.DataSource = new BindingSource(ListaLogs, null);
                comboLog.DisplayMember = "Value";
                comboLog.ValueMember = "Key";


                var ListaPrioridadExtraccion = new Dictionary<string, string>();
                ListaPrioridadExtraccion[DescompresionPriority.Normal.ToString()] = Language.GetText("Priority_Normal");
                ListaPrioridadExtraccion[DescompresionPriority.Low.ToString()] = Language.GetText("Priority_Low");
                // P2-9:VeryLow 必须也在列表里。DescompresionPriority 枚举定义了该成员且
                // DescompresionThrottle 有对应实现(Sleep 3ms),存量 Configuration.xml 也可能存着
                // "VeryLow"(v1.8 继承或手工编辑)。此前下拉只有 Normal/Low → 绑定该存量值失败 →
                // SelectedIndex=-1、SelectedValue=Nothing → 保存时 Enum.IsDefined(type, Nothing) 抛
                // ArgumentNullException → 整份配置静默丢失。让下拉能表示枚举的每个合法值即可
                // 从根上消除该触发条件(与 :591 的空值守卫互为双保险)。
                ListaPrioridadExtraccion[DescompresionPriority.VeryLow.ToString()] = Language.GetText("Priority_VeryLow");
                comboPrioridad.DataSource = new BindingSource(ListaPrioridadExtraccion, null);
                comboPrioridad.DisplayMember = "Value";
                comboPrioridad.ValueMember = "Key";


                var ListaIdiomas = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(Language.FollowSystemLanguageCode, Language.GetText("Theme_Auto")) };
                ListaIdiomas.AddRange(Language.GetAvailableLanguages());
                comboIdiomas.DataSource = new BindingSource(ListaIdiomas, null);
                comboIdiomas.DisplayMember = "Value";
                comboIdiomas.ValueMember = "Key";


                txtRuta.Text = Config.RutaDefecto;
                if (string.IsNullOrEmpty(txtRuta.Text))
                {
                    txtRuta.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                txtUsuario.Text = Config.Usuario;
                txtPassword.Text = PASSWORDDEFECTO;

                txtVLCPath.Text = Config.VLCPath;
                if (string.IsNullOrEmpty(Config.VLCPath))
                {
                    try
                    {
                        var registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\VideoLan\VLC", false);
                        if (registryKey is null)
                        {
                            registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\VideoLan\VLC", false);
                        }
                        if (registryKey is not null && registryKey.GetValue("InstallDir") is not null)
                        {
                            var VLCInstallDir = registryKey.GetValue("InstallDir");
                            txtVLCPath.Text = Conversions.ToString(VLCInstallDir);
                        }
                    }
                    catch (System.Security.SecurityException ex)
                    {
                        Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry. Can't read VLC path.");
                    }
                }

                chkAnalisisPortapapeles.Checked = Config.AnalizarPortapapeles;
                chkCrearDirectorio.Checked = Config.CrearDirectorioPaquete;
                chkUnZip.Checked = Config.ExtraerAutomaticamente;
                chkApagarPC.Checked = Config.ApagarPC;
                chkCheckUpdates.Checked = Config.CheckUpdates;
                chkUpdateManualOnly.Checked = Config.UpdateManualOnly;
                txtUpdateCheckInterval.Text = Config.UpdateCheckIntervalHours.ToString();
                chkUltimaConfig.Checked = Config.MantenerUltimaConfiguracion;
                chkComenzarPlay.Checked = Config.ComenzarDescargando;
                chkStartWindows.Checked = Config.IniciarConWindows;
                comboLog.SelectedValue = Config.NivelLog.ToString();
                comboPrioridad.SelectedValue = Config.PrioridadDescompresion.ToString();
                comboIdiomas.SelectedValue = Language.NormalizeLanguagePreference(Config.Idioma);
                if (string.IsNullOrEmpty(Conversions.ToString(comboIdiomas.SelectedValue)))
                {
                    comboIdiomas.SelectedValue = Language.GetCurrentLanguageCode();
                    if (string.IsNullOrEmpty(Conversions.ToString(comboIdiomas.SelectedValue)))
                        comboIdiomas.SelectedValue = Language.FollowSystemLanguageCode;
                }

                var ListaTemas = new Dictionary<string, string>();
                ListaTemas[ConfiguracionUI.ThemeModeType.Auto.ToString()] = Language.GetText("Theme_Auto");
                ListaTemas[ConfiguracionUI.ThemeModeType.Light.ToString()] = Language.GetText("Theme_Light");
                ListaTemas[ConfiguracionUI.ThemeModeType.Dark.ToString()] = Language.GetText("Theme_Dark");
                comboTheme.DataSource = new BindingSource(ListaTemas, null);
                comboTheme.DisplayMember = "Value";
                comboTheme.ValueMember = "Key";
                comboTheme.SelectedValue = Config.ConfigUI.Tema.ToString();
                if (string.IsNullOrEmpty(Conversions.ToString(comboTheme.SelectedValue)) && comboTheme.Items.Count > 0)
                {
                    comboTheme.SelectedIndex = 0;
                }

                chkReintentarError.Checked = Config.ResetearErrores;
                chkQuotaBreak.Checked = Config.QuotaBreakthroughMode;
                if (Config.ResetearErrores)
                {
                    txtPeriodoReintento.Text = Config.ResetearErroresPeriodoMinutos.ToString();
                }


                if (Config.TamanoPaqueteKB > 0)
                {
                    txtTamanoPaquete.Text = Config.TamanoPaqueteKB.ToString();
                }
                if (Config.TamanoBufferKB > 0)
                {
                    txtTamanoBuffer.Text = Config.TamanoBufferKB.ToString();
                }

                if (Config.ConexionesPorFichero > 0)
                {
                    txtConFic.Text = Config.ConexionesPorFichero.ToString();
                }
                if (Config.DescargasSimultaneas > 0)
                {
                    txtDescSimult.Text = Config.DescargasSimultaneas.ToString();
                }


                chkLimitarVelocidad.Checked = Config.LimiteVelocidadKBs > 0;
                txtLimiteVelocidadKBs.Enabled = chkLimitarVelocidad.Checked;
                if (Config.LimiteVelocidadKBs > 0)
                {
                    // ⑪:限长小数(1500KB→1.46484375)。4 位小数约 0.1KB 精度,回乘 Round 后误差 <1KB。
                    txtLimiteVelocidadKBs.Text = Utils.FormatSpeedLimit(Config.LimiteVelocidadKBs);
                }

                chkProxy.Checked = Config.UsarProxy;
                txtProxyIP.Text = Config.ProxyIP;
                txtProxyPort.Text = Config.ProxyPort.ToString();
                txtProxyName.Text = Config.ProxyUser;
                txtProxyPassword.Text = Config.ProxyPassword;
                if (Config.ProxyPort == 0)
                    txtProxyPort.Text = "";

                txtStreamingPassword.Text = Config.ServidorStreamingPassword;
                chkStreamingServer.Checked = Config.ServidorStreamingActivo;
                txtStreamingPort.Text = Config.ServidorStreamingPuerto.ToString();
                if (Config.ServidorStreamingPuerto == 0)
                    txtStreamingPort.Text = "";

                chkServidorWeb.Checked = Config.ServidorWebActivo;
                chkServidorWebLAN.Checked = Config.ServidorWebPermitirLAN;
                txtServidorWebBindIP.Text = Config.ServidorWebBindIP;
                txtServidorWebNombre.Text = Config.ServidorWebNombre;
                txtServidorWebPassword.Text = Config.ServidorWebPassword;
                txtServidorWebPort.Text = Config.ServidorWebPuerto.ToString();
                txtServidorWebTemplate.Text = Config.ServidorWebRutaPlantilla;
                txtServidorWebTimeout.Text = Config.ServidorWebTimeout.ToString();
                if (Config.ServidorWebPuerto == 0)
                    txtServidorWebPort.Text = "";
                // 与保存逻辑(0-99)保持同一边界:此前载入侧 >60 就清空,导致保存 61-99 后重开被静默改回 5
                if (Config.ServidorWebTimeout < 0 | Config.ServidorWebTimeout > 99)
                    txtServidorWebTimeout.Text = "";

                comboPrioridad.Enabled = chkUnZip.Checked;


                var keys = new System.Text.StringBuilder();
                if (Config.ListaPreSharedKeys is not null)
                {
                    foreach (System.Security.SecureString key in Config.ListaPreSharedKeys)
                    {
                        if (key is not null)
                            keys.AppendLine(Criptografia.ToInsecureString(key));
                    }
                }
                txtKeyList.Text = keys.ToString();

                chkProxy_CheckedChanged(null, null);
                chkServidorWeb_CheckedChanged(null, null);
                chkServidorStreaming_CheckedChanged(null, null);
                chkReintentarError_CheckedChanged(null, null);

                // 应用主题到设置窗体本身
                try
                {
                    ThemeManager.ApplyTheme(this, Config.ConfigUI.Tema);
                }
                catch (Exception ex)
                {
                    Log.WriteError("Failed to apply theme to Configuration form: " + ex.ToString());
                }
                configurationLoaded = true;
                EnsureElcAccounts();
            }
            finally
            {
                UiStyles.ResumeLayouts(suspended);
            }

        }

        private void ConfigurationShown(object sender, EventArgs e)
        {
            // Show warnings only after the settings window has painted, with a real
            // owner, instead of blocking Load behind an otherwise blank window.
            if (Config is not null && (Config.ProxyPasswordNeedsReentry && Config.UsarProxy || Config.ServidorStreamingPasswordNeedsReentry && Config.ServidorStreamingActivo))
            {
                BeginInvoke(new MethodInvoker(ShowCredentialWarning));
            }
        }

        private void ShowCredentialWarning()
        {
            if (IsDisposed || Disposing)
                return;
            // P0-4：DPAPI 口令换机/换账户恢复后解密失败，需用户重填。仅所用功能启用时弹窗，
            // 否则只留日志（Configuracion 读盘时已记 Warning），避免打扰未用该功能的用户。
            try
            {
                var needReentry = new List<string>();
                if (Config.ProxyPasswordNeedsReentry && Config.UsarProxy)
                    needReentry.Add(Language.GetText("Config_ProxyPassword"));
                if (Config.ServidorStreamingPasswordNeedsReentry && Config.ServidorStreamingActivo)
                    needReentry.Add(Language.GetText("Config_StreamingPassword"));
                if (needReentry.Count > 0)
                {
                    MessageBox.Show(this, Language.GetText("Config_PasswordReentryWarning") + Constants.vbCrLf + " * " + string.Join(Constants.vbCrLf + " * ", needReentry.ToArray()), Language.GetText("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);

                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Password re-entry prompt failed: " + ex.ToString());
            }

        }

        private void SettingsTabChanged(object sender, EventArgs e)
        {
            if (configurationLoaded)
                EnsureElcAccounts();
        }

        private void EnsureElcAccounts()
        {
            if (elcAccountsLoaded || !configurationLoaded || !ReferenceEquals(TabControl1.SelectedTab, TabPage5))
                return;
            ElcAccountControl.CargarDatos();
            elcAccountsLoaded = true;
        }

        private void Configuration_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
        {
            ElcAccountControl.Cerrar();
        }

        private void Translate()
        {
            Text = Language.GetText("Configuration");
            btnCancel.Text = Language.GetText("Cancel");
            btnGuardar.Text = Language.GetText("Save");
            GroupBox1.Text = Language.GetText("User data");
            chkShowPassword.Text = Language.GetText("Show password");
            Label3.Text = Language.GetText("Password") + ":";
            Label24.Text = Language.GetText("Language") + ":";
            Label25.Text = Language.GetText("Theme") + ":";
            Label2.Text = Language.GetText("E-mail") + ":";
            chkComenzarPlay.Text = Language.GetText("Start downloading when application starts");
            chkApagarPC.Text = Language.GetText("Turn off computer when finished");
            Label12.Text = Language.GetText("minutes");
            Label19.Text = Language.GetText("minutes");
            chkReintentarError.Text = Language.GetText("In case of error, retry the download each");
            chkQuotaBreak.Text = Language.GetText("Quota breakthrough mode (legacy persistent retry)");
            LinkLabel1.Text = Language.GetText("Important note about connections");
            Label10.Text = Language.GetText("Number of parallel downloads") + ":";
            Label9.Text = Language.GetText("Number of connections per file") + ":";
            chkAnalisisPortapapeles.Text = Language.GetText("Capture links from clipboard");
            chkUnZip.Text = Language.GetText("Automatic extraction") + ". " + Language.GetText("Priority") + ":";
            chkCheckUpdates.Text = Language.GetText("Check for updates");
            chkUpdateManualOnly.Text = Language.GetText("Manual check only");
            chkCrearDirectorio.Text = Language.GetText("Create folder per package");
            btnExaminar.Text = Language.GetText("Browse");
            Label1.Text = Language.GetText("Download path") + ":";
            GroupBox3.Text = Language.GetText("Advanced options");
            Label11.Text = Language.GetText("Log level") + ":";
            Label8.Text = Language.GetText("Note: for advanced users only. Dont touch anything if you arent sure");
            Label6.Text = Language.GetText("Disk buffer size") + ":";
            Label4.Text = Language.GetText("Package size") + ":";
            Label14.Text = Language.GetText("Port") + ":";
            Label16.Text = Language.GetText("Port") + ":";
            Label13.Text = Language.GetText("Proxy address/IP") + ":";
            chkProxy.Text = Language.GetText("Use proxy");
            ConexionGroup.Text = Language.GetText("Connection");
            chkLimitarVelocidad.Text = Language.GetText("Limit speed to");
            GroupBox4.Text = Language.GetText("General options");
            chkUltimaConfig.Text = Language.GetText("Save / use last used configuration");
            chkStartWindows.Text = Language.GetText("Start with Windows");
            TabPage1.Text = Language.GetText("General");
            TabPage2.Text = Language.GetText("Advanced");
            GroupBox5.Text = Language.GetText("Web server");
            btnExaminarTemplate.Text = Language.GetText("Browse");
            Label21.Text = Language.GetText("Template path") + ":";
            Label20.Text = Language.GetText("Name (optional)") + ":";
            Label18.Text = Language.GetText("Interrupt session") + ":";
            Label17.Text = Language.GetText("Password") + ":";

            chkServidorWeb.Text = Language.GetText("Use web server");
            chkServidorWebLAN.Text = Language.GetText("Allow LAN access");
            lblServidorWebBindIP.Text = Language.GetText("Bind IP (empty = all)") + ":";
            GroupBox2.Text = Language.GetText("Proxy");
            Label23.Text = Language.GetText("User (optional)") + ":";
            Label22.Text = Language.GetText("Password (optional)") + ":";
            TabPage3.Text = Language.GetText("Pre-Shared Keys");
            TabPage4.Text = Language.GetText("Streaming");
            TabPage5.Text = Language.GetText("ELC Accounts");
            lblDesc.Text = Language.GetText("Pre-Shared Key Manager Description");
            GroupBox.Text = Language.GetText("Pre-Shared Key list");


            GroupBox6.Text = Language.GetText("Streaming configuration");
            GroupBox7.Text = Language.GetText("Information");
            lblStreamingPort.Text = Language.GetText("Port") + ":";
            chkStreamingServer.Text = Language.GetText("Use streaming server");
            GroupBox8.Text = Language.GetText("VLC Configuration");
            Label26.Text = Language.GetText("VLC Path") + ":";
            btnExaminarVLCPath.Text = Language.GetText("Browse");
            linkDownloadVLC.Text = Language.GetText("Download VLC");
            lblInfoStreaming.Text = Language.GetText("Streaming configuration text");
            lblStreamingPassword.Text = Language.GetText("Password") + " (" + Language.GetText("optional") + "):";
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


        private void btnCancel_Click(object sender, EventArgs e)
        {
            // 密码框在 Load 时被填成占位符 PASSWORDDEFECTO("*****"),恒非空:
            // 必须把占位符视为"未设置",否则首次运行的强制配置可被"取消"直接绕过
            bool PasswordNoConfigurada = string.IsNullOrEmpty(txtPassword.Text) || (txtPassword.Text ?? "") == PASSWORDDEFECTO;
            if (RequiereConfiguracion & string.IsNullOrEmpty(txtUsuario.Text) & PasswordNoConfigurada)
            {
                MessageBox.Show(Language.GetText("You must configure user and password"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                Close();
            }

        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {

            // Final review: while a data file is locked, GuardarXML(True) is rejected
            // and this dialog stays open. Writing 30+ fields into Config before that
            // rejection left an in-memory state that the recovery timer would silently
            // roll back when the lock was released (and then persist). Refuse the edit
            // up front: the user's values stay in the controls and can be saved again
            // once recovery completes, with no silent loss.
            if (Config.LoadTransientError)
            {
                MessageBox.Show(Language.GetText("Config_LockedByAnotherProcess"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(Config.Password) & (txtPassword.Text ?? "") == PASSWORDDEFECTO)
            {
                txtPassword.Text = "";
            }

            if (string.IsNullOrEmpty(txtRuta.Text))
            {
                if (MessageBox.Show(Language.GetText("Do you want to leave the default path empty?"), Language.GetText("Save"), MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
                {
                    return;
                }
            }

            int TamanoPaquete = 0;
            int TamanoBuffer = 0;
            int.TryParse(txtTamanoPaquete.Text, out TamanoPaquete);
            int.TryParse(txtTamanoBuffer.Text, out TamanoBuffer);

            if (TamanoPaquete == 0 | TamanoBuffer == 0)
            {
                MessageBox.Show(Language.GetText("Disk buffer or package size values are not correct"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (TamanoPaquete >= TamanoBuffer)
            {
                MessageBox.Show(Language.GetText("Disk buffer size must be greater than package size"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int DescSim = 0;
            int ConFic = 0;
            int.TryParse(txtDescSimult.Text, out DescSim);
            int.TryParse(txtConFic.Text, out ConFic);

            if (DescSim <= 0 | ConFic <= 0 || DescSim * ConFic > 100)
            {
                MessageBox.Show(Language.GetText("Connection number values are incorrect or too high"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int PeriodoReintentosError = 0;
            int.TryParse(txtPeriodoReintento.Text, out PeriodoReintentosError);

            if (PeriodoReintentosError < 1 | PeriodoReintentosError > 999)
            {
                if (chkReintentarError.Checked)
                {
                    MessageBox.Show(Language.GetText("Must specify the retry period, and this value must be between %A and %B").Replace("%A", "1").Replace("%B", "999"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    PeriodoReintentosError = 15;
                }
            }

            int ProxyPort = 0;
            int.TryParse(txtProxyPort.Text, out ProxyPort);

            string proxyHost = Conexion.NormalizeProxyHost(txtProxyIP.Text);
            if (chkProxy.Checked && (ProxyPort < 1 || ProxyPort > 65535 || proxyHost is null))
            {
                MessageBox.Show(Language.GetText("Invalid proxy configuration"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int LimiteVelocidad = 0;
            if (chkLimitarVelocidad.Checked && !Utils.TryParseSpeedLimit(txtLimiteVelocidadKBs.Text, ref LimiteVelocidad))
            {
                MessageBox.Show(Language.GetText("Invalid speed limit"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int servidorWebPuerto = 0;
            int.TryParse(txtServidorWebPort.Text, out servidorWebPuerto);

            int servidorStreamingPuerto = 0;
            int.TryParse(txtStreamingPort.Text, out servidorStreamingPuerto);

            int servidorWebTimeout = 5;
            int.TryParse(txtServidorWebTimeout.Text, out servidorWebTimeout);

            if (!chkServidorWeb.Checked)
            {
                if (servidorWebPuerto < 1024 | servidorWebPuerto > 65535)
                {
                    servidorWebPuerto = 0;
                }
                if (servidorWebTimeout < 0 | servidorWebTimeout > 99)
                {
                    servidorWebTimeout = 5;
                }
            }
            else if (servidorWebPuerto < 1024 | servidorWebPuerto > 65535 | string.IsNullOrEmpty(txtServidorWebPort.Text))
            {
                MessageBox.Show(Language.GetText("Invalid server port"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (servidorWebTimeout < 0 | servidorWebTimeout > 99 | string.IsNullOrEmpty(txtServidorWebTimeout.Text))
            {
                MessageBox.Show(Language.GetText("The value for interrumpting the session must be between 0 and %A. A value of 0 means that session is not interrupted").Replace("%A", "99"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (!string.IsNullOrEmpty(txtServidorWebTemplate.Text) && !File.Exists(txtServidorWebTemplate.Text))
            {
                MessageBox.Show(Language.GetText("The template path is not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (!string.IsNullOrEmpty(txtServidorWebPassword.Text) && txtServidorWebPassword.Text.Length < 8)
            {
                MessageBox.Show(Language.GetText("The web server password must have at least %A characters").Replace("%A", "8"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (chkServidorWebLAN.Checked && string.IsNullOrEmpty(txtServidorWebPassword.Text))
            {
                MessageBox.Show(Language.GetText("A web server password is required to allow LAN access"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else if (chkServidorWebLAN.Checked && !string.IsNullOrEmpty(txtServidorWebBindIP.Text.Trim()))
            {
                try
                {
                    System.Net.IPAddress.Parse(txtServidorWebBindIP.Text.Trim());
                }
                catch
                {
                    MessageBox.Show(Language.GetText("Invalid IP address format"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (!chkStreamingServer.Checked)
            {
                if (servidorStreamingPuerto < 1024 | servidorStreamingPuerto > 65535)
                {
                    servidorStreamingPuerto = 0;
                }
            }
            else if (servidorStreamingPuerto < 1024 | servidorStreamingPuerto > 65535 | string.IsNullOrEmpty(txtStreamingPort.Text))
            {
                MessageBox.Show(Language.GetText("Invalid server port"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            if (chkServidorWeb.Checked & chkStreamingServer.Checked & servidorWebPuerto == servidorStreamingPuerto)
            {
                MessageBox.Show(Language.GetText("Web server and streaming server ports must be different"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            if ((txtPassword.Text ?? "") != PASSWORDDEFECTO)
            {
                Config.Password = txtPassword.Text;
            }

            string vlcPath = txtVLCPath.Text.Trim();
            if (!string.IsNullOrEmpty(vlcPath))
            {
                if (File.Exists(vlcPath) && vlcPath.ToLowerInvariant().EndsWith(".exe"))
                {
                    vlcPath = Path.GetDirectoryName(vlcPath);
                }
                else if (!Directory.Exists(vlcPath))
                {
                    MessageBox.Show(Language.GetText("VLC path is not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            Config.VLCPath = vlcPath;
            Config.Usuario = txtUsuario.Text;
            Config.RutaDefecto = txtRuta.Text;
            Config.ExtraerAutomaticamente = chkUnZip.Checked;
            Config.AnalizarPortapapeles = chkAnalisisPortapapeles.Checked;
            Config.CrearDirectorioPaquete = chkCrearDirectorio.Checked;
            Config.ComenzarDescargando = chkComenzarPlay.Checked;
            Config.TamanoBufferKB = TamanoBuffer;
            Config.TamanoPaqueteKB = TamanoPaquete;
            Config.ConexionesPorFichero = ConFic;
            Config.DescargasSimultaneas = DescSim;
            Config.ResetearErrores = chkReintentarError.Checked;
            Config.QuotaBreakthroughMode = chkQuotaBreak.Checked;
            Config.ApagarPC = chkApagarPC.Checked;
            Config.CheckUpdates = chkCheckUpdates.Checked;
            Config.UpdateManualOnly = chkUpdateManualOnly.Checked;
            int updateInterval = 1;
            if (!int.TryParse(txtUpdateCheckInterval.Text, out updateInterval) || updateInterval <= 0)
            {
                if (chkCheckUpdates.Checked)
                {
                    MessageBox.Show(Language.GetText("Must specify the update check interval, and this value must be a positive number of hours."), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                updateInterval = 1;
            }
            Config.UpdateCheckIntervalHours = updateInterval;
            Config.MantenerUltimaConfiguracion = chkUltimaConfig.Checked;
            Config.IniciarConWindows = chkStartWindows.Checked;
            Config.ResetearErroresPeriodoMinutos = PeriodoReintentosError;
            Config.UsarProxy = chkProxy.Checked;
            Config.ProxyPassword = txtProxyPassword.Text;
            Config.ProxyPasswordNeedsReentry = false;
            Config.ProxyUser = txtProxyName.Text;
            Config.ProxyPort = ProxyPort;
            Config.ProxyIP = proxyHost ?? "";
            Config.LimiteVelocidadKBs = LimiteVelocidad;

            Config.ServidorStreamingActivo = chkStreamingServer.Checked;
            Config.ServidorStreamingPuerto = servidorStreamingPuerto;
            Config.ServidorStreamingPassword = txtStreamingPassword.Text;
            Config.ServidorStreamingPasswordNeedsReentry = false;


            Config.ServidorWebActivo = chkServidorWeb.Checked;
            Config.ServidorWebPermitirLAN = chkServidorWebLAN.Checked;
            Config.ServidorWebBindIP = txtServidorWebBindIP.Text.Trim();
            Config.ServidorWebNombre = txtServidorWebNombre.Text;
            Config.ServidorWebPassword = txtServidorWebPassword.Text;
            Config.ServidorWebPuerto = servidorWebPuerto;
            Config.ServidorWebRutaPlantilla = txtServidorWebTemplate.Text;
            Config.ServidorWebTimeout = servidorWebTimeout;
            ElcAccountControl.SaveToConfig(ref Config);

            bool AvisoReiniciarAppParaIdioma = false;
            if ((Conversions.ToString(comboIdiomas.SelectedValue) ?? "") == Language.FollowSystemLanguageCode || Language.IsValidLanguageCode(Conversions.ToString(comboIdiomas.SelectedValue)))
            {
                AvisoReiniciarAppParaIdioma = (Config.Idioma ?? "") != (Conversions.ToString(comboIdiomas.SelectedValue) ?? "");
                Config.Idioma = Conversions.ToString(comboIdiomas.SelectedValue);
            }


            // P2-9:Enum.IsDefined(type, value) 在 value 为 Nothing 时抛 ArgumentNullException
            // (实测:"Value cannot be null. (Parameter 'value')")。ComboBox.SelectedValue 在
            // "当前值无法与列表项匹配"时返回 Nothing —— 例如存量 Configuration.xml 里的
            // PrioridadDescompresion="VeryLow",而本下拉只有 Normal/Low,绑定失败后
            // SelectedIndex=-1、SelectedValue=Nothing。
            // 此处异常会一路冒出 btnGuardar_Click(该 Sub 无整体 Try/Catch),而
            // ApplicationEvents.UnhandledException 设了 ExitApplication=False → 进程不崩,
            // 但 Config.GuardarXML(True) 被跳过 → 用户刚改的全部配置静默丢失且无任何提示。
            // 故:先判空,再判是否已定义。三处(comboLog/comboPrioridad/comboTheme)同样处理。
            string selLog = Conversions.ToString(comboLog.SelectedValue);
            if (!string.IsNullOrEmpty(selLog) && Enum.IsDefined(typeof(Log.LevelLogType), selLog))
            {
                Config.NivelLog = (Log.LevelLogType)Conversions.ToInteger(Enum.Parse(typeof(Log.LevelLogType), ((KeyValuePair<string, string>)comboLog.SelectedItem).Key));
                Log.SetLogLevel = Config.NivelLog;
            }

            string selPrioridad = Conversions.ToString(comboPrioridad.SelectedValue);
            if (!string.IsNullOrEmpty(selPrioridad) && Enum.IsDefined(typeof(DescompresionPriority), selPrioridad))
            {
                Config.PrioridadDescompresion = (DescompresionPriority)Conversions.ToInteger(Enum.Parse(typeof(DescompresionPriority), ((KeyValuePair<string, string>)comboPrioridad.SelectedItem).Key));
                DescompresionThrottle.DecompressionPriority = Config.PrioridadDescompresion;
            }

            string selTema = Conversions.ToString(comboTheme.SelectedValue);
            if (!string.IsNullOrEmpty(selTema) && Enum.IsDefined(typeof(ConfiguracionUI.ThemeModeType), selTema))
            {
                Config.ConfigUI.Tema = (ConfiguracionUI.ThemeModeType)Conversions.ToInteger(Enum.Parse(typeof(ConfiguracionUI.ThemeModeType), selTema));
            }

            var ListaSharedKeys = new List<System.Security.SecureString>();
            string ListadoSharedKeysStr = txtKeyList.Text;
            if (!string.IsNullOrEmpty(ListadoSharedKeysStr))
            {
                foreach (string Key in ListadoSharedKeysStr.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                {
                    if (!string.IsNullOrEmpty(Key))
                    {
                        ListaSharedKeys.Add(Criptografia.ToSecureString(Key));
                    }
                }
            }
            Config.ListaPreSharedKeys = ListaSharedKeys;

            Config.GuardarXML(true);
            if (Config.ErrorConfig != Configuracion.ErrorConfigClass.SinErrores)
            {
                if (Config.ErrorConfig == Configuracion.ErrorConfigClass.Fichero_Bloqueado)
                {
                    // B1: 真因是文件被其它进程占用,不是磁盘空间/权限不足。
                    MessageBox.Show(Language.GetText("Config_LockedByAnotherProcess"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(Language.GetText("Configuration could not be saved. Check disk space and permissions.") + Constants.vbNewLine + " * " + Config.ErrorConfig.ToString(), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);

                }
                // B1: 保存失败时不能让 StartupConfigurationSaved 保持 False ——
                // CompleteStartup 会据此 _ForzarCierre 直接退出程序。降级状态下
                // 配置会在占用解除后由主窗体计时器自动恢复,向导到此结束即可。
                if (Config.LoadTransientError)
                {
                    StartupConfigurationSaved = true;
                    Close();
                }
                return;
            }

            Conexion.SetProxy(Config);
            MegaQuotaManager.SetBreakthroughMode(Config.QuotaBreakthroughMode);
            StartupRegistration.RegisterInStartup(Config.IniciarConWindows);

            // ③:同 Configuracion,全局限速 KB→B。
            ThrottledStreamController.GetController().SetMaxGlobalSpeed(Config.LimiteVelocidadKBs * 1024L);

            My.MyProject.Forms.Main.Config = Config;
            My.MyProject.Forms.Main.NecesitaCambiarUsuarioYPassword = false;

            // 主题变更后立即刷新主窗体(无需重启)
            try
            {
                if (MainForm is not null)
                {
                    MainForm.ApplyCurrentTheme();
                }
                else
                {
                    My.MyProject.Forms.Main.ApplyCurrentTheme();
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Failed to re-apply theme after config save: " + ex.ToString());
            }

            // Reiniciamos el servidor 
            string avisoServidorWeb = "";
            if (!RequiereConfiguracion)
            {
                WebServerHost.Stop();
                string strErrServidorWeb = WebServerHost.Start(My.MyProject.Forms.Main, Config);
                if (!string.IsNullOrEmpty(strErrServidorWeb))
                {
                    // 设置此时已存盘,不能 Exit Sub:那会跳过"保存成功"提示与关窗,
                    // 让用户以为设置没保存、反复重填。把服务器错误并入最终提示。
                    avisoServidorWeb = Constants.vbNewLine + Constants.vbNewLine + Language.GetText("Error starting web server") + ": " + strErrServidorWeb;
                }
            }

            bool AvisoUsuarioAnonimo = string.IsNullOrEmpty(txtUsuario.Text) & string.IsNullOrEmpty(txtPassword.Text);
            bool AvisoUsuarioRegistrado = !string.IsNullOrEmpty(txtUsuario.Text) & !string.IsNullOrEmpty(txtPassword.Text);

            string avisos = "";
            if (AvisoReiniciarAppParaIdioma)
            {
                avisos += Constants.vbNewLine + " * " + Language.GetText("You have to restart the application for the language change to take effect");
            }
            if (AvisoUsuarioAnonimo)
            {
                // Cuando sirva el user/password para descargar, mostraremos este mensaje
                // avisos &= vbNewLine & " * " & Language.GetText("If no user and password is specified, downloads will be made as an anonymous user")
            }
            if (AvisoUsuarioRegistrado)
            {
                avisos += Constants.vbNewLine + " * " + Language.GetText("User and password is not used yet");
            }

            if (!string.IsNullOrEmpty(avisos))
            {
                avisos = Constants.vbNewLine + Constants.vbNewLine + Language.GetText("Please take into consideration the following") + ": " + avisos;
            }

            MessageBoxIcon iconoAviso = string.IsNullOrEmpty(avisoServidorWeb) ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            MessageBox.Show(Language.GetText("Data saved successfully") + avisos + avisoServidorWeb, Language.GetText("Save"), MessageBoxButtons.OK, iconoAviso);


            StartupConfigurationSaved = true;
            Close();

        }

        private ToolTip t;
        private string MsgMaxConexiones()
        {
            return Language.GetText("Number of connection explanation");
        }


        private void LinkLabel1_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip(); // 复用同一实例,避免每次悬停泄漏一个 ToolTip
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(LinkLabel1, MsgMaxConexiones());
        }

        private void LinkLabel1_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(LinkLabel1);
        }

        private void LinkLabel1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgMaxConexiones(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void chkShowPassword_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;
        }

        private void chkProxy_CheckedChanged(object sender, EventArgs e)
        {
            txtProxyIP.Enabled = chkProxy.Checked;
            txtProxyPort.Enabled = chkProxy.Checked;
            txtProxyName.Enabled = chkProxy.Checked;
            txtProxyPassword.Enabled = chkProxy.Checked;
        }

        private void chkServidorWeb_CheckedChanged(object sender, EventArgs e)
        {
            txtServidorWebNombre.Enabled = chkServidorWeb.Checked;
            txtServidorWebPassword.Enabled = chkServidorWeb.Checked;
            txtServidorWebPort.Enabled = chkServidorWeb.Checked;
            txtServidorWebTemplate.Enabled = chkServidorWeb.Checked;
            txtServidorWebTimeout.Enabled = chkServidorWeb.Checked;
            btnExaminarTemplate.Enabled = chkServidorWeb.Checked;
            chkServidorWebLAN.Enabled = chkServidorWeb.Checked;
            ActualizarEstadoBindIP();
        }

        private void chkServidorWebLAN_CheckedChanged(object sender, EventArgs e)
        {
            ActualizarEstadoBindIP();
        }

        private void ActualizarEstadoBindIP()
        {
            txtServidorWebBindIP.Enabled = chkServidorWeb.Checked && chkServidorWebLAN.Checked;
        }

        private void chkServidorStreaming_CheckedChanged(object sender, EventArgs e)
        {
            txtStreamingPort.Enabled = chkStreamingServer.Checked;
        }

        private string MsgApagarPC()
        {
            return Language.GetText("Turn off PC explanation");
        }


        private void linkApagarPC_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip(); // 复用同一实例,避免每次悬停泄漏一个 ToolTip
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(linkApagarPC, MsgApagarPC());
        }

        private void linkApagarPC_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(linkApagarPC);
        }

        private void linkApagarPC_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgApagarPC(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void chkLimitarVelocidad_CheckedChanged(object sender, EventArgs e)
        {
            txtLimiteVelocidadKBs.Enabled = chkLimitarVelocidad.Checked;
        }

        private void chkUnZip_CheckedChanged(object sender, EventArgs e)
        {
            comboPrioridad.Enabled = chkUnZip.Checked;
        }




        private string MsgUltConfig()
        {
            return Language.GetText("Save and use last conf explanation");
        }

        private void linkUltConfig_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip(); // 复用同一实例,避免每次悬停泄漏一个 ToolTip
            ThemeManager.ApplyThemeToToolTip(t);
            t.SetToolTip(linkUltConfig, MsgUltConfig());
        }

        private void linkUltConfig_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(linkUltConfig);
        }

        private void linkUltConfig_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgUltConfig(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnExaminarTemplate_Click(object sender, EventArgs e)
        {
            var Examinar = new OpenFileDialog();
            Examinar.CheckFileExists = true;
            Examinar.DefaultExt = "xml";
            Examinar.Filter = Language.GetText("Templates") + " (*.xml)|*.xml";
            Examinar.Multiselect = false;

            if (Examinar.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtServidorWebTemplate.Text = Examinar.FileName;
            }

            Examinar.Dispose();
        }

        private void txtServidorWebTemplate_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            string[] ficheros = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string Fichero in ficheros)
            {
                if (Fichero.ToUpper().EndsWith(".XML"))
                {
                    txtServidorWebTemplate.Text = Fichero;
                }
            }
        }

        private void txtServidorWebTemplate_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] ficheros = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string Fichero in ficheros)
                {
                    if (Fichero.ToUpper().EndsWith(".XML"))
                    {
                        e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.Copy);
                    }
                }
            }
        }

        private void HelpButtonPressed()
        {
            My.MyProject.Forms.Main.FAQ_Click(null, null);
        }

        private void linkDownloadVLC_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.videolan.org/vlc/");
        }

        private void btnExaminarVLCPath_Click(object sender, EventArgs e)
        {

            var ExaminarDirectorio = new FolderBrowserDialog();
            ExaminarDirectorio.Description = Language.GetText("Select directory");

            if (ExaminarDirectorio.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtVLCPath.Text = ExaminarDirectorio.SelectedPath;
            }

            ExaminarDirectorio.Dispose();
        }

        private void chkReintentarError_CheckedChanged(object sender, EventArgs e)
        {
            txtPeriodoReintento.Enabled = chkReintentarError.Checked;
        }

    }
}