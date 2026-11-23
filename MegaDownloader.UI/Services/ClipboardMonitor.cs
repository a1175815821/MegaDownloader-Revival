using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using MegaDownloader.UI.ViewModels;
using MegaDownloader.UI.Views;


namespace MegaDownloader.UI.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private readonly DownloadEngineService _engine;
    private readonly AddLinksCoordinator _coordinator;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly CancellationTokenSource _shutdown = new();
    private uint? _handledSequence;
    private string? _handledText;
    private bool _busy;
    private bool _disposed;

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    public ClipboardMonitor(DownloadEngineService engine, AddLinksCoordinator coordinator)
    {
        _engine = engine;
        _coordinator = coordinator;
        _timer.Tick += async (_, _) => await PollAsync();
    }

    /// <summary>Raised with the number of links found; the confirmation window does the rest.</summary>
    public event Action<int>? LinksFound;

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private async Task PollAsync()
    {
        if (_disposed || _busy || !_engine.IsLoaded || !_engine.Config.AnalizarPortapapeles)
            return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } clipboard)
            return;

        uint? sequence = OperatingSystem.IsWindows() ? GetClipboardSequenceNumber() : null;
        if (sequence.HasValue && sequence == _handledSequence)
            return;

        _busy = true;
        try
        {
            string? text = await clipboard.TryGetTextAsync();
            if (_disposed || text is null || (sequence is null && text == _handledText))
                return;
            var links = LinkIntakeService.ExtractLinks(text);
            if (links.Count > 0)
            {
                string joined = string.Join(Environment.NewLine, links);
                if (_coordinator.IsOpen)
                {
                    // The window is open: hold these for the next batch instead of
                    // queueing them behind the user's back.
                    _coordinator.Stage(joined);
                }
                else
                {
                    _ = _coordinator.ShowAsync(joined);
                    LinksFound?.Invoke(links.Count);
                }
            }
            _handledSequence = sequence;
            _handledText = sequence.HasValue ? null : text;
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception ex)
        {
            Log.WriteWarning("Clipboard read failed: " + Log.SafeException(ex));
        }
        finally { _busy = false; }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
