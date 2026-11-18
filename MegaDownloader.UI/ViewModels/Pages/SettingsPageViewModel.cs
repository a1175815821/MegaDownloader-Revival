using CommunityToolkit.Mvvm.ComponentModel;
using MegaDownloader.Platform;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IThemeService _theme;

    [ObservableProperty]
    private int selectedThemeIndex;

    public SettingsPageViewModel(IThemeService theme, IPlatformServices platform)
    {
        _theme = theme;
        AppDataPath = platform.GetAppDataDirectory("MegaDownloader");
        selectedThemeIndex = (int)theme.Mode;
    }

    public string[] ThemeOptions { get; } = { "Auto (system)", "Light", "Dark" };

    public string AppDataPath { get; }

    public string VersionText => AppInfo.Version;

    partial void OnSelectedThemeIndexChanged(int value)
    {
        _theme.SetMode((ThemeMode)value);
    }
}
