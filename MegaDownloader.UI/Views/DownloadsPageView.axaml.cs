using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Views;

public partial class DownloadsPageView : UserControl
{
    /// <summary>
    /// Marks a queue-row drag. The row itself stays in <see cref="_dragSource"/>: a drag
    /// within one window never needs to serialize it, and the platform drag plumbing
    /// drops in-process formats before they reach the drop target.
    /// </summary>
    private static readonly DataFormat<string> QueueRowFormat =
        DataFormat.CreateStringApplicationFormat("MegaDownloader.QueueRow");

    private readonly string _dragToken = Guid.NewGuid().ToString("N");
    private PointerPressedEventArgs? _dragTrigger;
    private DownloadItemViewModel? _dragSource;
    private Point _dragOrigin;

    public DownloadsPageView()
    {
        InitializeComponent();

        // Reordering: drag a row onto another row, or use the row menu commands. Both
        // go through the engine's move path, so the queue order and its saved order
        // are the same either way.
        DragDrop.SetAllowDrop(QueueList, true);
        QueueList.AddHandler(PointerPressedEvent, OnRowPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        QueueList.AddHandler(PointerMovedEvent, OnRowPointerMoved, RoutingStrategies.Bubble);
        QueueList.AddHandler(DragDrop.DragOverEvent, OnRowDragOver);
        QueueList.AddHandler(DragDrop.DropEvent, OnRowDrop);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DownloadsPageViewModel model)
        {
            model.PickIdentityAsync = current => IdentityPickerWindow.ShowAsync(this, current);
            model.SetAvailableWidth(Bounds.Width);
        }
        SubscribeSelectionSync();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (DataContext is DownloadsPageViewModel model)
            model.SetAvailableWidth(e.NewSize.Width);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeSelectionSync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeSelectionSync();
        base.OnDetachedFromVisualTree(e);
    }

    private DownloadsPageViewModel? _subscribed;

    /// <summary>
    /// The page raises this after rearranging the rows; only the view can write the
    /// list's own selection.
    /// </summary>
    private void SubscribeSelectionSync()
    {
        UnsubscribeSelectionSync();
        _subscribed = DataContext as DownloadsPageViewModel;
        if (_subscribed is not null)
            _subscribed.SelectionSyncRequested += SyncQueueSelection;
    }

    private void UnsubscribeSelectionSync()
    {
        if (_subscribed is not null)
            _subscribed.SelectionSyncRequested -= SyncQueueSelection;
        _subscribed = null;
    }

    private void SyncQueueSelection()
    {
        if (DataContext is not DownloadsPageViewModel model || QueueList is null)
            return;

        // Snapshot first: clearing the list runs QueueSelectionChanged, which rewrites
        // the model's set from the (now empty) list.
        var desired = model.SelectedDownloads.ToArray();
        QueueList.SelectedItems!.Clear();
        foreach (var row in desired)
        {
            if (model.Downloads.Contains(row))
                QueueList.SelectedItems.Add(row);
        }
    }

    private void RowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source && source.GetSelfAndVisualAncestors().Any(x => x is Button))
            return;

        if (sender is Control { DataContext: DownloadItemViewModel row })
            row.PropertiesCommand.Execute(null);
    }

    /// <summary>Keeps the page's batch selection in sync with the list's selection.</summary>
    /// <remarks>
    /// Shift ranges and the Cmd/Ctrl toggle are handled by the list itself
    /// (<c>ItemSelectionEventTriggers</c> reads the platform's selection and command
    /// modifiers), so this only mirrors the result for the batch commands.
    /// </remarks>
    private void QueueSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DownloadsPageViewModel model || sender is not ListBox list ||
            list.SelectedItems is not { } items)
            return;

        model.SelectedDownloads.Clear();
        foreach (var item in items)
        {
            if (item is DownloadItemViewModel row)
                model.SelectedDownloads.Add(row);
        }
        model.UpdateBatchSelection();
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ResetDrag();
        if (DataContext is not DownloadsPageViewModel)
            return;
        if (!e.GetCurrentPoint(QueueList).Properties.IsLeftButtonPressed)
            return;

        var source = e.Source as Visual;
        if (IsInteractive(source))
            return;
        var row = FindRow(source);
        if (row is null)
            return;

        _dragTrigger = e;
        _dragSource = row;
        _dragOrigin = e.GetPosition(QueueList);
    }

    private async void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragTrigger is null || _dragSource is null)
            return;
        if (!e.GetCurrentPoint(QueueList).Properties.IsLeftButtonPressed)
        {
            ResetDrag();
            return;
        }

        Point position = e.GetPosition(QueueList);
        if (Math.Abs(position.X - _dragOrigin.X) < 6 && Math.Abs(position.Y - _dragOrigin.Y) < 6)
            return;

        var trigger = _dragTrigger!;
        _dragTrigger = null;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(QueueRowFormat, _dragToken));
        try
        {
            await DragDrop.DoDragDropAsync(trigger, transfer, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Queue row drag failed: " + Log.SafeException(ex));
        }
        finally
        {
            _dragSource = null;
        }
    }

    private void OnRowDragOver(object? sender, DragEventArgs e)
    {
        if (!IsQueueDrag(e))
            return; // an external payload: the window-level drop target handles it

        var target = FindRow(e.Source as Visual);
        bool accepts = target is not null && DataContext is DownloadsPageViewModel model &&
                       model.CanMoveBeside(_dragSource!, target);
        e.DragEffects = accepts ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnRowDrop(object? sender, DragEventArgs e)
    {
        if (!IsQueueDrag(e))
            return; // dropped links and containers fall through to the main window

        e.Handled = true;
        if (DataContext is not DownloadsPageViewModel model)
            return;

        var source = _dragSource;
        var target = FindRow(e.Source as Visual);
        if (source is null || target is null)
            return;

        var container = ContainerFor(target);
        bool after = container is not null && e.GetPosition(container).Y > container.Bounds.Height / 2;
        model.MoveBeside(source, target, after);
    }

    private bool IsQueueDrag(DragEventArgs e) =>
        _dragSource is not null &&
        string.Equals(e.DataTransfer.TryGetValue(QueueRowFormat), _dragToken, StringComparison.Ordinal);

    private void ResetDrag()
    {
        _dragTrigger = null;
        _dragSource = null;
    }

    /// <summary>Buttons and inputs inside a row keep their own gestures.</summary>
    private bool IsInteractive(Visual? source)
    {
        if (source is null)
            return false;
        foreach (var visual in source.GetSelfAndVisualAncestors())
        {
            if (visual is ListBoxItem)
                return false;
            if (visual is Button or TextBox or ComboBox or CheckBox)
                return true;
        }
        return false;
    }

    private DownloadItemViewModel? FindRow(Visual? source)
    {
        if (source is null)
            return null;
        foreach (var ancestor in source.GetSelfAndVisualAncestors())
        {
            if (ReferenceEquals(ancestor, QueueList))
                return null;
            if (ancestor is ListBoxItem { DataContext: DownloadItemViewModel row })
                return row;
        }
        return null;
    }

    private ListBoxItem? ContainerFor(DownloadItemViewModel row) =>
        QueueList.GetVisualDescendants().OfType<ListBoxItem>()
            .FirstOrDefault(item => ReferenceEquals(item.DataContext, row));
}
