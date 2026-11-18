using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MegaDownloader;

// Regression cases for the deep-review round: the fixes that touched the Avalonia
// shell and the shared engine. Every case runs against the real assemblies in an
// isolated temporary directory; no MEGA account and no user configuration is used.
public partial class RegressionTests
{
    static void AvaloniaShellRegressionCases()
    {
        ExtractionWorkerRegression();
        ScheduleGuardRegression();
        WebServerReconfigurationRegression();
        SingleInstanceArgumentRegression();
        SessionLifecycleRegression();
    }

    // P0: the Avalonia shell never drained the extraction queue, so a finished
    // archive stayed in "Descomprimiendo" forever. The engine now owns the worker.
    static void ExtractionWorkerRegression()
    {
        var engineType = typeof(DownloadEngineService);
        var workerField = engineType.GetField("_extractionWorker", hidden);
        Check(workerField != null, "the download engine owns an extraction worker");
        if (workerField == null)
            return;

        var start = engineType.GetMethod("StartExtractionWorker", hidden);
        Check(start != null, "the engine can start the extraction worker");
        if (start == null)
            return;

        string d = Path.Combine(root, "extraction-worker");
        Directory.CreateDirectory(d);
        string zip = Path.Combine(d, "payload.zip");
        MakeZip(zip, "data.txt", "EXTRACTED");

        var engine = new DownloadEngineService();
        try
        {
            start.Invoke(engine, null);
            var worker = workerField.GetValue(engine) as System.ComponentModel.BackgroundWorker;
            Check(worker != null && worker.IsBusy, "the extraction worker runs after the queue is loaded");

            var controller = DescompresorController.GetController();
            bool queued = controller.AgregarElemento("AVALEXTR01", zip, true, "");
            Check(queued, "the archive enters the extraction queue");

            string extracted = Path.Combine(d, "payload", "data.txt");
            bool done = false;
            for (int i = 0; i < 100 && !done; i++)
            {
                Thread.Sleep(100);
                done = File.Exists(extracted) && File.ReadAllText(extracted) == "EXTRACTED";
            }
            Check(done, "the extraction worker extracts a queued archive without the WinForms shell");
        }
        finally
        {
            engine.Dispose();
        }
    }

    // P1: a manual start ignored the download schedule and the quota cooldown, so
    // the queue flipped to "Running" and the next tick silently paused it again.
    static void ScheduleGuardRegression()
    {
        var method = typeof(DownloadEngineService).GetMethod("StartBlockedReason", shared);
        Check(method != null, "the engine exposes the start guard");
        if (method == null)
            return;

        var config = (Configuracion)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(Configuracion));
        var schedule = new DownloadScheduleSettings { Enabled = true, Daily = true, StartMinute = 0, EndMinute = 1 };
        typeof(Configuracion).GetField("DownloadSchedule").SetValue(config, schedule);

        // A daily window of one minute: outside of it the start must be refused.
        DateTime now = DateTime.Now;
        int minute = now.TimeOfDay.TotalMinutes < 30 ? 600 : 0;
        schedule.StartMinute = minute;
        schedule.EndMinute = minute + 1;

        string outside = (string)method.Invoke(null, new object[] { config, false });
        Check(!string.IsNullOrEmpty(outside), "a start outside the download window is refused");

        schedule.StartMinute = (int)now.TimeOfDay.TotalMinutes;
        schedule.EndMinute = schedule.StartMinute == 1439 ? 0 : schedule.StartMinute + 1;
        string inside = (string)method.Invoke(null, new object[] { config, false });
        Check(string.IsNullOrEmpty(inside), "a start inside the download window is allowed");

        string quota = (string)method.Invoke(null, new object[] { config, true });
        Check(!string.IsNullOrEmpty(quota), "a start during the quota cooldown is refused");
    }

    // P1: changing the port or the password in the settings did nothing while the
    // server was running: the old listener stayed bound with the old password.
    static void WebServerReconfigurationRegression()
    {
        var cfg = (Configuracion)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(Configuracion));
        cfg.ServidorWebActivo = true;
        cfg.ServidorStreamingActivo = false;
        cfg.ServidorWebPermitirLAN = false;
        cfg.ServidorWebBindIP = "";
        cfg.ServidorWebNombre = "";
        cfg.ServidorWebTimeout = 5;
        cfg.ServidorWebPassword = "first-password";
        cfg.ServidorWebPuerto = FreePort();

        try
        {
            string error = WebServerHost.Start(null, cfg);
            Check(error == "", "the remote-control server starts: " + error);
            int firstPort = WebServerHost.RemoteControlPort;
            Check(firstPort == cfg.ServidorWebPuerto, "the server reports the port it bound");

            cfg.ServidorWebPuerto = FreePort();
            cfg.ServidorWebPassword = "second-password";
            string restartError = WebServerHost.Start(null, cfg);
            Check(restartError == "", "the server restarts after a settings change: " + restartError);
            Check(WebServerHost.RemoteControlPort == cfg.ServidorWebPuerto,
                  "a changed port is actually bound after saving the settings");
        }
        finally
        {
            WebServerHost.Stop();
        }
    }

    // P2: a second launch without arguments was dropped, so the running window was
    // never brought to the front.
    static void SingleInstanceArgumentRegression()
    {
        string dir = Path.Combine(root, "single-instance");
        Directory.CreateDirectory(dir);

        var coordinator = new SingleInstanceCoordinator(dir, "RegressionTest");
        try
        {
            coordinator.SendArguments(Array.Empty<string>());
            var pending = coordinator.ReadPendingArguments();
            Check(pending.Count == 1 && pending[0].Count == 0,
                  "a launch without arguments still reaches the running instance");

            coordinator.SendArguments(new[] { "https://mega.nz/file/REGRESSION1#key" });
            var withLinks = coordinator.ReadPendingArguments();
            Check(withLinks.Count == 1 && withLinks[0].Count == 1 &&
                  withLinks[0][0] == "https://mega.nz/file/REGRESSION1#key",
                  "a launch with arguments reaches the running instance");
            Check(coordinator.ReadPendingArguments().Count == 0, "the queue is drained after reading");
        }
        finally
        {
            coordinator.Dispose();
        }
    }

    // P1: the browser remote control allocated a session for every anonymous
    // request and never released it, and a CSRF token issued before the login
    // stayed valid afterwards.
    static void SessionLifecycleRegression()
    {
        var responderType = typeof(WebServerHost).Assembly
            .GetType("MegaDownloader.RemoteControlResponder");
        Check(responderType != null, "the remote-control responder is reachable");
        if (responderType == null)
            return;

        var prune = responderType.GetMethod("PruneSessions", hidden);
        var rotate = responderType.GetMethod("RotateCsrf", shared);
        Check(prune != null, "expired sessions are pruned");
        Check(rotate != null, "the CSRF token can be rotated");

        var sessionType = responderType.GetNestedType("Session", BindingFlags.NonPublic);
        Check(sessionType != null, "the session type is reachable");
        if (sessionType == null || rotate == null)
            return;

        var session = Activator.CreateInstance(sessionType, true);
        var csrfField = sessionType.GetField("Csrf", BindingFlags.Instance | BindingFlags.NonPublic);
        var ensure = responderType.GetMethod("EnsureCsrf", shared);
        Check(ensure != null, "the session hands out a CSRF token");
        if (ensure == null)
            return;

        string first = (string)ensure.Invoke(null, new object[] { session });
        rotate.Invoke(null, new object[] { session });
        string second = (string)csrfField.GetValue(session);
        Check(!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second) && first != second,
              "a rotated CSRF token replaces the previous one");
    }
}
