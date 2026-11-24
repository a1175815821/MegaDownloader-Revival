using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MegaDownloader.UI.Services;
using MegaDownloader.UI.ViewModels.Pages;

namespace MegaDownloader.UI.Views;

public partial class CloudDrivePageView : UserControl
{
    public CloudDrivePageView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not CloudDrivePageViewModel model)
            return;
        model.PickFolderAsync = PickFolderAsync;
        model.EditSubscriptionAsync = settings => SubscriptionSettingsWindow.ShowAsync(this, settings);
        model.ShowDownloadPreviewAsync = request => CloudDownloadWindow.ShowAsync(this, request);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is CloudDrivePageViewModel model)
            model.Initialize();
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = UiText.Get("Choose a folder"),
                AllowMultiple = false
            });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void RowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(visual => visual is CheckBox))
            return;
        if (sender is Control { DataContext: CloudDriveRowViewModel row })
            row.OpenCommand.Execute(null);
    }
}
