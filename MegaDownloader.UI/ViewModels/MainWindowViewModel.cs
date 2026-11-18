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
        DownloadsPageViewModel downloadsPage,
        SettingsPageViewModel settingsPage)
    {
        _navigation = navigation;
        _theme = theme;
        _dialogs = dialogs;

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("downloads", "Downloads", downloadsPage),
            new("settings", "Settings", settingsPage)
        };

        _navigation.CurrentPageChanged += (_, _) => CurrentPage = _navigation.CurrentPage;

        SelectedNavigationItem = NavigationItems[0];
        CurrentPage = _navigation.CurrentPage;
        UpdateThemeLabel();
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public string VersionText => AppInfo.Version;

    public string RuntimeText => AppInfo.Runtime;

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        if (value is not null)
            _navigation.NavigateTo(value.Page);
    }

    [RelayCommand]
    private Task AddLinksAsync() => _dialogs.ShowMessageAsync(
        "Add links",
        "Link intake and preflight arrive in Phase 3 of the Avalonia refactor. The queue page already runs the shared download engine.");

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
