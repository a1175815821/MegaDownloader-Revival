using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MegaDownloader.UI.Services;
using MegaDownloader.UI.ViewModels.Pages;

namespace MegaDownloader.UI.Views;

public partial class CloudDownloadWindow : Window
{
    public CloudDownloadWindow()
    {
        InitializeComponent();
    }

    public static async Task<CloudDownloadConfirmation?> ShowAsync(Visual owner, CloudDownloadRequest request)
    {
        Window? parent = TopLevel.GetTopLevel(owner) as Window;
        var model = new CloudDownloadViewModel(request)
        {
            PickFolderAsync = async () =>
            {
                if (parent is null)
                    return null;
                var folders = await parent.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = UiText.Get("Choose a folder"),
                    AllowMultiple = false
                });
                return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            }
        };
        var window = new CloudDownloadWindow { DataContext = model };
        var completion = new TaskCompletionSource<CloudDownloadConfirmation?>();
        model.Confirmed += (_, _) =>
        {
            completion.TrySetResult(new CloudDownloadConfirmation
            {
                Plan = model.ConfirmedPlan,
                Destination = model.Destination.Trim()
            });
            window.Close();
        };
        model.Cancelled += (_, _) =>
        {
            completion.TrySetResult(null);
            window.Close();
        };
        window.Closed += (_, _) => completion.TrySetResult(null);
        if (parent is not null)
            await window.ShowDialog(parent);
        else
            window.Show();
        return await completion.Task;
    }
}
