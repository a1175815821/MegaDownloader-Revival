using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MegaDownloader
{

    public partial class Main
    {
        private readonly DownloadScheduleController scheduleController = new DownloadScheduleController();
        private readonly object scheduleActionLock = new object();
        private Timer scheduleTimer;
        private ToolStripMenuItem scheduleMenuItem;

        private void InitDownloadScheduler()
        {
            scheduleTimer = new Timer(components) { Interval = 1000 };
            scheduleTimer.Tick += ScheduleTick;
            ScheduleTick(null, EventArgs.Empty);
            scheduleTimer.Start();
        }

        private void ScheduleTick(object sender, EventArgs e)
        {
            lock (scheduleActionLock)
            {
                if (Cerrando || Config is null || Config.LoadTransientError || Paquete.HasTransientLoadError || System.Threading.Volatile.Read(ref _startupQueueLoading))
                    return;
                if (scheduleMenuItem is not null)
                    scheduleMenuItem.Checked = Config.DownloadSchedule.Enabled;
                switch (scheduleController.Evaluate(Config.DownloadSchedule, DateTime.Now, EstadoAplicacion == TipoEstadoAplicacion.Descargando))
                {
                    case DownloadScheduleController.ScheduleAction.Pause:
                        QuitarDescargasIndividuales();
                        EstadoAplicacion = TipoEstadoAplicacion.Pausa;
                        ThrottledStreamController.GetController().Abortar();
                        Log.WriteInfo("Download schedule paused the queue.");
                        break;
                    case DownloadScheduleController.ScheduleAction.Start:
                        // Leave PausaIndividual intact. Quota gating is also enforced in
                        // PonerFicherosADescargar, so an active quota wait remains in force.
                        EstadoAplicacion = TipoEstadoAplicacion.Descargando;
                        ThrottledStreamController.GetController().Continuar();
                        Log.WriteInfo("Download schedule started the queue.");
                        break;
                }
            }
        }

        private bool ScheduleAllowsStart(bool showMessage = false)
        {
            if (Config is null || Config.LoadTransientError || Paquete.HasTransientLoadError || System.Threading.Volatile.Read(ref _startupQueueLoading))
                return false;
            bool allowed = Config.DownloadSchedule.AllowsDownload(DateTime.Now);
            if (!allowed && showMessage)
                ToastForm.ShowToast(this, Language.GetText("Schedule_Outside"));
            return allowed;
        }

        private void OpenDownloadSchedule(object sender, EventArgs e)
        {
            using (var dialog = new DownloadScheduleForm(Config.DownloadSchedule))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                lock (scheduleActionLock)
                {
                    Config.DownloadSchedule = dialog.Settings;
                    scheduleController.Reset();
                }
                Config.GuardarXML(true);
                if (Config.ErrorConfig == Configuracion.ErrorConfigClass.Fichero_No_Creado || Config.LoadTransientError)
                {
                    MessageBox.Show(this, Language.GetText("Schedule_SaveFailed"), Language.GetText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                ScheduleTick(null, EventArgs.Empty);
            }
        }

        internal Dictionary<string, URLProcessor.FileURL> GetQueuedLinkSnapshot()
        {
            var result = new Dictionary<string, URLProcessor.FileURL>(StringComparer.Ordinal);
            var files = new List<Fichero>();
            lock (Mutex.ListaDescargas)
            {
                foreach (Paquete package in ListaPaquetes)
                    files.AddRange(package.SnapshotFicheros());
            }
            foreach (Fichero @file in files)
            {
                try
                {
                    string key = BatchLinkPreflight.Identity(@file.FileID, @file.FileKey);
                    if (key is null)
                        continue;
                    result[key] = new URLProcessor.FileURL("", "")
                    {
                        DisplayName = @file.DescargaProcesada ? @file.NombreFichero : null,
                        SizeBytes = @file.DescargaProcesada ? @file.TamanoBytes : default(long?)
                    };
                }
                catch (ObjectDisposedException ex)
                {
                    // A task can be removed after the short reference snapshot.
                }
            }
            return result;
        }
    }
}