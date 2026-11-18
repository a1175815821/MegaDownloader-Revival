using System;

namespace MegaDownloader
{
    public sealed partial class SplashScreen
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        private void SplashScreen1_Load(object sender, EventArgs e)
        {
            lblMsg.Text = Language.GetText("Loading please wait");
            lblThx.Text = InternalConfiguration.ObtenerNombreApp() + InternalConfiguration.ObtenerValueFromInternalConfig("VERSION_MEGADOWNLOADER");
            ThemeManager.ApplyTheme(this);
        }

    }
}