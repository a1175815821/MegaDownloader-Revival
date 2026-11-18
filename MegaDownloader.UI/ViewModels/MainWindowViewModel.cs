using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private readonly ClipboardMonitor _clipboard;

    [ObservableProperty]
    private NavigationItemViewModel? selectedNavigationItem;

    [ObservableProperty]
    private ViewModelBase? currentPage;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string themeLabel = "Theme: Auto";

    public MainWindowViewModel(
        INavigationService navigation,
        IThemeService theme,
        IDialogService dialogs,
        ClipboardMonitor clipboard,
        DownloadsPageViewModel downloadsPage,
        SettingsPageViewModel settingsPage,
        StreamingPageViewModel streamingPage)
    {
        _navigation = navigation;
        _theme = theme;
        _dialogs = dialogs;
        _clipboard = clipboard;
        DownloadsPage = downloadsPage;

        // Clipboard monitoring is opt-in through the configuration; the monitor itself
        // checks the setting on every tick, so toggling it needs no restart.
        _clipboard.IntakeCompleted += result =>
        {
            if (result.Status is LinkIntakeStatus.Added or LinkIntakeStatus.Failed or LinkIntakeStatus.NotReady)
                StatusText = result.Message;
        };
        _clipboard.Start();

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("downloads", "Downloads", downloadsPage),
            new("streaming", "Streaming", streamingPage),
            new("settings", "Settings", settingsPage)
        };

        _navigation.CurrentPageChanged += (_, _) => CurrentPage = _navigation.CurrentPage;

        SelectedNavigationItem = NavigationItems[0];
        CurrentPage = _navigation.CurrentPage;
        UpdateThemeLabel();
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    /// <summary>The queue page owns the live start/pause/stop state used by the toolbar.</summary>
    public DownloadsPageViewModel DownloadsPage { get; }

    public string VersionText => AppInfo.Version;

    public string RuntimeText => AppInfo.Runtime;

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        if (value is not null)
            _navigation.NavigateTo(value.Page);
    }

    [RelayCommand]
    private Task AddLinksAsync() => _dialogs.ShowAddLinksAsync();

    [RelayCommand]
    private Task EncodeLinksAsync() => _dialogs.ShowEncodeLinksAsync();

    [RelayCommand]
    private Task GenerateElcAsync() => _dialogs.ShowElcGenerateAsync();

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
        UpdateThemeLabel();
    }

    private void UpdateThemeLabel() => ThemeLabel = $"Theme: {_theme.Mode}";
}
