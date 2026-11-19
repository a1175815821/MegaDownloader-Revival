using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
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
        if (e.Source is Control source && source.GetSelfAndVisualAncestors().Any(x => x is Button))
            return;

        if (sender is Control { DataContext: DownloadItemViewModel row })
            row.PropertiesCommand.Execute(null);
    }
}
