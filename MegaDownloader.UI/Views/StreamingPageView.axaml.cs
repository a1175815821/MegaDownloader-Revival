using Avalonia.Controls;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Views;

public partial class StreamingPageView : UserControl
{
    public StreamingPageView()
    {
        InitializeComponent();

        // Playback: the resolved stream URL is opened with the configured player.
        if (DataContext is StreamingPageViewModel viewModel)
            viewModel.OpenStreamAsync = OpenStreamAsync;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is StreamingPageViewModel viewModel)
            viewModel.OpenStreamAsync = OpenStreamAsync;
    }

    private async Task OpenStreamAsync(string url)
    {
        // The stream server runs on loopback, so the default browser can play it.
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is null)
            return;
        await launcher.LaunchUriAsync(new Uri(url));
    }
}
