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
    private readonly IPlatformServices _platform;
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
    private bool watchClipboard;

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

    /// <summary>Global speed limit in KB/s (0 = unlimited).</summary>
    [ObservableProperty]
    private int globalSpeedLimitKBs;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private string vlcPath = string.Empty;

    [ObservableProperty]
    private bool streamingEnabled;

    [ObservableProperty]
    private decimal streamingPort;

    [ObservableProperty]
    private string streamingPassword = string.Empty;

    [ObservableProperty]
    private string preSharedKeysText = string.Empty;

    [ObservableProperty]
    private bool webServerEnabled;

    [ObservableProperty]
    private decimal webServerPort;

    [ObservableProperty]
    private string webServerPassword = string.Empty;

    [ObservableProperty]
    private bool webServerAllowLan;

    [ObservableProperty]
    private string webServerBindIp = string.Empty;

    [ObservableProperty]
    private string webServerTitle = string.Empty;

    [ObservableProperty]
    private decimal webServerTimeout;

    [ObservableProperty]
    private string saveStatus = string.Empty;

    [ObservableProperty]
    private bool saveStatusIsError;

    public SettingsPageViewModel(IThemeService theme, IPlatformServices platform, DownloadEngineService engine)
    {
        _theme = theme;
        _platform = platform;
        _engine = engine;
        AppDataPath = platform.GetAppDataDirectory("MegaDownloader");
        selectedThemeIndex = (int)theme.Mode;

        // The toolbar button and this page both change the theme. Without this the
        // page kept showing the old selection after the toolbar cycled it, and
        // picking the stale entry did nothing.
        _theme.ModeChanged += (_, _) => SyncThemeFromService();

        var config = engine.Config;
        DefaultFolder = config.RutaDefecto ?? string.Empty;
        SimultaneousDownloads = Math.Clamp(config.DescargasSimultaneas, 1, 20);
        ConnectionsPerFile = Math.Clamp(config.ConexionesPorFichero, 1, 16);
        CreateSubdirectory = config.CrearDirectorioPaquete;
        ExtractArchives = config.ExtraerAutomaticamente;
        StartOnLaunch = config.ComenzarDescargando;
        KeepLastOptions = config.MantenerUltimaConfiguracion;
        WatchClipboard = config.AnalizarPortapapeles;

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

        GlobalSpeedLimitKBs = Math.Clamp(config.LimiteVelocidadKBs, 0, int.MaxValue);
        StartWithWindows = config.IniciarConWindows;
        VlcPath = config.VLCPath ?? string.Empty;

        StreamingEnabled = config.ServidorStreamingActivo;
        StreamingPort = Math.Clamp(config.ServidorStreamingPuerto, 0, 65535);
        StreamingPassword = config.ServidorStreamingPassword ?? string.Empty;
        StreamingPasswordNeedsReentry = config.ServidorStreamingPasswordNeedsReentry;

        WebServerEnabled = config.ServidorWebActivo;
        WebServerPort = Math.Clamp(config.ServidorWebPuerto, 0, 65535);
        WebServerPassword = config.ServidorWebPassword ?? string.Empty;
        WebServerAllowLan = config.ServidorWebPermitirLAN;
        WebServerBindIp = config.ServidorWebBindIP ?? string.Empty;
        WebServerTitle = config.ServidorWebNombre ?? string.Empty;
        WebServerTimeout = Math.Clamp(config.ServidorWebTimeout, 0, 99);

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

    public bool ProxyPasswordNeedsReentry { get; private set; }

    public bool StreamingPasswordNeedsReentry { get; private set; }

    public bool HasSaveStatus => !string.IsNullOrWhiteSpace(SaveStatus);

    /// <summary>Set by the view so Browse can use the platform folder picker.</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    /// <summary>
    /// Reflects a theme change that came from another surface (the toolbar). The page
    /// stages its own selection and applies it on Save, so the two never fight.
    /// </summary>
    private void SyncThemeFromService()
    {
        int index = (int)_theme.Mode;
        if (SelectedThemeIndex == index)
            return;

        SelectedThemeIndex = index;
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
    private async Task SaveAsync()
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
        config.AnalizarPortapapeles = WatchClipboard;

        // The theme is staged on this page and applied here, like every other setting.
        if (_theme.Mode != (ThemeMode)SelectedThemeIndex)
            _theme.SetMode((ThemeMode)SelectedThemeIndex);

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
        // The user just typed the password, so the "cannot be decrypted" warning is
        // resolved. Leaving it up would keep nagging after a successful save.
        config.ProxyPasswordNeedsReentry = false;
        ProxyPasswordNeedsReentry = false;
        Conexion.SetProxy(config);

        var schedule = config.DownloadSchedule;
        schedule.Enabled = ScheduleEnabled;
        schedule.Daily = ScheduleDaily;
        if (ScheduleDaily)
        {
            schedule.StartMinute = ToMinutes(ScheduleStart);
            schedule.EndMinute = ToMinutes(ScheduleEnd);
        }

        // The global limit is applied to the running transfers, not only stored.
        config.LimiteVelocidadKBs = Math.Clamp(GlobalSpeedLimitKBs, 0, int.MaxValue);
        ThrottledStreamController.GetController().SetMaxGlobalSpeed(config.LimiteVelocidadKBs * 1024L);

        config.IniciarConWindows = StartWithWindows;
        config.VLCPath = VlcPath.Trim();

        config.ServidorStreamingActivo = StreamingEnabled;
        config.ServidorStreamingPuerto = (int)Math.Clamp(StreamingPort, 0, 65535);
        config.ServidorStreamingPassword = StreamingPassword;
        config.ServidorStreamingPasswordNeedsReentry = false;
        StreamingPasswordNeedsReentry = false;

        config.ServidorWebActivo = WebServerEnabled;
        config.ServidorWebPuerto = (int)Math.Clamp(WebServerPort, 0, 65535);
        config.ServidorWebPassword = WebServerPassword;
        config.ServidorWebPermitirLAN = WebServerAllowLan;
        config.ServidorWebBindIP = WebServerBindIp.Trim();
        config.ServidorWebNombre = WebServerTitle.Trim();
        config.ServidorWebTimeout = (int)Math.Clamp(WebServerTimeout, 0, 99);

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

        // The schedule controller caches the window it last saw, so an edited
        // schedule only took effect when the window changed by itself.
        _engine.ApplyScheduleSettings();

        // Start with Windows is applied by the platform layer (registry on Windows,
        // autostart entries elsewhere). A failure here must not lose the rest of
        // the settings, so it is reported but not fatal.
        try
        {
            string executable = Environment.ProcessPath ?? string.Empty;
            if (executable.Length > 0)
                _platform.SetAutoStart(StartWithWindows, executable, string.Empty);
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Could not update the start-with-Windows entry: " + Log.SafeException(ex));
            SetSaveStatus("Saved, but the start-with-Windows entry could not be updated.", true);
            return;
        }

        // Restart the servers so port/password changes take effect immediately. The
        // streaming server and the remote controller are independent, so a failure in
        // one is reported without hiding the other. Off the UI thread: starting Kestrel
        // must not freeze the window.
        string serverError = await Task.Run(() => WebServerHost.Start(_engine, config));
        if (!string.IsNullOrEmpty(serverError))
        {
            SetSaveStatus("Saved, but the web server could not start: " + serverError, true);
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
