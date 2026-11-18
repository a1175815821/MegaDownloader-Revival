using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MegaDownloader.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI.Views;

public partial class AddLinksWindow : Window
{
    public AddLinksWindow()
    {
        InitializeComponent();

        var viewModel = App.Services.GetRequiredService<AddLinksViewModel>();
        viewModel.PickFolderAsync = PickFolderAsync;
        viewModel.CloseRequested += result => Close(result);
        DataContext = viewModel;

        Closed += (_, _) => viewModel.Dispose();
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
