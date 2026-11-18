using System;

namespace MegaDownloader
{
    public partial class PropiedadesDescarga
    {

        private IDescarga _Descarga;
        public IDescarga Descarga
        {
            set
            {
                _Descarga = value;
            }
        }

        // P2-6: 载入时的限速状态快照(单位 KB,与 Fichero.LimiteVelocidad 一致)。
        // 包模式下"复选框未勾选"有两种含义:用户从没碰过(包内各文件限速不一致,
        // 载入时就不勾),或者用户主动取消了勾选。之前只按 Checked 判断,后者被
        // 当成前者 → 取消勾选保存后限速纹丝不动,重开属性窗口复选框又变回勾选。
        // 改用"用户是否动过"来判断:只有相对快照发生了变化才下发/清除限速。
        private bool _limiteInicialActivo = false;
        private int _limiteInicialKB = 0;

        public PropiedadesDescarga()
        {
            InitializeComponent();
        }



        private void PropiedadesDescarga_Load(object sender, EventArgs e)
        {
            ThemeManager.ApplyTheme(this);
            Translate();

            txtNombre.Text = _Descarga.DescargaNombre();
            // P2-6: 载入时的限速快照(见字段说明)。限速以 KB 记,与 Fichero.LimiteVelocidad 同单位。
            int limiteInicialKB = 0;
            if (_Descarga is Fichero)
            {
                GroupBox1.Text = Language.GetText("File properties");
                Fichero f = (Fichero)_Descarga;
                txtMD5.Text = f.MD5;
                txtRuta.Text = f.RutaLocal;
                txtUrl.Text = f.LinkVisible ? f.URL : Fichero.HIDDEN_LINK_DESC;
                if (string.IsNullOrEmpty(txtMD5.Text))
                {
                    txtMD5.Text = Language.GetText("Not available");
                }
                if (f.DescargaComenzada)
                {
                    txtRuta.Enabled = false;
                    btnExaminar.Enabled = false;
                }

                chkLimitarVelocidad.Checked = f.LimiteVelocidad > 0;
                txtLimiteVelocidad.Enabled = chkLimitarVelocidad.Checked;
                if (chkLimitarVelocidad.Checked)
                {
                    txtLimiteVelocidad.Text = Utils.FormatSpeedLimit(f.LimiteVelocidad);
                    limiteInicialKB = f.LimiteVelocidad;
                }
            }
            else
            {
                GroupBox1.Text = Language.GetText("Package properties");
                Paquete p = (Paquete)_Descarga;
                txtRuta.Text = p.RutaLocal;
                txtMD5.Text = Language.GetText("Not applied");
                txtUrl.Text = Language.GetText("Not applied");
                // ⑨:包模式初始化各文件公共限速(全一致才显示),否则保持未勾选;
                // 保存侧按"用户是否动过"判断(见字段说明),只改路径/密码不会静默清零文件限速。
                try
                {
                    int commonLimit = -1;
                    foreach (Fichero ff in p.ListaFicheros)
                    {
                        if (commonLimit == -1)
                        {
                            commonLimit = ff.LimiteVelocidad;
                        }
                        else if (commonLimit != ff.LimiteVelocidad)
                        {
                            commonLimit = -2;
                            break;
                        }
                    }
                    if (commonLimit > 0)
                    {
                        chkLimitarVelocidad.Checked = true;
                        txtLimiteVelocidad.Text = Utils.FormatSpeedLimit(commonLimit);
                        limiteInicialKB = commonLimit;
                    }
                    else
                    {
                        chkLimitarVelocidad.Checked = false;
                        txtLimiteVelocidad.Text = string.Empty;
                    }
                }
                catch
                {
                    chkLimitarVelocidad.Checked = false;
                    txtLimiteVelocidad.Text = string.Empty;
                }
                txtLimiteVelocidad.Enabled = chkLimitarVelocidad.Checked;
            }
            // P2-6: 快照落地。保存时只有相对这份快照发生了变化才动限速,
            // 这样"用户从没碰过"与"用户主动取消勾选"才能区分开。
            _limiteInicialActivo = chkLimitarVelocidad.Checked;
            _limiteInicialKB = limiteInicialKB;
            txtTamano.Text = Language.GetText("Downloaded") + ": " + PintarTamano(_Descarga.DescargaPorcentaje() * _Descarga.DescargaTamanoBytes() / 100m) + " - " + Language.GetText("Total") + ": " + PintarTamano(_Descarga.DescargaTamanoBytes());
            chkUnZip.Checked = _Descarga.DescargaExtraccionAutomatica();
            txtPassword.Text = _Descarga.DescargaExtraccionPassword();
            chkUnZip_CheckedChanged(null, null);

        }

