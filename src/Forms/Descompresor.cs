using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;

namespace MegaDownloader
{

    public partial class Descompresor
    {



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

        public Descompresor()
        {
            InitializeComponent();
        }

        private void Descompresor_Load(object sender, EventArgs e)
        {
            ThemeManager.ApplyTheme(this);
            Translate();
        }


        private void Translate()
        {
            gbEstado.Text = Language.GetText("Status");
            Label6.Text = Language.GetText("Size") + ":";
            Label4.Text = Language.GetText("Current") + ":";
            Label2.Text = Language.GetText("File") + ":";
            GroupBox2.Text = Language.GetText("Extraction queue");
            Label1.Text = Language.GetText("This screen lets you see the status of the files pending of extraction") + " " + '\r' + '\n' + "(" + Language.GetText("if automatic extraction option is enabled") + ").";
            Text = Language.GetText("File decompressor");

        }

        private void Descompresor_Shown(object sender, EventArgs e)
        {
            ActualizarDatos();

            bckActualizador = new BackgroundWorker();
            bckActualizador.WorkerSupportsCancellation = true;
            bckActualizador.RunWorkerAsync();
        }

        public delegate void ActualizarDatosCallback();
        private void ActualizarDatos()
        {
            if (txtCola.InvokeRequired)
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

                var desc = DescompresorController.GetController();

                var Elementos = desc.GetCola();
                lblFichero.Text = desc.EleActual_Ruta;
                lblActual.Text = desc.EleActual_FicActNombre;
                lblTamano.Text = "  -";
                var Total = desc.EleActual_TamanoTotal;
                var Extraido = desc.EleActual_FicActExtraido;
                var Extraido2 = desc.EleActual_TamanoTotalExtraido;
                if (Total.HasValue)
                {
                    if (!Extraido.HasValue)
                        Extraido = 0;
                    if (!Extraido2.HasValue)
                        Extraido2 = 0;
                    Extraido += Extraido2;
                    lblTamano.Text = PintarTamano(Extraido.Value) + " / " + PintarTamano(Total.Value);
                }
                if (string.IsNullOrEmpty(lblFichero.Text))
                {
                    lblFichero.Text = "-";
                }
                if (string.IsNullOrEmpty(lblActual.Text))
                {
                    lblActual.Text = "-";
                }

                // Si el nombre del fichero es muy largo, evitamos que se corte
                while (lblFichero.Width + 65 >= gbEstado.Width & lblFichero.Text.Length > 10)
                {
                    int Longi = lblFichero.Text.Length;
                    int MitadLongi = (int)Math.Round(Longi / 2d);
                    lblFichero.Text = lblFichero.Text.Substring(0, MitadLongi - 3) + " ... " + lblFichero.Text.Substring(MitadLongi + 3, Longi - (MitadLongi + 3));
                    if (lblFichero.Text.Length >= Longi)
                        break;
                }

                while (lblActual.Width + 220 >= gbEstado.Width & lblActual.Text.Length > 15)
                {
                    int Longi = lblActual.Text.Length;
                    int MitadLongi = (int)Math.Round(Longi / 2d);
                    lblActual.Text = lblActual.Text.Substring(0, MitadLongi - 4) + " ... " + lblActual.Text.Substring(MitadLongi + 3, Longi - (MitadLongi + 4));
                    if (lblActual.Text.Length >= Longi)
                        break;
                }



                string str = "";
                foreach (string ele in Elementos)
                    str += ele + Constants.vbNewLine;

                if ((txtCola.Text ?? "") != (str ?? ""))
                {
                    txtCola.Text = str;
                }

            }
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
            return numBytes.ToString("F1") + " " + Dato;

        }

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

        private void Cerrando(object sender, System.Windows.Forms.FormClosedEventArgs e) => Cerrando();




    }
}