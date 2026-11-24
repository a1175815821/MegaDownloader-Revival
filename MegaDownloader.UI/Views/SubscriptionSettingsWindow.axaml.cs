using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MegaDownloader.UI.Services;
using MegaDownloader.UI.ViewModels.Pages;

namespace MegaDownloader.UI.Views;

public partial class SubscriptionSettingsWindow : Window
{
    public SubscriptionSettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>Edits a cloud-drive folder subscription.</summary>
    public static Task<MegaFolderSubscriptionSettings?> ShowAsync(Visual owner, MegaDriveSubscription settings) =>
        ShowAsync(owner, settings.ReadSettings(),
            string.IsNullOrEmpty(settings.Name) ? settings.Destination : settings.Name);

    /// <summary>Edits an anonymous public-folder subscription.</summary>
    public static Task<MegaFolderSubscriptionSettings?> ShowAsync(Visual owner, MegaPublicFolderSubscription settings) =>
        ShowAsync(owner, settings.ReadSettings(), settings.Caption);

    private static async Task<MegaFolderSubscriptionSettings?> ShowAsync(Visual owner,
        MegaFolderSubscriptionSettings settings, string title)
    {
        Window? parent = TopLevel.GetTopLevel(owner) as Window;
        var model = new SubscriptionSettingsViewModel(settings, title)
        {
            PickFolderAsync = async () =>
            {
                if (parent is null)
                    return null;
                var folders = await parent.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = UiText.Get("Save to"),
                    AllowMultiple = false
                });
                return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            }
        };
        var window = new SubscriptionSettingsWindow { DataContext = model };
        var completion = new TaskCompletionSource<MegaFolderSubscriptionSettings?>();
        model.Confirmed += result =>
        {
            completion.TrySetResult(result);
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
