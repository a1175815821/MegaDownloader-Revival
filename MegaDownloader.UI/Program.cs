using Avalonia;

namespace MegaDownloader.UI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash(e.Exception);
            e.SetObserved();
        };

        // A mega:// activation (or a second launch) hands its arguments to the instance
        // that is already running and exits, so there is one queue and one window.
        if (!App.BecomePrimaryInstance(args))
            return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void LogCrash(Exception? exception)
    {
        if (exception is null)
            return;

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MegaDownloader",
                "logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, $"crash-{DateTime.Now:yyyyMMdd}.log"),
                $"{DateTime.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
        catch
        {
            // Crash logging must never mask the original failure.
        }
    }
}
