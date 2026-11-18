using System;
using System.Collections.Generic;

using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.CompilerServices;

namespace MegaDownloader
{

    public partial class ELCForm
    {
        public ELCForm()
        {
            // The Me.InitializeComponent call is required for Windows Forms designer support.
            InitializeComponent();
            Load += ELCForm_Load;
            FormClosed += (_, __) => Cerrando();
            Shown += PantallaMsg_Shown;

        }


        private bool ClosingForm = false;
        public Main MainForm;
        public string MegaURLs
        {
            set
            {
                txtMegaURLs.Text = value;
            }
        }

        private void ELCForm_Load(object sender, EventArgs e)
        {
            ThemeManager.ApplyTheme(this);
            if (MainForm is not null)
            {
                Translate();

                btnSaveFile.Enabled = false;
                btnExaminar.Enabled = false;


                var AccountH = new ELCAccountHelper(ref MainForm.Config);
                comboELCAccounts.DataSource = AccountH.GetAccounts().OrderBy(c => c.Alias).Select((c) => new

                {
                    Url = c.URL,
                    AccountName = c.Alias
                }).ToList();

                comboELCAccounts.DisplayMember = "AccountName";
                comboELCAccounts.ValueMember = "Url";

                var DefAccount = AccountH.GetDefaultAccount();
                if (DefAccount is not null)
                {
                    comboELCAccounts.SelectedValue = DefAccount.URL;
                }

                AccountH.Dispose();

                if (comboELCAccounts.Items.Count == 0)
                {
                    MessageBox.Show(Language.GetText("No ELC accounts configured. Go to Configuration in order to add an ELC account"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);

                }

                bckELCGenerator = new BackgroundWorker();
                bckELCGenerator.WorkerSupportsCancellation = true;
                bckELCGenerator.RunWorkerAsync();


                // Centramos la pantalla
                // http://stackoverflow.com/questions/7892090/how-to-set-winform-start-position-at-top-right
                var scr = Screen.FromPoint(Location);
                Location = new Point((int)((scr.WorkingArea.Right - Width) / 2), (int)((scr.WorkingArea.Bottom - Height) / 2));
            }
        }

        private void Cerrando()
        {
            ClosingForm = true;
            try
            {
                if (bckELCGenerator is not null)
                    bckELCGenerator.CancelAsync();
            }
            catch
            {
            }
            // Wake the worker so it can observe CancellationPending and exit promptly.
            try
            {
                elcSignal.Set();
            }
            catch
            {
            }
        }

        private void PantallaMsg_Shown(object sender, EventArgs e)
        {
            PonerFoco();
        }

        public void PonerFoco()
        {
            // http://stackoverflow.com/questions/278237/keep-window-on-top-and-steal-focus-in-winforms
            TopMost = true;
            TopMost = false;
            TopMost = true; // Para que funcione tenemos que quitar Topmost y volverlo a activar (??)
            Activate();
        }

        private void Translate()
        {
            Text = Language.GetText("ELC generator");
            btnCerrar.Text = Language.GetText("Close");
            gbMEGAUrl.Text = Language.GetText("MEGA Url");
            btnExaminar.Text = Language.GetText("Browse");
            gbELCLink.Text = Language.GetText("ELC");
            // RC:此前两 Label 漏翻译,永远西班牙语。复用已有键,不新增键。
            label3.Text = Language.GetText("ELC Accounts") + ":";
            lblSaveAsELC.Text = Language.GetText("Save file") + ":";
            lblExplanation.Text = Language.GetText("Paste your MEGA Url(s), select your ELC account, and click on Generate ELC");
            lblExplanation2.Text = Language.GetText("This is your ELC link. You can also create an ELC file clicking on Save file");
            btnCrearELC.Text = Language.GetText("Generate ELC");
            btnSaveFile.Text = Language.GetText("Save file");
            chkMultipleELCs.Text = Language.GetText("Multiple ELC");
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnCrearELC_Click(object sender, EventArgs e)
        {
            try
            {

                if (comboELCAccounts.SelectedValue is null || string.IsNullOrEmpty(Conversions.ToString(comboELCAccounts.SelectedValue)))
                {
                    MessageBox.Show(Language.GetText("Please select a valid ELC account"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (BackgroundWorkerBusy)
                {
                    MessageBox.Show(Language.GetText("There is an ELC being processed, please wait"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }



                if (chkMultipleELCs.Checked)
                {
                    // 1 ELC for each link
                    var Mlist = new List<ServerEncoderLinkHelper.MegaLink>();
                    int linkIndex = 0;
                    string originalText = txtMegaURLs.Text;
                    var modifiedText = new System.Text.StringBuilder();
                    foreach (var line in originalText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                    {
                        var linkList = URLExtractor.ExtraerURLs(line);
                        if (linkList.Count > 0)
                        {
                            foreach (string Link in linkList)
                            {
                                var l = new ServerEncoderLinkHelper.MegaLink();
                                l.FileID = URLExtractor.ExtraerFileID(Link);
                                l.FileKey = URLExtractor.ExtraerFileKey(Link);
                                l.MegaFolder = URLExtractor.IsMegaFolder(Link);
                                l.SubFolderID = URLExtractor.ExtraerSubFolderID(Link);
                                l.SubFileID = URLExtractor.ExtraerSubFileID(Link);
                                Mlist.Add(l);
                                modifiedText.AppendLine("{" + linkIndex + "}");
                                linkIndex += 1;
                            }
                        }
                        else
                        {
                            modifiedText.AppendLine(line);
                        }

                    }

                    txtELCUrl.Text = "";
                    BackgroundWorkerBusy = true;
                    btnCrearELC.Text = Language.GetText("Loading...");
                    btnCrearELC.Enabled = false;
                    ELC_URL = Conversions.ToString(comboELCAccounts.SelectedValue);

                    MegaLinkList = Mlist;
                    TextTemplateToPrint = modifiedText.ToString();
                    Action = "GenerateELC_Multiple";
                    elcSignal.Set();
                }
                else
                {
                    // 1 ELC for all links

                    // Check data
                    var linkList = URLExtractor.ExtraerURLs(txtMegaURLs.Text);
                    if (linkList.Count == 0)
                    {
                        MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    txtELCUrl.Text = "";
                    BackgroundWorkerBusy = true;
                    btnCrearELC.Text = Language.GetText("Loading...");
                    btnCrearELC.Enabled = false;
                    ELC_URL = Conversions.ToString(comboELCAccounts.SelectedValue);

                    // Perform ELC generation
                    var Mlist = new List<ServerEncoderLinkHelper.MegaLink>();
                    foreach (string Link in linkList)
                    {
                        var l = new ServerEncoderLinkHelper.MegaLink();
                        l.FileID = URLExtractor.ExtraerFileID(Link);
                        l.FileKey = URLExtractor.ExtraerFileKey(Link);
                        l.MegaFolder = URLExtractor.IsMegaFolder(Link);
                        l.SubFolderID = URLExtractor.ExtraerSubFolderID(Link);
                        l.SubFileID = URLExtractor.ExtraerSubFileID(Link);
                        Mlist.Add(l);
                    }
                    MegaLinkList = Mlist;
                    TextTemplateToPrint = string.Empty;
                    Action = "GenerateELC";
                    elcSignal.Set();
                }
            }




            catch (Exception ex)
            {
                Log.WriteError("Error generating ELC: " + ex.ToString());
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void btnSaveFile_Click(object sender, EventArgs e)
        {
            try
            {

                // Check data
                if (string.IsNullOrEmpty(ELCResult))
                {
                    MessageBox.Show(Language.GetText("Links not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrEmpty(txtExaminar.Text))
                {
                    MessageBox.Show(Language.GetText("The ELC path is not valid"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (BackgroundWorkerBusy)
                {
                    MessageBox.Show(Language.GetText("There is an ELC being processed, please wait"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ELCFilePath = txtExaminar.Text;
                BackgroundWorkerBusy = true;
                Action = "SaveELC";
                elcSignal.Set();
            }

            catch (Exception ex)
            {
                Log.WriteError("Error generating ELC: " + ex.ToString());
                MessageBox.Show(ex.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void btnExaminar_Click(object sender, EventArgs e)
        {
            var Examinar = new SaveFileDialog();
            Examinar.CheckFileExists = false;
            Examinar.DefaultExt = "elc";
            Examinar.Filter = Language.GetText("ELC file") + " (*.elc)|*.elc";

            if (Examinar.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtExaminar.Text = Examinar.FileName;
            }

            Examinar.Dispose();
        }


        private BackgroundWorker _bckELCGenerator;

        private BackgroundWorker bckELCGenerator
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _bckELCGenerator;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_bckELCGenerator != null)
                {
                    _bckELCGenerator.DoWork -= bckELCGenerator_DoWork;
                }

                _bckELCGenerator = value;
                if (_bckELCGenerator != null)
                {
                    _bckELCGenerator.DoWork += bckELCGenerator_DoWork;
                }
            }
        }
        private bool BackgroundWorkerBusy = false;
        private string Action = null;
        private List<ServerEncoderLinkHelper.MegaLink> MegaLinkList = null;
        private string TextTemplateToPrint;
        private string ELC_URL = null;
        private string ELCResult = null;
        private Exception ErrorGeneratingELC = null;
        private string ELCFilePath;
        // Replaces the previous 300ms busy-poll. The worker blocks on this handle until a
        // task is queued (Action is set + Set() is called) or cancellation is requested.
        private System.Threading.AutoResetEvent elcSignal = new System.Threading.AutoResetEvent(false);

        public void bckELCGenerator_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            while (!worker.CancellationPending)
            {
                // Block until a task is signalled or cancellation wakes us up.
                elcSignal.WaitOne();

                if (worker.CancellationPending)
                    break;

                GenerateELC();

            }
        }


        private void GenerateELC()
        {

            switch (Action ?? "")
            {
                case "GenerateELC_Multiple":

                    try
                    {
                        if (string.IsNullOrEmpty(ELC_URL) | MegaLinkList is null || MegaLinkList.Count == 0)
                        {
                            // Check input
                            throw new ApplicationException(Language.GetText("Invalid input data"));
                        }
                        else
                        {
                            // Encode
                            string temp = TextTemplateToPrint;
                            int index = 0;
                            foreach (var link in MegaLinkList)
                            {
                                var l = new List<ServerEncoderLinkHelper.MegaLink>();
                                l.Add(link);
                                temp = temp.Replace("{" + index + "}", "mega://elc?" + ServerEncoderLinkHelper.ServerEncode(ELC_URL, l, ref MainForm.Config));
                                index += 1;
                            }
                            ELCResult = temp;
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorGeneratingELC = e;
                        ELCResult = string.Empty;
                    }
                    finally
                    {
                        BackgroundWorkerBusy = false;
                    }
                    ActualizarDatos();
                    break;

                case "GenerateELC":
                    try
                    {
                        if (string.IsNullOrEmpty(ELC_URL) | MegaLinkList is null || MegaLinkList.Count == 0)
                        {
                            // Check input
                            throw new ApplicationException(Language.GetText("Invalid input data"));
                        }
                        else
                        {
                            // Encode
                            ELCResult = "mega://elc?" + ServerEncoderLinkHelper.ServerEncode(ELC_URL, MegaLinkList, ref MainForm.Config);
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorGeneratingELC = e;
                        ELCResult = string.Empty;
                    }
                    finally
                    {
                        BackgroundWorkerBusy = false;
                    }
                    ActualizarDatos();
                    break;

                case "SaveELC":
                    try
                    {
                        if (string.IsNullOrEmpty(ELCFilePath) | string.IsNullOrEmpty(ELCResult))
                        {
                            // Check input
                            throw new ApplicationException(Language.GetText("Invalid input data"));
                        }
                        else
                        {
                            using (var t = new System.IO.StreamWriter(ELCFilePath, false))
                            {
                                t.Write(ELCResult);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // N-P1-1: do NOT wipe ELCResult here. A failed save (disk full, access
                        // denied) used to destroy the already-generated link while txtELCUrl kept
                        // displaying it, and the sticky ErrorGeneratingELC made the next successful
                        // generate show the old error and hide the new link.
                        ErrorGeneratingELC = e;
                    }
                    finally
                    {
                        BackgroundWorkerBusy = false;
                    }
                    ActualizarDatos();
                    break;
                default:
                    break;
                    // Sleep
            }

        }



        public delegate void ActualizarDatosCallback();
        private void ActualizarDatos()
        {
            if (ClosingForm)
                return;
            if (txtMegaURLs.InvokeRequired)
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

                switch (Action ?? "")
                {

                    case "GenerateELC":
                    case "GenerateELC_Multiple":
                        Action = string.Empty;
                        btnCrearELC.Text = Language.GetText("Generate ELC");
                        btnCrearELC.Enabled = true;
                        if (ErrorGeneratingELC is not null)
                        {
                            MessageBox.Show(Language.GetText("Error generating ELC") + ": " + ErrorGeneratingELC.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            ErrorGeneratingELC = null;
                        }
                        else
                        {
                            txtELCUrl.Text = ELCResult;
                            btnExaminar.Enabled = true;
                            btnSaveFile.Enabled = true;
                        }

                        break;

                    case "SaveELC":
                        Action = string.Empty;
                        if (ErrorGeneratingELC is not null)
                        {
                            MessageBox.Show(Language.GetText("Error generating ELC") + ": " + ErrorGeneratingELC.Message, Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            // N-P1-1: clear the flag here too (the generate branch does it at :371);
                            // otherwise the next successful generate shows this stale error and skips
                            // updating txtELCUrl / enabling the save buttons.
                            ErrorGeneratingELC = null;
                        }
                        else
                        {
                            MessageBox.Show(Language.GetText("ELC created successfully"), Language.GetText("Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        break;
                }

            }
        }

        private void txtELCUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A | e.KeyCode == Keys.E)
            {
                if (sender is not null)
                {
                    ((TextBox)sender).SelectAll();
                }
                e.Handled = true;
            }
        }

        private void txtMegaURLs_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A | e.KeyCode == Keys.E)
            {
                if (sender is not null)
                {
                    ((TextBox)sender).SelectAll();
                }
                e.Handled = true;
            }
        }
    }
}