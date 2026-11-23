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

    /// <summary>Keeps the page's batch selection in sync with the list's selection.</summary>
    private void QueueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DownloadsPageViewModel model || sender is not ListBox list)
            return;

        model.SelectedDownloads.Clear();
        foreach (var item in list.SelectedItems)
        {
            if (item is DownloadItemViewModel row)
                model.SelectedDownloads.Add(row);
        }
        model.UpdateBatchSelection();
    }
}
