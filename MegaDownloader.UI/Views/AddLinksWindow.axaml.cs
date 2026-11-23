using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using MegaDownloader.UI.Services;
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
        Opened += async (_, _) =>
        {
            LinkInput.Focus();
            await viewModel.BeginAsync(InitialText);
        };
        Closed += (_, _) => viewModel.Dispose();
    }

    public string? InitialText { get; init; }

    public string? Outcome => (DataContext as AddLinksViewModel)?.Status;

    /// <summary>Shows how many links arrived while this window was open; they open as
    /// the next batch instead of a second window.</summary>
    public void ShowPendingBatch(int count) =>
        (DataContext as AddLinksViewModel)?.SetPendingBatch(count);

    private async void PasteLinks(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddLinksViewModel model)
            return;
        try
        {
            string? text = Clipboard is null ? null : await Clipboard.TryGetTextAsync();
            if (!IsVisible || model.IsInspecting || model.IsImporting)
                return;
            var links = LinkIntakeService.ExtractLinks(text);
            if (links.Count == 0)
            {
                model.ClipboardNotice("No valid MEGA links were found.");
                return;
            }
            model.LinksText = string.Join(Environment.NewLine, links);
            LinkInput.Focus();
            LinkInput.CaretIndex = model.LinksText.Length;
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Could not read the clipboard: " + Log.SafeException(ex));
            model.ClipboardNotice("No valid MEGA links were found.");
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = UiText.Get("Choose download folder"),
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
