using MegaDownloader.UI.Services;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using MegaDownloader.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI.Views;

public partial class ElcGenerateWindow : Window
{
    public ElcGenerateWindow()
    {
        InitializeComponent();

        var viewModel = new ElcGenerateViewModel(App.Services.GetRequiredService<DownloadEngineService>())
        {
            CopyAsync = CopyAsync,
            SaveFileAsync = SaveFileAsync,
            OpenAccountsAsync = OpenAccountsAsync
        };
        viewModel.CloseRequested += Close;
        DataContext = viewModel;
    }

    private async Task CopyAsync(string text)
    {
        var clipboard = Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    private async Task SaveFileAsync(string text)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UiText.Get("Save ELC links"),
            SuggestedFileName = "links.elc",
            DefaultExtension = "txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(UiText.Get("Text")) { Patterns = new[] { "*.txt", "*.elc" } }
            }
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
    }

    private async Task OpenAccountsAsync()
    {
        var window = new ElcAccountsWindow();
        await window.ShowDialog(this);
    }
}
