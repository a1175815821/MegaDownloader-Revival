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
    private readonly LinkIntakeService _intake;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly CancellationTokenSource _shutdown = new();
    private uint? _handledSequence;
    private string? _handledText;
    private bool _busy;
    private bool _disposed;

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    public ClipboardMonitor(DownloadEngineService engine, LinkIntakeService intake)
    {
        _engine = engine;
        _intake = intake;
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public event Action<LinkIntakeResult>? IntakeCompleted;

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
                var editor = desktop.Windows.OfType<AddLinksWindow>().FirstOrDefault(w => w.IsVisible);
                if (editor?.DataContext is AddLinksViewModel model)
                {
                    if (model.LinksText.Length == 0 && !model.IsInspecting && !model.IsImporting)
                        model.LinksText = string.Join(Environment.NewLine, links);
                }
                else
                {
                    var result = await _intake.AddAsync(text, cancellationToken: _shutdown.Token);
                    if (_disposed || result.Status == LinkIntakeStatus.NotReady)
                        return;
                    IntakeCompleted?.Invoke(result);
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
