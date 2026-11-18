using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace MegaDownloader.UI.Services;

/// <summary>
/// Watches the clipboard for MEGA links and feeds them to <see cref="LinkIntakeService"/>.
/// </summary>
/// <remarks>
/// Avalonia has no clipboard-changed event, so this polls at a low frequency while the
/// option is enabled. Browsers use delayed rendering, so a read can legitimately return
/// empty right after a copy; the next tick picks the text up.
/// </remarks>
public sealed class ClipboardMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(900);

    private readonly DownloadEngineService _engine;
    private readonly LinkIntakeService _intake;
    private readonly DispatcherTimer _timer;
    private string? _lastHandled;
    private int _busy;
    private bool _disposed;

    public ClipboardMonitor(DownloadEngineService engine, LinkIntakeService intake)
    {
        _engine = engine;
        _intake = intake;
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    /// <summary>Raised on the UI thread after an intake attempt, for the shell status bar.</summary>
    public event Action<LinkIntakeResult>? IntakeCompleted;

    public void Start()
    {
        if (_disposed)
            return;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private async Task PollAsync()
    {
        if (_disposed || _busy != 0)
            return;
        if (!_engine.IsLoaded || !_engine.Config.AnalizarPortapapeles)
            return;

        string? text;
        try
        {
            var clipboard = MainWindowOrNull()?.Clipboard;
            if (clipboard is null)
                return;
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception)
        {
            // Another process can hold the clipboard; the next tick retries.
            return;
        }

        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, _lastHandled, StringComparison.Ordinal))
            return;

        // Only react to text that actually contains something link-shaped.
        var links = LinkIntakeService.ExtractLinks(text);
        if (links.Count == 0)
            return;

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return;

        _lastHandled = text;
        try
        {
            LinkIntakeResult result = await _intake.AddAsync(text);
            IntakeCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Clipboard intake failed: " + Log.SafeException(ex));
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    private static Avalonia.Controls.Window? MainWindowOrNull() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Stop();
    }
}