        private void Translate()
        {
            chkUnZip.Text = Language.GetText("Automatic extraction");
            btnExaminar.Text = Language.GetText("Browse");
            Label1.Text = Language.GetText("Path") + ":";
            GroupBox1.Text = Language.GetText("Download properties");
            Label5.Text = Language.GetText("URL") + ":";
            Label4.Text = Language.GetText("MD5") + ":";
            Label3.Text = Language.GetText("Size") + ":";
            Label2.Text = Language.GetText("Name") + ":";
            GroupBox2.Text = Language.GetText("Options");
            chkLimitarVelocidad.Text = Language.GetText("Limit speed to");
            LinkLabel1.Text = Language.GetText("Note about the options");
            btnGuardar.Text = Language.GetText("Save");
            btnCancelar.Text = Language.GetText("Cancel");
            Text = Language.GetText("Properties");
            lblPassword.Text = Language.GetText("Password") + ":";
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

        private ToolTip t;
        private string MsgRutaFic()
        {
            return Language.GetText("Can change file path if not downloaded");
        }

        private string MsgRutaPaq()
        {
            return Language.GetText("Message change package path");
        }

        private string MsgPasswordZip()
        {
            return Language.GetText("MsgPasswordZip");
        }

        private void LinkLabel1_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip(); // 复用同一实例,避免每次悬停泄漏一个 ToolTip
            t.SetToolTip(LinkLabel1, _Descarga is Fichero ? MsgRutaFic() : MsgRutaPaq());
        }


        private void LinkLabel1_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(LinkLabel1);
        }

