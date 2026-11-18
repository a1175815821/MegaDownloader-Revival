using System;
using Microsoft.VisualBasic;

namespace MegaDownloader
{
    public partial class PantallaMsg
    {

        public string TextoError;
        public bool MostrarCodificarEnlaces = false;

        public PantallaMsg()
        {
            InitializeComponent();
        }
        private void VerError_Load(object sender, EventArgs e)
        {
            ThemeManager.ApplyTheme(this);

            txtDatos.Multiline = true;
            txtDatos.Text = (TextoError + "").Replace(Constants.vbNewLine, Environment.NewLine);

            chkCodificarEnlaces.Visible = MostrarCodificarEnlaces;
            lklGenerarELC.Visible = MostrarCodificarEnlaces;

            Text = Language.GetText("Information");
            Button1.Text = Language.GetText("Close");
            chkCodificarEnlaces.Text = Language.GetText("Encode Url");
            lklGenerarELC.Text = Language.GetText("Generate ELC");

            lklGenerarELC.Location = new System.Drawing.Point(chkCodificarEnlaces.Location.X + chkCodificarEnlaces.Width, lklGenerarELC.Location.Y);

            // Centramos la pantalla
            // http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
            var scr = Screen.FromPoint(Location);
            Location = new Point((int)((scr.WorkingArea.Right - Width) / 2), (int)((scr.WorkingArea.Bottom - Height) / 2));

        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Close();
        }


        private void lklGenerarELC_Click(object sender, EventArgs e)
        {
            var ventanaELC = new ELCForm();
            ventanaELC.MegaURLs = TextoError;
            ventanaELC.MainForm = (Main)Owner;
            ventanaELC.Show();
            Close();
        }


        private void chkCodificarEnlaces_CheckedChanged(object sender, EventArgs e)
        {
            if (chkCodificarEnlaces.Checked)
            {

                var l = URLExtractor.ExtraerSoloURLsOficiales(TextoError);
                string Texto = TextoError;
                foreach (string Url in l)
                {
                    string FileID = URLExtractor.ExtraerFileID(Url);
                    string FileKey = URLExtractor.ExtraerFileKey(Url);
                    if (!string.IsNullOrEmpty(FileID))
                    {
                        // enc2 密文内无子范围字段:子文件夹/单文件链接保持原文,避免静默扩大为整文件夹
                        // (同 EncodeLinksForm:GenerateEncodedURILink 没有子范围字段)。
                        if (!string.IsNullOrEmpty(URLExtractor.ExtraerSubFolderID(Url)) || !string.IsNullOrEmpty(URLExtractor.ExtraerSubFileID(Url)))
                        {
                            continue;
                        }
                        string NewUrl = URLExtractor.GenerateEncodedURILink(FileID, FileKey, URLExtractor.IsMegaFolder(Url), false);
                        Texto = Texto.Replace(Url, NewUrl);
                    }
                }

                txtDatos.Text = (Texto + "").Replace(Constants.vbNewLine, Environment.NewLine);
            }

            else
            {
                txtDatos.Text = (TextoError + "").Replace(Constants.vbNewLine, Environment.NewLine);
            }
        }


    }
}