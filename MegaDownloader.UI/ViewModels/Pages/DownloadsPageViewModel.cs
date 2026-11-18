using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MegaDownloader.UI.ViewModels;

public partial class DownloadsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string status = "No active downloads";

    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();
}

/// <summary>
/// Placeholder row shape for the future virtualized queue. The real item model is
/// defined by the Core engine port in Phase 1.
/// </summary>
public partial class DownloadItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private string state = "Queued";

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string speed = string.Empty;
}