        private void LinkLabel1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(_Descarga is Fichero ? MsgRutaFic() : MsgRutaPaq(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }



        private void LinkLabel2_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip(); // 复用同一实例,避免每次悬停泄漏一个 ToolTip
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

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {

            int LimiteVelocidad = 0;
            if (chkLimitarVelocidad.Checked && !Utils.TryParseSpeedLimit(txtLimiteVelocidad.Text, ref LimiteVelocidad))
            {
                MessageBox.Show(Language.GetText("Invalid speed limit"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // P2-6: 判断"用户是否动过限速",而不是只看复选框勾没勾。
            // 包模式下"未勾选"有两种含义:用户从没碰过(包内各文件限速不一致,载入时
            // 就不勾),或者用户主动取消了勾选。之前只按 Checked 判断,后者被当成前者 →
            // 取消勾选保存后限速纹丝不动,重开属性窗口复选框又变回勾选。
            bool limiteCambiado = chkLimitarVelocidad.Checked != _limiteInicialActivo || chkLimitarVelocidad.Checked && LimiteVelocidad != _limiteInicialKB;


            if (_Descarga is Fichero)
            {
                Fichero f = (Fichero)_Descarga;
                if ((txtRuta.Text ?? "") != (f.RutaLocal ?? "") & !f.DescargaComenzada)
                {
                    // N-P2-4: the package branch below validates the path (CreateDirectory
                    // with a friendly "Invalid directory" error); the single-file branch
                    // accepted anything — including an emptied box — and the download only
                    // failed later with a cryptic IO exception. Align both branches.
                    if (string.IsNullOrWhiteSpace(txtRuta.Text))
                    {
                        MessageBox.Show(Language.GetText("Invalid directory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    try
                    {
                        if (!System.IO.Directory.Exists(txtRuta.Text))
                        {
                            System.IO.Directory.CreateDirectory(txtRuta.Text);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("PropiedadesDescarga: invalid single-file path: " + Log.SafeException(ex));
                        MessageBox.Show(Language.GetText("Invalid directory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    f.RutaLocal = txtRuta.Text;
                }
                f.set_SetDescargaExtraccionAutomatica(txtPassword.Text, chkUnZip.Checked);
                // P2-6: 同样按"用户是否动过"判断,避免没碰限速时被回写
                // (回写会因 MB→KB 的四舍五入产生微小漂移)。取消勾选 = 清除限速。
                if (limiteCambiado)
                {
                    f.LimiteVelocidad = LimiteVelocidad;
                    // ③:单文件限速同为 KB,控制器要字节。
                    ThrottledStreamController.GetController().SetMaxSpeed(f.FileID, LimiteVelocidad * 1024L);
                }
            }
            else
            {
                Paquete p = (Paquete)_Descarga;

                string newRoot = txtRuta.Text;
                bool rootChanged = (newRoot ?? "") != (p.RutaLocal ?? "");
                if (rootChanged)
                {
                    // 无效路径直接拒收,不再把垃圾写进队列(文案沿用 AddLinks 的 Invalid directory)
                    try
                    {
                        System.IO.Directory.CreateDirectory(newRoot);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Language.GetText("Invalid directory"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                foreach (Fichero f in p.ListaFicheros)
                {
                    // 仅包根真变了才搬移未开下文件,且按 RutaRelativa 保留相对结构;
                    // 此前逐文件比 txtRuta <> f.RutaLocal,包内但凡有子目录、哪怕只改限速点确定也会被拍平到包根。
                    if (rootChanged & !f.DescargaComenzada)
                    {
                        try
                        {
                            f.RutaLocal = PathGuard.GetSafePathUnderRoot(newRoot, f.RutaRelativa ?? string.Empty, allowRoot: true);
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("PropiedadesDescarga: invalid relocated path, keeping old one for " + f.FileID + ": " + Log.SafeException(ex));
                        }
                    }
                    // P2-5:逐文件下发必须覆盖"只改密码"的情况。此前条件只比勾选状态,
                    // 于是用户只改解压密码(勾选不变)时子文件拿不到新密码 —— 包级字段更新了
                    // (:259),文件级仍是旧密码,解压时用的是文件自己的密码(Fichero.vb:686
                    // 传 Me.DescargaExtraccionPassword)→ 解压必失败。
                    // 判定条件与包级更新条件(:259)保持一致:勾选状态变了 或 密码变了 就下发。
                    if (p.DescargaExtraccionAutomatica() != chkUnZip.Checked | (p.DescargaExtraccionPassword() ?? "") != (txtPassword.Text ?? ""))
                    {
                        f.set_SetDescargaExtraccionAutomatica(txtPassword.Text, chkUnZip.Checked);
                    }
                    // P2-6: 只有用户动过限速才批量下发。勾选 = 统一设置;
                    // 取消勾选 = 统一清除。此前"未勾选=不覆写"让取消勾选完全无效
                    // (限速纹丝不动,重开窗口复选框又变回勾选),与单文件分支
                    // "未勾选=清零"的语义正好相反。
                    if (limiteCambiado)
                    {
                        if (chkLimitarVelocidad.Checked)
                        {
                            f.LimiteVelocidad = LimiteVelocidad;
                            ThrottledStreamController.GetController().SetMaxSpeed(f.FileID, LimiteVelocidad * 1024L);
                        }
                        else
                        {
                            f.LimiteVelocidad = 0;
                            ThrottledStreamController.GetController().SetMaxSpeed(f.FileID, 0L);
                        }
                    }
                }
                if ((txtRuta.Text ?? "") != (p.RutaLocal ?? ""))
                {
                    p.RutaLocal = txtRuta.Text;
                }
                if (p.DescargaExtraccionAutomatica() != chkUnZip.Checked | (p.DescargaExtraccionPassword() ?? "") != (txtPassword.Text ?? ""))
                {
                    p.set_SetDescargaExtraccionAutomatica(txtPassword.Text, chkUnZip.Checked);
                }
            }
            Close();
        }

        private void chkLimitarVelocidad_CheckedChanged(object sender, EventArgs e)
        {
            txtLimiteVelocidad.Enabled = chkLimitarVelocidad.Checked;
        }

        private void chkUnZip_CheckedChanged(object sender, EventArgs e)
        {
            txtPassword.Enabled = chkUnZip.Checked;
            lblPassword.Enabled = chkUnZip.Checked;
        }
    }
}