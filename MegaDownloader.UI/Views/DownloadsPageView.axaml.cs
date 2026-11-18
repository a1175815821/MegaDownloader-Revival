using Avalonia.Controls;
using Avalonia.Input;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Views;

public partial class DownloadsPageView : UserControl
{
    public DownloadsPageView()
    {
        InitializeComponent();
    }

    private void RowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: DownloadItemViewModel row })
            row.PropertiesCommand.Execute(null);
    }
}
