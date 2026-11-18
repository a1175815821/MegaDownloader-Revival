using Avalonia.Controls;
using Avalonia.Input.Platform;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Views;

public partial class EncodeLinksWindow : Window
{
    public EncodeLinksWindow()
    {
        InitializeComponent();

        var viewModel = new EncodeLinksViewModel
        {
            CopyAsync = CopyAsync
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
}
