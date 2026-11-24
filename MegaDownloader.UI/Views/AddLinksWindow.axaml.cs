using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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

        // Shift-click toggles a range of checkboxes, the same gesture the queue uses.
        // Tunnelling: the row checkbox must not also toggle itself on release.
        AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Tunnel);

        // A container dropped while the window is open joins the next batch instead of
        // opening a second window.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

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

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        if (DataContext is not AddLinksViewModel model)
            return;

        if (model.IsStepTwo)
        {
            if (model.RecheckCommand.CanExecute(null))
                model.RecheckCommand.Execute(null);
        }
        else if (model.CheckCommand.CanExecute(null))
        {
            model.CheckCommand.Execute(null);
        }
        e.Handled = true;
    }

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

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not AddLinksViewModel model || PreflightList is null)
            return;
        if (!e.GetCurrentPoint(PreflightList).Properties.IsLeftButtonPressed)
            return;

        PreflightRowViewModel? row = FindRow(e.Source as Visual);
        if (row is null)
            return;

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            model.SetRangeAnchor(row);
            return;
        }

        // Marked handled so the checkbox under the pointer does not toggle itself again.
        model.ExtendSelectionTo(row);
        e.Handled = true;
    }

    private PreflightRowViewModel? FindRow(Visual? source)
    {
        if (source is null)
            return null;
        foreach (var ancestor in source.GetSelfAndVisualAncestors())
        {
            if (ReferenceEquals(ancestor, PreflightList))
                return null;
            if (ancestor is ListBoxItem { DataContext: PreflightRowViewModel row })
                return row;
        }
        return null;
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
                    coordinator.Stage(unpacked);
                return;
            }
        }

        string? text = e.DataTransfer.TryGetText();
        if (!string.IsNullOrWhiteSpace(text))
            coordinator.Stage(text);
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
