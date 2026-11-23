using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MegaDownloader.UI.Services;
using MegaDownloader.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is not MainWindowViewModel model)
                return;

            // Ctrl/Cmd+F focuses the queue search box instead of opening a dialog.
            model.SearchRequested += () => Dispatcher.UIThread.Post(FocusQueueSearch);

            if (!App.Services.GetRequiredService<ISettingsService>().Current.HasSeenGuide &&
                model.ShowGuideCommand.CanExecute(null))
                model.ShowGuideCommand.Execute(null);
        };
    }

    private void FocusQueueSearch()
    {
        var search = this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(box => box.Name == "QueueSearch");
        if (search is not { IsVisible: true })
            return;
        search.Focus();
        search.SelectAll();
    }
}
