using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MegaDownloader.UI.ViewModels;

/// <summary>
/// Root view model for the main download workbench. This is the cross-platform
/// replacement for the WinForms Main form; the download engine will be wired in
/// as the core library compile fixes land.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();

    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private int activeCount;
    [ObservableProperty] private bool isPaused;
}

public partial class DownloadItemViewModel : ObservableObject
{
    [ObservableProperty] private string fileName = string.Empty;
    [ObservableProperty] private string state = "Queued";
    [ObservableProperty] private double progress;
    [ObservableProperty] private string speed = string.Empty;
}
