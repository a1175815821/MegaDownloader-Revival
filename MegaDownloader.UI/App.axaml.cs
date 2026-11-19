using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MegaDownloader.Platform;
using MegaDownloader.UI.Services;
using MegaDownloader.UI.ViewModels;
using MegaDownloader.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// The single-instance gate, created before Avalonia starts so a second launch can
    /// exit without building a window. Held for the process lifetime.
    /// </summary>
    private static SingleInstanceCoordinator? _instance;

    /// <summary>
    /// The arguments this process was started with (without the executable path), so
    /// the activation service sees exactly what the user passed.
    /// </summary>
    public static IReadOnlyList<string> LaunchArguments { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Claims the running instance. Returns false when another copy already owns the
    /// queue; in that case the launch arguments have been forwarded to it.
    /// </summary>
    public static bool BecomePrimaryInstance(string[] args)
    {
        IPlatformServices platform = PlatformServicesFactory.Create();
        string appData = platform.GetAppDataDirectory("MegaDownloader");

        // The Avalonia shell and the WinForms reference app keep separate gates so the
        // two can run side by side.
        _instance = new SingleInstanceCoordinator(appData, "MegaDownloader-Avalonia");
        if (_instance.TryAcquire())
        {
            LaunchArguments = args ?? Array.Empty<string>();
            return true;
        }

        _instance.SendArguments(args ?? Array.Empty<string>());
        _instance.Dispose();
        _instance = null;
        return false;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services.GetRequiredService<IThemeService>().Initialize();
            Services.GetRequiredService<IAppLogger>().Info("Avalonia shell starting");

            // Secrets at rest use the platform scheme: DPAPI on Windows, the OS keychain
            // (with an encrypted key file as fallback) elsewhere. The Windows protector
            // keeps the legacy entropy chain so existing configuration files keep working.
            Criptografia.SetSecretProtector(
                PlatformServicesFactory.CreateSecretProtector(Services.GetRequiredService<IPlatformServices>()));

            var engine = Services.GetRequiredService<DownloadEngineService>();
            UiText.Refresh();

            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
            desktop.MainWindow = mainWindow;

            // The engine reports problems (a file that could not be deleted, a quota
            // cooldown) through this seam. Without a sink they only reached the log and
            // the user never saw them.
            var dialogs = Services.GetRequiredService<IDialogService>();
            var logger = Services.GetRequiredService<IAppLogger>();
            UserNotification.Sink = (kind, title, message) =>
            {
                if (kind == UserNotificationKind.Error)
                    logger.Error(title + ": " + message);
                else
                    logger.Warn(title + ": " + message);

                // The sink is called from worker threads; the dialog has to be shown
                // on the UI thread.
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await dialogs.ShowMessageAsync(title, message);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Showing the notification failed: " + ex.Message);
                    }
                });
            };

            // mega:// activations and second launches are delivered through the spool the
            // single-instance gate owns.
            ActivationService activation = Services.GetRequiredService<ActivationService>();
            activation.Start(LaunchArguments);

            desktop.Exit += (_, _) =>
            {
                // Only dispose the page if it was actually created; resolving it here
                // would construct the view model and start loading the queue at shutdown.
                activation.Dispose();
                Services.GetService<ClipboardMonitor>()?.Dispose();
                Services.GetService<DownloadsPageViewModel>()?.Dispose();
                WebServerHost.Stop();
                engine.Dispose();
                _instance?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPlatformServices>(_ =>
        {
            if (OperatingSystem.IsWindows())
                return new WindowsPlatformServices();

            return new UnixPlatformServices(PlatformServicesFactory.DetectPlatform());
        });

        services.AddSingleton<IAppLogger, FileLogger>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<DownloadEngineService>();
        services.AddSingleton<LinkIntakeService>();
        services.AddSingleton<ClipboardMonitor>();

        // The gate created before Avalonia started; a fresh one only when this process
        // was started without going through Program (tests, designer).
        services.AddSingleton(_ => _instance ?? new SingleInstanceCoordinator(
            PlatformServicesFactory.Create().GetAppDataDirectory("MegaDownloader"), "MegaDownloader-Avalonia"));
        services.AddSingleton<ActivationService>();
        services.AddTransient<AddLinksViewModel>();
        services.AddSingleton<DownloadsPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<StreamingPageViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
