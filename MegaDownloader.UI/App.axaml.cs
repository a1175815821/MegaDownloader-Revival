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

            // Secrets at rest use the platform scheme: DPAPI on Windows, an AES-GCM key
            // file elsewhere. The Windows protector keeps the legacy entropy chain so
            // existing configuration files keep working.
            Criptografia.SetSecretProtector(
                OperatingSystem.IsWindows()
                    ? new WindowsDpapiSecretProtector()
                    : new AesFileSecretProtector(
                        Services.GetRequiredService<IPlatformServices>().GetAppDataDirectory("MegaDownloader")));

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };

            desktop.Exit += (_, _) =>
            {
                // Only dispose the page if it was actually created; resolving it here
                // would construct the view model and start loading the queue at shutdown.
                Services.GetService<ClipboardMonitor>()?.Dispose();
                Services.GetService<DownloadsPageViewModel>()?.Dispose();
                WebServerHost.Stop();
                Services.GetRequiredService<DownloadEngineService>().Dispose();
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
        services.AddTransient<AddLinksViewModel>();
        services.AddSingleton<DownloadsPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<StreamingPageViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
