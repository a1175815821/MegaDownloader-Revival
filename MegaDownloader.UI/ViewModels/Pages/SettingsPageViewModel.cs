using System.Collections.ObjectModel;
using System.Security;
using System.Text;
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
    private LanguageOption? selectedLanguage;

    [ObservableProperty]
    private bool useProxy;

    [ObservableProperty]
    private string proxyHost = string.Empty;

    [ObservableProperty]
    private decimal proxyPort;

    [ObservableProperty]
    private string proxyUser = string.Empty;

    [ObservableProperty]
    private string proxyPassword = string.Empty;

    [ObservableProperty]
    private bool scheduleEnabled;

    [ObservableProperty]
    private bool scheduleDaily;

    [ObservableProperty]
    private TimeSpan? scheduleStart;

    [ObservableProperty]
    private TimeSpan? scheduleEnd;

    [ObservableProperty]
    private bool streamingEnabled;

    [ObservableProperty]
    private decimal streamingPort;

    [ObservableProperty]
    private string streamingPassword = string.Empty;

    [ObservableProperty]
    private string preSharedKeysText = string.Empty;

    [ObservableProperty]
    private string saveStatus = string.Empty;

    [ObservableProperty]
    private bool saveStatusIsError;

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

        Languages = new ObservableCollection<LanguageOption>
        {
            new(Language.FollowSystemLanguageCode, "Follow system")
        };
        foreach (var pair in Language.GetAvailableLanguages().OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase))
            Languages.Add(new LanguageOption(pair.Key, pair.Value));
        string currentLanguage = Language.NormalizeLanguagePreference(config.Idioma);
        selectedLanguage = Languages.FirstOrDefault(
            option => string.Equals(option.Code, currentLanguage, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];

        UseProxy = config.UsarProxy;
        ProxyHost = config.ProxyIP ?? string.Empty;
        ProxyPort = Math.Clamp(config.ProxyPort, 0, 65535);
        ProxyUser = config.ProxyUser ?? string.Empty;
        ProxyPassword = config.ProxyPassword ?? string.Empty;
        ProxyPasswordNeedsReentry = config.ProxyPasswordNeedsReentry;

        var schedule = config.DownloadSchedule;
        ScheduleEnabled = schedule.Enabled;
        ScheduleDaily = schedule.Daily;
        ScheduleStart = TimeSpan.FromMinutes(Math.Clamp(schedule.StartMinute, 0, 1439));
        ScheduleEnd = TimeSpan.FromMinutes(Math.Clamp(schedule.EndMinute, 0, 1439));

        StreamingEnabled = config.ServidorStreamingActivo;
        StreamingPort = Math.Clamp(config.ServidorStreamingPuerto, 0, 65535);
        StreamingPassword = config.ServidorStreamingPassword ?? string.Empty;
        StreamingPasswordNeedsReentry = config.ServidorStreamingPasswordNeedsReentry;

        var keys = new StringBuilder();
        if (config.ListaPreSharedKeys is not null)
        {
            foreach (SecureString key in config.ListaPreSharedKeys)
            {
                if (key is null)
                    continue;
                try
                {
                    keys.AppendLine(Criptografia.ToInsecureString(key));
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
        PreSharedKeysText = keys.ToString();
    }

    public ObservableCollection<LanguageOption> Languages { get; }

    public string[] ThemeOptions { get; } = { "Auto (system)", "Light", "Dark" };

    public string AppDataPath { get; }

    public string VersionText => AppInfo.Version;

    public bool ProxyPasswordNeedsReentry { get; }

    public bool StreamingPasswordNeedsReentry { get; }

    public bool HasSaveStatus => !string.IsNullOrWhiteSpace(SaveStatus);

    /// <summary>Set by the view so Browse can use the platform folder picker.</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        _theme.SetMode((ThemeMode)value);
    }

    partial void OnSaveStatusChanged(string value) => OnPropertyChanged(nameof(HasSaveStatus));

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
        if (ScheduleEnabled && ScheduleDaily)
        {
            int start = ToMinutes(ScheduleStart);
            int end = ToMinutes(ScheduleEnd);
            if (start == end)
            {
                SetSaveStatus("The schedule start and end times must differ.", true);
                return;
            }
        }

        if (UseProxy && string.IsNullOrWhiteSpace(ProxyHost))
        {
            SetSaveStatus("Enter a proxy host or turn the proxy off.", true);
            return;
        }

        var config = _engine.Config;
        config.RutaDefecto = DefaultFolder.Trim();
        config.DescargasSimultaneas = (int)Math.Clamp(SimultaneousDownloads, 1, 20);
        config.ConexionesPorFichero = (int)Math.Clamp(ConnectionsPerFile, 1, 16);
        config.CrearDirectorioPaquete = CreateSubdirectory;
        config.ExtraerAutomaticamente = ExtractArchives;
        config.ComenzarDescargando = StartOnLaunch;
        config.MantenerUltimaConfiguracion = KeepLastOptions;

        string language = SelectedLanguage?.Code ?? Language.FollowSystemLanguageCode;
        config.Idioma = language;
        try
        {
            Language.InitLanguage(language);
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Applying the selected language failed: " + Log.SafeException(ex));
        }

        config.UsarProxy = UseProxy;
        config.ProxyIP = ProxyHost.Trim();
        config.ProxyPort = (int)Math.Clamp(ProxyPort, 0, 65535);
        config.ProxyUser = ProxyUser.Trim();
        config.ProxyPassword = ProxyPassword;
        Conexion.SetProxy(config);

        var schedule = config.DownloadSchedule;
        schedule.Enabled = ScheduleEnabled;
        schedule.Daily = ScheduleDaily;
        if (ScheduleDaily)
        {
            schedule.StartMinute = ToMinutes(ScheduleStart);
            schedule.EndMinute = ToMinutes(ScheduleEnd);
        }

        config.ServidorStreamingActivo = StreamingEnabled;
        config.ServidorStreamingPuerto = (int)Math.Clamp(StreamingPort, 0, 65535);
        config.ServidorStreamingPassword = StreamingPassword;

        var keys = new List<SecureString>();
        foreach (string line in PreSharedKeysText.Split('\n'))
        {
            string key = line.Trim();
            if (key.Length > 0)
                keys.Add(Criptografia.ToSecureString(key));
        }
        config.ListaPreSharedKeys = keys;

        config.GuardarXML(true);
        if (config.ErrorConfig != Configuracion.ErrorConfigClass.SinErrores)
        {
            SetSaveStatus("Configuration could not be saved: " + config.ErrorConfig + ".", true);
            return;
        }

        SetSaveStatus($"Saved at {DateTime.Now:HH:mm:ss}.", false);
    }

    private static int ToMinutes(TimeSpan? value)
    {
        TimeSpan time = value ?? TimeSpan.Zero;
        int minutes = (int)Math.Round(time.TotalMinutes);
        return Math.Clamp(minutes, 0, 1439);
    }

    private void SetSaveStatus(string text, bool isError)
    {
        SaveStatus = text;
        SaveStatusIsError = isError;
    }
}

public sealed class LanguageOption
{
    public LanguageOption(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; }

    public string Name { get; }

    public override string ToString() => Name;
}
