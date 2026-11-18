using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MegaDownloader
{

    public partial class EncodeLinksForm
    {

        private bool ClosingForm = false;
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
        public Main MainForm;

        public EncodeLinksForm()
        {
            InitializeComponent();
        }


        private void EncodeLinksForm_Load(object sender, EventArgs e)
        {
            ThemeManager.ApplyTheme(this);
            txtEncodedLinks.Multiline = true;
            txtEncodedLinks.Text = "";

            Translate();

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
            Text = Language.GetText("Encode Url");
            Button1.Text = Language.GetText("Close");
            GroupBox1.Text = Language.GetText("MEGA Url");
            GroupBox2.Text = Language.GetText("Encoded Url");
            lblExplanation.Text = Language.GetText("Paste your MEGA Url(s) and the encoded Url(s) will appear under the Encoded Url box.");
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Close();
        }


        private void Cerrando()
        {
            ClosingForm = true;
            try
            {
                if (bckActualizador is not null)
                    bckActualizador.CancelAsync();
            }
            catch
            {
            }
        }



        private int TextHash = 0;
        public void bckActualizador_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            while (!worker.CancellationPending)
            {

                System.Threading.Thread.Sleep(300);

                if (worker.CancellationPending)
                    break;

                ActualizarDatos();

            }
        }



        public delegate void ActualizarDatosCallback();
        private void ActualizarDatos()
        {
            if (ClosingForm)
                return;
            if (txtOriginalLinks.InvokeRequired)
            {
                try
                {
                    var d = new ActualizarDatosCallback(ActualizarDatos);
                    Invoke(d, new object[] { });
                }
                catch (Exception ex)
                {
                    // A veces da error al cerrar la ventana
                }
            }

            else
            {

                if (ClosingForm)
                    return;
                string text = txtOriginalLinks.Text;
                int NewTextHash = text.GetHashCode();

                if (NewTextHash != TextHash)
                {
                    TextHash = NewTextHash;

                    var l = URLExtractor.ExtraerSoloURLsOficiales(text);
                    foreach (string Url in l)
                    {
                        string FileID = URLExtractor.ExtraerFileID(Url);
                        string FileKey = URLExtractor.ExtraerFileKey(Url);
                        if (!string.IsNullOrEmpty(FileID))
                        {
                            // enc/enc2 密文内无子范围字段:子文件夹/单文件链接保持原文,避免静默扩大为整文件夹
                            if (!string.IsNullOrEmpty(URLExtractor.ExtraerSubFolderID(Url)) || !string.IsNullOrEmpty(URLExtractor.ExtraerSubFileID(Url)))
                            {
                                continue;
                            }
                            string NewUrl = URLExtractor.GenerateEncodedURILink(FileID, FileKey, URLExtractor.IsMegaFolder(Url), false);
                            text = text.Replace(Url, NewUrl);
                        }
                    }

                    txtEncodedLinks.Text = text;

                }

            }
        }

        private void Cerrando(object sender, System.Windows.Forms.FormClosedEventArgs e) => Cerrando();


    }
}