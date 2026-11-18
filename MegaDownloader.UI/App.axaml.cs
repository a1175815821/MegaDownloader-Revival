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

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };

            desktop.Exit += (_, _) =>
            {
                // Only dispose the page if it was actually created; resolving it here
                // would construct the view model and start loading the queue at shutdown.
                Services.GetService<DownloadsPageViewModel>()?.Dispose();
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
        services.AddSingleton<DownloadsPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
