using System;
using System.Diagnostics;

namespace MegaDownloader
{
    public partial class Credits
    {
        public Credits()
        {
            InitializeComponent();
        }


        private void Credits_Load(object sender, EventArgs e)
        {
            ThemeManager.ApplyTheme(this);
            Translate();
        }

        private void Translate()
        {
            Text = Language.GetText("About");
            Button1.Text = Language.GetText("Close");
            lblTitle.Text = InternalConfiguration.ObtenerNombreApp() + InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER");
            lblAutor.Text = Language.GetText("Created and designed by %NAME");
            if (!lblAutor.Text.Contains("%NAME"))
                lblAutor.Text = "Created and designed by %NAME";
            lblAutor.Text = lblAutor.Text.Replace("%NAME", "Andres Soliño [andres_age] & Yingxue");

            lblEmail.Text = "a1175815821@163.com";
            lblGraciasA.Text = Language.GetText("Thanks to") + ":";
            {
                var withBlock = lblListaColaboradores;
                withBlock.Text = "";
                withBlock.Text += "* " + Language.GetText("%NAME for helping with MEGA criptographic system").Replace("%NAME", "Bernardo Vadell") + '\r' + '\n';
                withBlock.Text += "* Yingxue - Revival maintainer (v2.0+)" + '\r' + '\n';
                withBlock.Text += "* " + Language.GetText("All users that have collaborated") + '\r' + '\n';
                // RC:GetText 缺键/空译文回退返回 key 本身,必须排除,否则 About 多一行裸 key。
                string tc = Language.GetText("Translator credits");
                if (!string.IsNullOrEmpty(tc) && tc != "Translator credits")
                {
                    withBlock.Text += "* " + tc;
                }
            }
            Label1.Text = Language.GetText("Thanks for using this app");
            Label2.Text = Language.GetText("Legal text");

        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void lblTitle_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(InternalConfiguration.ObtenerValueFromInternalConfig("CREDITS_LINK"));
        }
    }
}