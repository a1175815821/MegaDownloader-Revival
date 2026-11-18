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
        _ = HandleAsync(arguments);
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

        IReadOnlyList<IReadOnlyList<string>> pending;
        try
        {
            pending = _instance.ReadPendingArguments();
        }
        catch (Exception ex)
        {
            Log.WriteWarning("Reading pending activations failed: " + Log.SafeException(ex));
            return;
        }

        foreach (IReadOnlyList<string> arguments in pending)
        {
            await HandleAsync(arguments);
            ActivateWindow();
        }
    }

    private async Task HandleAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
            return;

        try
        {
            LinkIntakeResult result = await _intake.AddFromArgumentsAsync(arguments);
            if (result.Status != LinkIntakeStatus.NoLinks)
                IntakeCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            Log.WriteError("Handling an activation failed: " + Log.SafeException(ex));
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
        _instance.Dispose();
    }
}
