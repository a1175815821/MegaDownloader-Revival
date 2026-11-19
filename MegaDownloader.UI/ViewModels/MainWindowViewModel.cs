using Avalonia.Media;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private readonly DownloadsPageViewModel _downloads;

    [ObservableProperty]
    private NavigationItemViewModel? selectedNavigationItem;

    [ObservableProperty]
    private ViewModelBase? currentPage;

    [ObservableProperty]
    private string statusText = UiText.Get("Ready");

    [ObservableProperty]
    private string themeLabel = string.Empty;

    public MainWindowViewModel(
        INavigationService navigation,
        IThemeService theme,
        IDialogService dialogs,
        ClipboardMonitor clipboard,
        ActivationService activation,
        DownloadsPageViewModel downloadsPage,
        SettingsPageViewModel settingsPage,
        StreamingPageViewModel streamingPage)
    {
        _navigation = navigation;
        _theme = theme;
        _dialogs = dialogs;
        _downloads = downloadsPage;

        // Clipboard monitoring is opt-in through the configuration; the monitor itself
        // checks the setting on every tick, so toggling it needs no restart.
        clipboard.IntakeCompleted += result =>
        {
            if (result.Status is LinkIntakeStatus.Added or LinkIntakeStatus.Failed or LinkIntakeStatus.NotReady or LinkIntakeStatus.AllDuplicates)
                StatusText = IntakeMessage(result);
        };
        clipboard.Start();

        // A mega:// activation (or a second launch) is reported in the status bar too:
        // without this the user sees no feedback at all when a link arrives.
        activation.IntakeCompleted += result =>
        {
            if (result.Status is LinkIntakeStatus.Added or LinkIntakeStatus.Failed
                or LinkIntakeStatus.NotReady or LinkIntakeStatus.AllDuplicates)
                StatusText = IntakeMessage(result);
        };

        // The settings page can change the theme too; the toolbar label follows.
        _theme.ModeChanged += (_, _) => UpdateThemeLabel();

        NavigationItems =
        [
            new("Downloads", GetIcon("Downloads"), downloadsPage),
            new("Streaming library", GetIcon("Library"), streamingPage),
            new("Settings", GetIcon("Settings"), settingsPage)
        ];
        UiText.Changed += (_, _) =>
        {
            foreach (var item in NavigationItems)
                item.ApplyLanguage();
            UpdateThemeLabel();
            StatusText = UiText.Get("Ready");
        };

        _navigation.CurrentPageChanged += (_, _) => CurrentPage = _navigation.CurrentPage;

        SelectedNavigationItem = NavigationItems[0];
        CurrentPage = _navigation.CurrentPage;
        UpdateThemeLabel();
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    private static string IntakeMessage(LinkIntakeResult result) => result.Status switch
    {
        LinkIntakeStatus.Added => UiText.Format("Added {0} file(s) to the queue.", result.AddedCount),
        LinkIntakeStatus.AllDuplicates => UiText.Get("All selected links are already queued."),
        LinkIntakeStatus.NotReady when result.Message == "The download queue is still loading." =>
            UiText.Get("The download queue is still loading. Try again in a moment."),
        LinkIntakeStatus.NotReady when result.Message == "The download queue is not ready." =>
            UiText.Get("The queue is not ready yet. Try again in a moment."),
        _ => UiText.Get(result.Message)
    };

    private static Geometry GetIcon(string name) =>
        Application.Current!.Resources.TryGetResource("Icon." + name, null, out object? resource) && resource is Geometry icon
            ? icon
            : Geometry.Parse("M0,0");

    public string VersionText => AppInfo.Version;

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        if (value is not null)
            _navigation.NavigateTo(value.Page);
    }

    [RelayCommand]
    private Task AddLinksAsync() => _downloads.AddLinksCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task ShowGuideAsync()
    {
        if (await _dialogs.ShowGuideAsync())
            await AddLinksAsync();
    }

    [RelayCommand]
    private void CycleTheme()
    {
        var next = _theme.Mode switch
        {
            ThemeMode.Auto => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.Auto
        };

        _theme.SetMode(next);
    }

    public Geometry ThemeIcon => GetIcon(_theme.Mode switch { ThemeMode.Dark => "Moon", ThemeMode.Light => "Theme", _ => "SystemTheme" });

    private void UpdateThemeLabel()
    {
        ThemeLabel = UiText.Format("Theme: {0}", UiText.Get(_theme.Mode.ToString()));
        OnPropertyChanged(nameof(ThemeIcon));
    }
}