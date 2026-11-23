using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
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
    private readonly AddLinksCoordinator _coordinator;
    private readonly DownloadEngineService _engine;
    private readonly SingleInstanceCoordinator _instance;
    private readonly DispatcherTimer _pollTimer;
    private IActivatableLifetime? _activatable;

    /// <summary>
    /// Argument batches that were received but not queued yet. A batch stays here until
    /// the queue can take it, so a link handed to a starting application is not lost.
    /// </summary>
    private readonly List<IReadOnlyList<string>> _pending = new();

    private bool _disposed;
    private bool _draining;

    public ActivationService(
        IPlatformServices platform,
        AddLinksCoordinator coordinator,
        DownloadEngineService engine,
        SingleInstanceCoordinator instance)
    {
        _platform = platform;
        _coordinator = coordinator;
        _engine = engine;
        _instance = instance;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _pollTimer.Tick += async (_, _) => await DrainAsync();
    }

    /// <summary>Raised on the UI thread with the number of links an activation carried.</summary>
    public event Action<int>? LinksFound;

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

        // macOS delivers a mega:// link to the already-running .app as an activation
        // event instead of starting a second process; Linux desktops can do the same.
        // Both carry the same payloads the spool does, so they join the same queue.
        _activatable = Avalonia.Application.Current?.TryGetFeature<IActivatableLifetime>();
        if (_activatable is not null)
            _activatable.Activated += OnActivated;

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
            if (ApplicationDataDirectory.Override is not null || executable.Length == 0)
                return;
            _platform.SetMegaProtocolHandler(true, executable);
        }
        catch (Exception ex)
        {
            // A missing registration only means browser links open nothing; the app works.
            Log.WriteWarning("mega:// registration failed: " + Log.SafeException(ex));
        }
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (_disposed)
            return;

        IReadOnlyList<string>? batch = e switch
        {
            ProtocolActivatedEventArgs protocol => new[] { protocol.Uri.ToString() },
            FileActivatedEventArgs files => LocalPaths(files.Files),
            _ => null
        };
        if (batch is null || batch.Count == 0)
            return;

        _pending.Add(batch);
        ActivateWindow();
        _ = DrainAsync();
    }

    private static IReadOnlyList<string> LocalPaths(IReadOnlyList<IStorageItem> files)
    {
        var paths = new List<string>();
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
                paths.Add(path);
        }
        return paths;
    }

    private async Task DrainAsync()
    {
        if (_disposed || _draining)
            return;
        _draining = true;
        try { await DrainPendingAsync(); }
        finally { _draining = false; }
    }

    private async Task DrainPendingAsync()
    {
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
            if (_disposed || result is null)
            {
                // The queue could not take the links yet: keep the batch and try again.
                break;
            }

            _pending.RemoveAt(0);
            ActivateWindow();
        }
    }

    /// <summary>
    /// Hands the links of one activation to the confirmation window. Returns null while
    /// the queue is still loading, so the caller retries instead of losing them.
    /// </summary>
    private async Task<LinkIntakeResult?> HandleAsync(IReadOnlyList<string> arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return null;

        if (!_engine.IsLoaded)
            return null;

        var links = new List<string>();
        var containers = new List<string>();
        foreach (string argument in arguments)
        {
            // A browser activation normally carries mega:// links, but a file
            // association or a command line can hand over an .elc container instead.
            if (LinkContainer.IsContainerPath(argument) && System.IO.File.Exists(argument))
                containers.Add(argument);
            else
                links.AddRange(LinkIntakeService.ExtractLinks(argument));
        }

        if (containers.Count > 0)
        {
            string? unpacked = await _coordinator.ReadContainersAsync(containers);
            if (!string.IsNullOrEmpty(unpacked))
                links.AddRange(LinkIntakeService.ExtractLinks(unpacked));
        }
        if (links.Count == 0)
            return new LinkIntakeResult(LinkIntakeStatus.NoLinks, 0, 0, "No MEGA links found.");

        try
        {
            if (_coordinator.IsOpen)
                _coordinator.Stage(string.Join(Environment.NewLine, links));
            else
                _ = _coordinator.ShowAsync(string.Join(Environment.NewLine, links));

            LinksFound?.Invoke(links.Count);
            return new LinkIntakeResult(LinkIntakeStatus.Added, links.Count, 0, string.Empty);
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
        if (_activatable is not null)
        {
            _activatable.Activated -= OnActivated;
            _activatable = null;
        }
        _pending.Clear();
        _instance.Dispose();
    }
}
