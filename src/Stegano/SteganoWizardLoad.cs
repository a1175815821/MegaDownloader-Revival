using System;
using System.Diagnostics;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader.Stegano
{
    public partial class SteganoWizardLoad
    {


        public Configuracion Config;
        public Main MainForm;

        public SteganoWizardLoad()
        {
            InitializeComponent();
        }

        private void Translate()
        {

            Text = Language.GetText("Steganography Wizard");
            gbIntro.Text = Language.GetText("Introduction");
            lbIntro.Text = Language.GetText("Steganography is the art and science ... LOAD");
            lbImage.Text = Language.GetText("Select an existing image as Input image, or enter the image URL");
            lbPassword.Text = Language.GetText("If the image has a password, enter it. If not, just leave it empty");
            btCancel.Text = Language.GetText("Cancel");
            btLoad.Text = Language.GetText("Load");
            gbImage.Text = Language.GetText("Image to load");
            gbPassword.Text = Language.GetText("Other options");
            lbPasswordTxt.Text = Language.GetText("Password") + ":";
            btImageInput.Text = Language.GetText("Browse");
            lbImageInput.Text = Language.GetText("Input image") + ":";
        }


        private void ScreenLoad(object sender, EventArgs e)
        {

            // Centramos la pantalla
            // http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
            var scr = Screen.FromPoint(Location);
            Location = new Point((int)((scr.WorkingArea.Right - Width) / 2), (int)((scr.WorkingArea.Bottom - Height) / 2));

            Translate();
            ThemeManager.ApplyTheme(this);
        }

        private void btLoad_Click(object sender, EventArgs e)
        {
            btLoad.Enabled = false;
            btCancel.Enabled = false;

            // Me.Invoke(Sub()
            // txtLinks.Text = "Working please wait"
            // End Sub)

            var start = DateTime.Now;

            try
            {

                if (string.IsNullOrEmpty(txtImageInput.Text))
                {
                    throw new ApplicationException(Language.GetText("Input image not specified"));
                }

                // P1-3: 旧校验 IsValidUri 对 Windows 路径恒真(Uri 把 "C:\..." 解析成
                // scheme=file),不存在的路径被放行,最后只得到 "未找到隐藏链接" 的
                // 误导提示。改用 SteganoManager.IsValidImageSource:本地文件必须真实
                // 存在,或必须是 http(s) 远程 URL。
                if (!SteganoManager.IsValidImageSource(txtImageInput.Text))
                {
                    throw new ApplicationException(Language.GetText("Input image does not exist"));
                }


                string Text = null;

                var mng = new SteganoManager();
                if (mng.LoadImages(txtImageInput.Text, txtPassword.Text, ref Text))
                {

                    Hide();
                    if ((Text + "").StartsWith(Fichero.HIDDEN_LINK))
                    {
                        My.MyProject.Forms.Main.ComprobarYAgregarLinks(Text.Substring(Fichero.HIDDEN_LINK.Length), false, true);
                    }
                    else
                    {
                        My.MyProject.Forms.Main.ComprobarYAgregarLinks(Text, false, false);
                    }

                    Close();
                    return;
                }

                else
                {
                    MessageBox.Show(Language.GetText("No hidden links were found. Please check the image and the password"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }


            catch (ApplicationException ex)
            {
                // R-P2-1: restore the window before showing the error. The wizard is
                // hidden at :62 and Main.ComprobarYAgregarLinks can throw after that
                // (e.g. an ELC server failure while hiding the extracted links); the
                // single-instance guard then blocked reopening the wizard, so the menu
                // looked dead until a restart.
                RestaurarVentana();
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }


            catch (Exception ex)
            {
                Log.WriteError("Error trying to load stegano: " + ex.ToString());
                RestaurarVentana();
                // Generic errors: don't leak raw exception text; show localized generic message.
                MessageBox.Show(Language.GetText("Notify_UnknownError"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);

            }

            btCancel.Enabled = true;
            btLoad.Enabled = true;

        }

        /// <summary>R-P2-1: make the wizard visible again after an error on the hidden
        /// code path (Me.Hide() at :62).</summary>
        private void RestaurarVentana()
        {
            try
            {
                if (!Visible)
                {
                    Show();
                }
                Activate();
            }
            catch
            {
            }
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btImageInput_Click(object sender, EventArgs e)
        {

            var Examinar = new OpenFileDialog();
            Examinar.CheckFileExists = true;
            Examinar.DefaultExt = "jpg";
            Examinar.Filter = Language.GetText("Images") + "|*.jpg";
            Examinar.Multiselect = false;

            if (Examinar.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtImageInput.Text = Examinar.FileName;
            }

            Examinar.Dispose();
        }

        private void HelpButtonPressed()
        {
            string codIdi = Language.GetCurrentLanguageCode().ToUpperInvariant();
            if (codIdi.StartsWith("ES"))
            {
                Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("STEGANO_LINK_ES"));
            }
            else
            {
                Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("STEGANO_LINK_EN"));
            }
        }


        // R-P4-2: txtImageInput/btImageInput 也接拖拽(与保存向导对称;此前只有窗体空白处生效)
        private void SteganoWizard_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not null)
            {
                // File drag & drop

                string[] ficheros = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool TodosExisten = true;
                foreach (string Fichero in ficheros)
                {
                    if (!System.IO.File.Exists(Fichero) || !Fichero.ToLower().EndsWith(".jpg"))  // Solo permitimos jpgs
                    {
                        TodosExisten = false;
                    }
                }
                if (TodosExisten)
                {

                    foreach (string Fichero in ficheros)
                    {
                        if (Fichero.ToLower().EndsWith(".jpg"))
                        {
                            txtImageInput.Text = Fichero;
                            return;
                        }
                    }
                }
            }
        }

        private void SteganoWizard_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not null)
            {
                // File drag & drop
                string[] ficheros = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool TodosExisten = true;
                foreach (string Fichero in ficheros)
                {
                    if (!System.IO.File.Exists(Fichero) || !Fichero.ToLower().EndsWith(".jpg"))  // Solo permitimos jpgs
                    {
                        TodosExisten = false;
                    }
                }
                if (TodosExisten)
                {
                    e.Effect = (System.Windows.Forms.DragDropEffects)Conversions.ToInteger(DragDropEffects.Copy);
                }
            }
        }

        private void HelpButtonPressed(object sender, System.ComponentModel.CancelEventArgs e) => HelpButtonPressed();


    }
}