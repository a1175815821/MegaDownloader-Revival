using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using MegaDownloader.Platform;

namespace MegaDownloader.UI.Services;

/// <summary>
/// Entry point for links that arrive from outside the running window: a <c>mega://</c>
/// activation from the browser, a file association, or the user launching the app again.
/// </summary>
/// <remarks>
/// A second launch hands its arguments to the running instance and exits, so there is one
/// queue and one window. The running instance polls the spool on the UI thread and feeds
/// the links through <see cref="LinkIntakeService"/>.
/// </remarks>
public sealed class ActivationService : IDisposable
{
    private readonly IPlatformServices _platform;
    private readonly LinkIntakeService _intake;
    private readonly DownloadEngineService _engine;
    private readonly SingleInstanceCoordinator _instance;
    private readonly DispatcherTimer _pollTimer;

    /// <summary>
    /// Argument batches that were received but not queued yet. A batch stays here until
    /// the queue can take it, so a link handed to a starting application is not lost.
    /// </summary>
    private readonly List<IReadOnlyList<string>> _pending = new();

    private bool _disposed;

    public ActivationService(
        IPlatformServices platform,
        LinkIntakeService intake,
        DownloadEngineService engine,
        SingleInstanceCoordinator instance)
    {
        _platform = platform;
        _intake = intake;
        _engine = engine;
        _instance = instance;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _pollTimer.Tick += async (_, _) => await DrainAsync();
    }

    /// <summary>Raised on the UI thread after an activation attempt, for the status bar.</summary>
    public event Action<LinkIntakeResult>? IntakeCompleted;

    /// <summary>
    /// Returns false when another instance already owns the queue; the caller must then
    /// forward its arguments and exit without showing a window.
    /// </summary>
    public bool TryBecomePrimary(IReadOnlyList<string> arguments)
    {
        if (_instance.TryAcquire())
            return true;

        _instance.SendArguments(arguments);
        return false;
    }

    /// <summary>
    /// Starts the poll and processes this launch's own arguments. The protocol handler is
    /// registered on every start: a portable build may have moved, and a stale command
    /// line would send links to a path that no longer exists.
    /// </summary>
    public void Start(IReadOnlyList<string> arguments)
    {
        if (_disposed)
            return;

        RegisterProtocolHandler();
        _pollTimer.Start();

        // The queue is loaded asynchronously, so this launch's own arguments are queued
        // like any other activation and processed once the queue is ready.
        if (arguments is not null && arguments.Count > 0)
            _pending.Add(arguments);
    }

    private void RegisterProtocolHandler()
    {
        try
        {
            string executable = Environment.ProcessPath ?? string.Empty;
            if (executable.Length == 0)
                return;
            _platform.SetMegaProtocolHandler(true, executable);
        }
        catch (Exception ex)
        {
            // A missing registration only means browser links open nothing; the app works.
            Log.WriteWarning("mega:// registration failed: " + Log.SafeException(ex));
        }
    }

    private async Task DrainAsync()
    {
        if (_disposed)
            return;

        // The spool is only read once the queue can take the links. Reading it earlier
        // would truncate the file and drop the links of a second launch that arrived
        // while the queue was still loading.
        if (_engine.IsLoaded)
        {
            IReadOnlyList<IReadOnlyList<string>> pending;
            try
            {
                pending = _instance.ReadPendingArguments();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Reading pending activations failed: " + Log.SafeException(ex));
                pending = Array.Empty<IReadOnlyList<string>>();
            }

            foreach (IReadOnlyList<string> arguments in pending)
                _pending.Add(arguments);
        }

        while (_pending.Count > 0)
        {
            IReadOnlyList<string> arguments = _pending[0];

            // A launch without arguments only asks the running window to come forward.
            if (arguments.Count == 0)
            {
                _pending.RemoveAt(0);
                ActivateWindow();
                continue;
            }

            LinkIntakeResult? result = await HandleAsync(arguments);
            if (result is null)
            {
                // The queue could not take the links yet: keep the batch and try again.
                break;
            }

            _pending.RemoveAt(0);
            ActivateWindow();
        }
    }

    /// <summary>
    /// Queues the links of one activation. Returns null when the queue could not take
    /// them yet, so the caller can retry instead of losing them.
    /// </summary>
    private async Task<LinkIntakeResult?> HandleAsync(IReadOnlyList<string> arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return null;

        try
        {
            LinkIntakeResult result = await _intake.AddFromArgumentsAsync(arguments);
            // "Not ready" means the queue is still loading: the batch stays pending.
            if (result.Status == LinkIntakeStatus.NotReady && !_engine.IsLoaded)
                return null;

            if (result.Status != LinkIntakeStatus.NoLinks)
                IntakeCompleted?.Invoke(result);
            return result;
        }
        catch (Exception ex)
        {
            Log.WriteError("Handling an activation failed: " + Log.SafeException(ex));
            return new LinkIntakeResult(LinkIntakeStatus.Failed, 0, 0,
                DownloadAdvice.FirstLine(Log.Redact(ex.Message)));
        }
    }

    private static void ActivateWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Activate();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _pollTimer.Stop();
        _pending.Clear();
        _instance.Dispose();
    }
}
