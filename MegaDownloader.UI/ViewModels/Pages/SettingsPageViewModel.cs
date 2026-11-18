using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.Platform;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IThemeService _theme;
    private readonly DownloadEngineService _engine;

    [ObservableProperty]
    private int selectedThemeIndex;

    [ObservableProperty]
    private string defaultFolder = string.Empty;

    [ObservableProperty]
    private decimal simultaneousDownloads;

    [ObservableProperty]
    private decimal connectionsPerFile;

    [ObservableProperty]
    private bool createSubdirectory;

    [ObservableProperty]
    private bool extractArchives;

    [ObservableProperty]
    private bool startOnLaunch;

    [ObservableProperty]
    private bool keepLastOptions;

    [ObservableProperty]
    private string saveStatus = string.Empty;

    public SettingsPageViewModel(IThemeService theme, IPlatformServices platform, DownloadEngineService engine)
    {
        _theme = theme;
        _engine = engine;
        AppDataPath = platform.GetAppDataDirectory("MegaDownloader");
        selectedThemeIndex = (int)theme.Mode;

        var config = engine.Config;
        DefaultFolder = config.RutaDefecto ?? string.Empty;
        SimultaneousDownloads = Math.Clamp(config.DescargasSimultaneas, 1, 20);
        ConnectionsPerFile = Math.Clamp(config.ConexionesPorFichero, 1, 16);
        CreateSubdirectory = config.CrearDirectorioPaquete;
        ExtractArchives = config.ExtraerAutomaticamente;
        StartOnLaunch = config.ComenzarDescargando;
        KeepLastOptions = config.MantenerUltimaConfiguracion;
    }

    public string[] ThemeOptions { get; } = { "Auto (system)", "Light", "Dark" };

    public string AppDataPath { get; }

    public string VersionText => AppInfo.Version;

    /// <summary>Set by the view so Browse can use the platform folder picker.</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        _theme.SetMode((ThemeMode)value);
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        if (PickFolderAsync is null)
            return;
        string? folder = await PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
            DefaultFolder = folder;
    }

    [RelayCommand]
    private void Save()
    {
        var config = _engine.Config;
        config.RutaDefecto = DefaultFolder.Trim();
        config.DescargasSimultaneas = (int)Math.Clamp(SimultaneousDownloads, 1, 20);
        config.ConexionesPorFichero = (int)Math.Clamp(ConnectionsPerFile, 1, 16);
        config.CrearDirectorioPaquete = CreateSubdirectory;
        config.ExtraerAutomaticamente = ExtractArchives;
        config.ComenzarDescargando = StartOnLaunch;
        config.MantenerUltimaConfiguracion = KeepLastOptions;
        config.GuardarXML(true);
        SaveStatus = $"Saved at {DateTime.Now:HH:mm:ss}.";
    }
}
