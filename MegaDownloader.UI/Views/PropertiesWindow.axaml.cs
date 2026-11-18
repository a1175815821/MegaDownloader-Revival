using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Views;

public partial class PropertiesWindow : Window
{
    public PropertiesWindow(object item)
    {
        InitializeComponent();

        var viewModel = new PropertiesViewModel(item);
        viewModel.PickFolderAsync = PickFolderAsync;
        viewModel.CloseRequested += result => Close(result);
        DataContext = viewModel;
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose download folder",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
