using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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

        // Dropping links or an .elc container opens the same confirmation window as the
        // clipboard and mega:// activations, so every intake path ends in one place.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

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

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        bool accepts = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text);
        e.DragEffects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        var coordinator = App.Services.GetRequiredService<AddLinksCoordinator>();

        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } files)
        {
            var paths = new List<string>();
            foreach (var file in files)
            {
                if (file.TryGetLocalPath() is { } path)
                    paths.Add(path);
            }

            if (paths.Count > 0)
            {
                string? unpacked = await coordinator.ReadContainersAsync(paths);
                if (!string.IsNullOrEmpty(unpacked))
                    await coordinator.ShowAsync(unpacked);
                return;
            }
        }

        string? text = e.DataTransfer.TryGetText();
        if (!string.IsNullOrWhiteSpace(text) && LinkIntakeService.ExtractLinks(text).Count > 0)
            await coordinator.ShowAsync(text);
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
