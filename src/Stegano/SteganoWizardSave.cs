using System;
using System.Diagnostics;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader.Stegano
{
    public partial class SteganoWizardSave
    {


        public Configuracion Config;
        public Main MainForm;

        public SteganoWizardSave()
        {
            InitializeComponent();
        }

        private void Translate()
        {

            Text = Language.GetText("Steganography Wizard");
            gbIntro.Text = Language.GetText("Introduction");
            gbImage.Text = Language.GetText("Image to load");
            gbPassword.Text = Language.GetText("Other options");
            btCancel.Text = Language.GetText("Cancel");
            btSave.Text = Language.GetText("Save");
            lbIntro.Text = Language.GetText("Steganography is the art and science ... SAVE");
            lbQuality.Text = Language.GetText("JPEG Quality") + ":";
            lbPasswordTxt.Text = Language.GetText("Password") + ":";
            btImageInput.Text = Language.GetText("Browse");
            btImageOutput.Text = Language.GetText("Browse");
            lbLinks.Text = Language.GetText("Enter one or more MEGA links to be hidden");
            lbImage.Text = Language.GetText("Select an existing image as Input image ...");
            lbImageInput.Text = Language.GetText("Input image") + ":";
            lbImageOutput.Text = Language.GetText("Output image") + ":";
            lbPassword.Text = Language.GetText("Optionally, you can specify the JPEG quality and a password. If not, just leave it empty");
            chkVisibleLinks.Text = Language.GetText("Visible links");
            helpStegano.Text = Language.GetText("For more information about stegano click here");

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


        private void btSave_Click(object sender, EventArgs e)
        {
            btSave.Enabled = false;
            btCancel.Enabled = false;

            var start = DateTime.Now;

            try
            {
                // Validate all inputs
                var URLs = URLExtractor.ExtraerURLs(txtLinks.Text);
                if (URLs.Count == 0)
                {
                    throw new ApplicationException(Language.GetText("Links not valid"));
                }

                if (string.IsNullOrEmpty(txtImageInput.Text))
                {
                    throw new ApplicationException(Language.GetText("Input image not specified"));
                }
                else if (string.IsNullOrEmpty(txtImageOutput.Text))
                {
                    throw new ApplicationException(Language.GetText("Output image not specified"));
                }

                // P1-3: 旧校验 IsValidUri 对 Windows 路径恒真(Uri 把 "C:\..." 解析成
                // scheme=file),不存在的路径被放行,随后 CreateImage 直接炸原始
                // InvalidCastException。改用 SteganoManager.IsValidImageSource。
                if (!SteganoManager.IsValidImageSource(txtImageInput.Text))
                {
                    throw new ApplicationException(Language.GetText("Input image does not exist"));
                }

                // R-P3-6: IsNumeric + CInt threw a raw OverflowException (surfaced as an
                // English "Value was either too large or too small for an Integer."
                // from the generic Catch) for out-of-Int32 inputs like "99999999999".
                // TryParse is overflow-safe, trims stray spaces, and parses once
                // instead of three times.
                int qualityValue = 0;
                if (!int.TryParse(txtQuality.Text.Trim(), out qualityValue) || qualityValue < 10 || qualityValue > 100)
                {
                    throw new ApplicationException(Language.GetText("Invalid JPEG quality"));
                }

                var mng = new SteganoManager();
                // mng.CreateImage(String.Join(vbNewLine, URLs.ToArray), txtImageInput.Text, txtImageOutput.Text, CInt(txtQuality.Text), txtPassword.Text)
                mng.CreateImage((!chkVisibleLinks.Checked ? Fichero.HIDDEN_LINK : "") + txtLinks.Text, txtImageInput.Text, txtImageOutput.Text, qualityValue, txtPassword.Text);




                MessageBox.Show(string.Format(Language.GetText("The image was created successfully in {0} ms"), DateTime.Now.Subtract(start).TotalMilliseconds.ToString("F2")), Language.GetText("Save"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }


            catch (Exception ex)
            {
                Log.WriteError("Error trying to generate stegano: " + ex.ToString());
                // Generic errors: don't leak raw exception text; show localized generic message.
                MessageBox.Show(Language.GetText("Notify_UnknownError"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);

            }

            btCancel.Enabled = true;
            btSave.Enabled = true;
            btSave.Text = Language.GetText("Save");

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
            Examinar.Filter = Language.GetText("Images") + "|*.jpg;*.png;*.bmp;*.gif";
            Examinar.Multiselect = false;

            if (Examinar.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtImageInput.Text = Examinar.FileName;
            }

            Examinar.Dispose();
        }

        private void btImageOutput_Click(object sender, EventArgs e)
        {
            var Examinar = new SaveFileDialog();
            Examinar.CheckFileExists = false;
            Examinar.DefaultExt = "jpg";
            Examinar.Filter = Language.GetText("Images") + " (*.jpg)|*.jpg";

            if (Examinar.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // R-P3-5: the dialog still lets the user type any extension ("out.png"),
                // and a non-.jpg output cannot be read back (the load wizard filters
                // *.jpg). Force .jpg deterministically instead of silently producing
                // an unreadable image.
                string outPath = Examinar.FileName;
                if (!outPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    outPath = System.IO.Path.ChangeExtension(outPath, ".jpg");
                }
                txtImageOutput.Text = outPath;
            }

            Examinar.Dispose();
        }


        private ToolTip t;
        private string MsgVisibleLinksHelp()
        {
            return Language.GetText("Visible links HELP");
        }


        private void helpVisibleLinks_MouseHover(object sender, EventArgs e)
        {
            if (t is null)
                t = new ToolTip();
            t.SetToolTip(helpVisibleLinks, MsgVisibleLinksHelp());
        }

        private void helpVisibleLinks_MouseLeave(object sender, EventArgs e)
        {
            if (t is not null)
                t.Hide(helpVisibleLinks);
        }

        private void helpVisibleLinks_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MsgVisibleLinksHelp(), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void helpStegano_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
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

        private void HelpButtonPressed()
        {
            helpStegano_LinkClicked(default, default);
        }

        // R-P4-2: txtImageInput/btImageInput 也接拖拽(此前 AllowDrop=False,拖到输入框上
        // 光标变禁入、窗体的 DragDrop 不会触发,只有拖到窗体空白处才生效)
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

        private void SteganoWizard_DragDrop2(object sender, System.Windows.Forms.DragEventArgs e)
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
                            txtImageOutput.Text = Fichero;
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