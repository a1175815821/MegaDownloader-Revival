using System.Collections.ObjectModel;
using System.Security;
using System.Text;
using Avalonia.Threading;
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

    [ObservableProperty]
    private DateTimeOffset? scheduleOnceStartDate;

    [ObservableProperty]
    private TimeSpan? scheduleOnceStartTime;

    [ObservableProperty]
    private DateTimeOffset? scheduleOnceEndDate;

    [ObservableProperty]
    private TimeSpan? scheduleOnceEndTime;

    [ObservableProperty]
    private string scheduleSummary = string.Empty;

    /// <summary>The values that are actually in force right now, shown after saving.</summary>
    [ObservableProperty]
    private string effectiveSummary = string.Empty;

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
        UiText.Changed += (_, _) =>
        {
            Languages[0].ApplyLanguage();
            OnPropertyChanged(nameof(ThemeOptions));
        };

        var config = engine.Config;
        DefaultFolder = LinkIntakeService.DownloadFolder(config);
        SimultaneousDownloads = Math.Clamp(config.DescargasSimultaneas, 1, 20);
        ConnectionsPerFile = Math.Clamp(config.ConexionesPorFichero, 1, 16);
        CreateSubdirectory = config.CrearDirectorioPaquete;
        ExtractArchives = config.ExtraerAutomaticamente;
        StartOnLaunch = config.ComenzarDescargando;
        KeepLastOptions = config.MantenerUltimaConfiguracion;
        WatchClipboard = config.AnalizarPortapapeles;

        Languages = new ObservableCollection<LanguageOption>
        {
            new(Language.FollowSystemLanguageCode, UiText.Get("Follow system"))
        };
        foreach (var pair in Language.GetAvailableLanguages().OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase))
            Languages.Add(new LanguageOption(pair.Key, NativeLanguageName(pair.Key, pair.Value)));
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
        ScheduleOnceStartDate = new DateTimeOffset(schedule.OnceStart);
        ScheduleOnceStartTime = schedule.OnceStart.TimeOfDay;
        ScheduleOnceEndDate = new DateTimeOffset(schedule.OnceEnd);
        ScheduleOnceEndTime = schedule.OnceEnd.TimeOfDay;

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

        // Both summaries describe values that were all assigned above.
        UpdateScheduleSummary();
        UpdateEffectiveSummary();

        // The schedule summary says "allowed now" or "paused until …"; keep it truthful
        // while the page stays open across a window boundary.
        var scheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        scheduleTimer.Tick += (_, _) => UpdateScheduleSummary();
        scheduleTimer.Start();
    }

    private static string NativeLanguageName(string code, string fallback) => code switch
    {
        "en-US" => "English",
        "de-DE" => "Deutsch",
        "es-ES" => "Español",
        "fr-FR" => "Français",
        "hu-HU" => "Magyar",
        "it-IT" => "Italiano",
        "pt-BR" => "Português (Brasil)",
        "ro-RO" => "Română",
        "ru-RU" => "Русский",
        "zh-CN" => "简体中文",
        "zh-TW" => "繁體中文",
        _ => fallback
    };

    public ObservableCollection<LanguageOption> Languages { get; }

    public string[] ThemeOptions => [UiText.Get("Follow system"), UiText.Get("Light"), UiText.Get("Dark")];

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

    partial void OnScheduleEnabledChanged(bool value) => UpdateScheduleSummary();

    partial void OnScheduleDailyChanged(bool value) => UpdateScheduleSummary();

    partial void OnScheduleStartChanged(TimeSpan? value) => UpdateScheduleSummary();

    partial void OnScheduleEndChanged(TimeSpan? value) => UpdateScheduleSummary();

    partial void OnScheduleOnceStartDateChanged(DateTimeOffset? value) => UpdateScheduleSummary();

    partial void OnScheduleOnceStartTimeChanged(TimeSpan? value) => UpdateScheduleSummary();

    partial void OnScheduleOnceEndDateChanged(DateTimeOffset? value) => UpdateScheduleSummary();

    partial void OnScheduleOnceEndTimeChanged(TimeSpan? value) => UpdateScheduleSummary();

    /// <summary>
    /// Says, in the schedule editor itself, whether downloads are allowed right now and
    /// when the next window opens. The engine enforces the same rules every tick; this
    /// is the visible summary of that decision.
    /// </summary>
    private void UpdateScheduleSummary()
    {
        if (!ScheduleEnabled)
        {
            ScheduleSummary = UiText.Get("The schedule is off. Downloads start whenever you start the queue.");
            return;
        }

        DateTime now = DateTime.Now;
        if (ScheduleDaily)
        {
            int start = ToMinutes(ScheduleStart);
            int end = ToMinutes(ScheduleEnd);
            if (start == end)
            {
                ScheduleSummary = UiText.Get("The schedule start and end times must differ.");
                return;
            }

            double minute = now.TimeOfDay.TotalMinutes;
            bool active = start < end
                ? minute >= start && minute < end
                : minute >= start || minute < end;
            if (active)
            {
                DateTime pauseAt = now.Date.AddMinutes(end);
                if (start > end && minute >= start)
                    pauseAt = pauseAt.AddDays(1);
                ScheduleSummary = UiText.Format("Downloads are allowed now — the queue pauses at {0:HH:mm}.", pauseAt);
                return;
            }

            DateTime next = now.Date.AddMinutes(start);
            if (next <= now)
                next = next.AddDays(1);
            ScheduleSummary = UiText.Format("Downloads are paused until {0:ddd HH:mm}.", next);
            return;
        }

        DateTime onceStart = CombineDateAndTime(ScheduleOnceStartDate, ScheduleOnceStartTime);
        DateTime onceEnd = CombineDateAndTime(ScheduleOnceEndDate, ScheduleOnceEndTime);
        if (onceEnd <= onceStart)
        {
            ScheduleSummary = UiText.Get("Set the end time after the start time.");
            return;
        }

        if (now >= onceStart && now < onceEnd)
            ScheduleSummary = UiText.Format("Downloads are allowed now — this one-time window ends {0:g}.", onceEnd);
        else if (now < onceStart)
            ScheduleSummary = UiText.Format("Runs once from {0:g} to {1:g}.", onceStart, onceEnd);
        else
            ScheduleSummary = UiText.Get("The one-time window has already ended.");
    }

    private void UpdateEffectiveSummary()
    {
        string folder = string.IsNullOrWhiteSpace(DefaultFolder) ? UiText.Get("(not set)") : DefaultFolder.Trim();
        string limit = GlobalSpeedLimitKBs > 0
            ? UiText.Format("{0} KB/s", GlobalSpeedLimitKBs)
            : UiText.Get("Unlimited");
        string proxy = UseProxy
            ? UiText.Format("{0}:{1}", ProxyHost.Trim(), (int)ProxyPort)
            : UiText.Get("Off");
        EffectiveSummary = UiText.Format("{0} · {1} parallel · {2} connections each · limit {3} · proxy {4}",
            folder, (int)SimultaneousDownloads, (int)ConnectionsPerFile, limit, proxy);
    }

    private static DateTime CombineDateAndTime(DateTimeOffset? date, TimeSpan? time)
    {
        DateTime day = (date ?? DateTimeOffset.Now).Date;
        return day.Add(time ?? TimeSpan.Zero);
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
    private async Task SaveAsync()
    {
        DateTime onceStart = default;
        DateTime onceEnd = default;
        if (ScheduleEnabled && ScheduleDaily)
        {
            int start = ToMinutes(ScheduleStart);
            int end = ToMinutes(ScheduleEnd);
            if (start == end)
            {
                SetSaveStatus(UiText.Get("The schedule start and end times must differ."), true);
                return;
            }
        }
        else if (ScheduleEnabled)
        {
            onceStart = CombineDateAndTime(ScheduleOnceStartDate, ScheduleOnceStartTime);
            onceEnd = CombineDateAndTime(ScheduleOnceEndDate, ScheduleOnceEndTime);
            if (onceEnd <= onceStart)
            {
                SetSaveStatus(UiText.Get("Set the end time after the start time."), true);
                return;
            }
        }

        if (UseProxy && string.IsNullOrWhiteSpace(ProxyHost))
        {
            SetSaveStatus(UiText.Get("Enter a proxy host or turn the proxy off."), true);
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

        string previousLanguage = config.Idioma;
        string language = SelectedLanguage?.Code ?? Language.FollowSystemLanguageCode;
        config.Idioma = language;

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
        else if (ScheduleEnabled)
        {
            schedule.OnceStart = onceStart;
            schedule.OnceEnd = onceEnd;
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
            config.Idioma = previousLanguage;
            SetSaveStatus(UiText.Format("Configuration could not be saved: {0}.", config.ErrorConfig), true);
            return;
        }

        int themeIndex = SelectedThemeIndex;
        bool languageApplied = language == previousLanguage || UiText.ApplyLanguage(language);
        if (Enum.IsDefined(typeof(ThemeMode), themeIndex) && _theme.Mode != (ThemeMode)themeIndex)
            _theme.SetMode((ThemeMode)themeIndex);
        SelectedThemeIndex = (int)_theme.Mode;

        // The schedule controller caches the window it last saw, so an edited
        // schedule only took effect when the window changed by itself.
        _engine.ApplyScheduleSettings();
        UpdateScheduleSummary();
        UpdateEffectiveSummary();

        // Start with Windows is applied by the platform layer (registry on Windows,
        // autostart entries elsewhere). A failure here must not lose the rest of
        // the settings, so it is reported but not fatal.
        bool autoStartApplied = true;
        try
        {
            string executable = Environment.ProcessPath ?? string.Empty;
            if (ApplicationDataDirectory.Override is null && executable.Length > 0)
                _platform.SetAutoStart(StartWithWindows, executable, string.Empty);
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Could not update the start-with-Windows entry: " + Log.SafeException(ex));
            autoStartApplied = false;
        }

        // Restart the servers so port/password changes take effect immediately. The
        // streaming server and the remote controller are independent, so a failure in
        // one is reported without hiding the other. Off the UI thread: starting Kestrel
        // must not freeze the window.
        string serverError = await Task.Run(() => WebServerHost.Start(_engine, config));
        if (!string.IsNullOrEmpty(serverError))
        {
            SetSaveStatus(UiText.Format("Saved, but the web server could not start: {0}", serverError), true);
            return;
        }

        if (!languageApplied)
            SetSaveStatus(UiText.Get("Saved, but the language could not be applied."), true);
        else if (!autoStartApplied)
            SetSaveStatus(UiText.Get("Saved, but the start-with-Windows entry could not be updated."), true);
        else
            SetSaveStatus(UiText.Format("Saved at {0:HH:mm:ss}.", DateTime.Now), false);
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

public sealed class LanguageOption : ObservableObject
{
    public LanguageOption(string code, string name)
    {
        Code = code;
        _name = name;
    }

    public string Code { get; }

    private readonly string _name;

    public string Name => Code == Language.FollowSystemLanguageCode ? UiText.Get(_name) : _name;

    internal void ApplyLanguage() => OnPropertyChanged(nameof(Name));

    public override string ToString() => Name;
}
