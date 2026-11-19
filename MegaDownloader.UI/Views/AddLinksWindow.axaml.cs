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
            if (!string.IsNullOrWhiteSpace(InitialText))
                viewModel.LinksText = InitialText;
            else if (viewModel.LinksText.Length == 0)
                await ReadClipboardAsync(viewModel, automatic: true);
            if (IsVisible && viewModel.CheckCommand.CanExecute(null))
                await viewModel.CheckCommand.ExecuteAsync(null);
        };
        Closed += (_, _) => viewModel.Dispose();
    }

    public string? InitialText { get; init; }

    public string? Outcome => (DataContext as AddLinksViewModel)?.Status;

    private async void PasteLinks(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AddLinksViewModel model)
            await ReadClipboardAsync(model, automatic: false);
    }

    private async Task ReadClipboardAsync(AddLinksViewModel model, bool automatic)
    {
        try
        {
            string? text = Clipboard is null ? null : await Clipboard.TryGetTextAsync();
            if (!IsVisible || model.IsInspecting || model.IsImporting || (automatic && model.LinksText.Length > 0))
                return;
            var links = LinkIntakeService.ExtractLinks(text);
            if (links.Count == 0)
            {
                if (!automatic)
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
            if (!automatic)
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
