using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Views;

public partial class SettingsPageView : UserControl
{
    public SettingsPageView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsPageViewModel viewModel)
            viewModel.PickFolderAsync = PickFolderAsync;
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose default download folder",
                AllowMultiple = false
            });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
