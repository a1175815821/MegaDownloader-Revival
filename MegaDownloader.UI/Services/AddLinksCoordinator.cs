using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MegaDownloader.UI.Services;

/// <summary>
/// The single entry point for links that arrive from outside the workbench: the toolbar,
/// the clipboard monitor and a <c>mega://</c> activation all open the same confirmation
/// window. Links that arrive while it is open are held and offered as the next batch,
/// so nothing is queued without the user seeing it first.
/// </summary>
public sealed class AddLinksCoordinator
{
    private readonly IDialogService _dialogs;
    private readonly List<string> _staged = new();
    private bool _open;

    public AddLinksCoordinator(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    /// <summary>Raised on the UI thread when links were held for the next batch.</summary>
    public event Action<int>? LinksStaged;

    public bool IsOpen => _open;

    public int StagedCount => _staged.Count;

    /// <summary>
    /// Opens the window with the given text (or empty). While it is already open the
    /// links are staged and null is returned so the caller can say they were held.
    /// </summary>
    public async Task<string?> ShowAsync(string? links)
    {
        if (_open)
        {
            Stage(links);
            return null;
        }

        _open = true;
        try
        {
            string? batch = links;
            string? lastResult = null;
            while (true)
            {
                string? result = await _dialogs.ShowAddLinksAsync(batch);
                if (result is not null)
                    lastResult = result;

                if (_staged.Count == 0)
                    return lastResult;

                batch = string.Join(Environment.NewLine, _staged);
                _staged.Clear();
                _dialogs.ShowPendingBatch(0);
            }
        }
        finally
        {
            _open = false;
            _staged.Clear();
        }
    }

    /// <summary>
    /// Unpacks dropped or command-line files into link text. Non-container files are
    /// reported: the drop target only accepts <c>.elc</c>/<c>.dlc</c> containers and
    /// plain link text.
    /// </summary>
    public async Task<string?> ReadContainersAsync(IReadOnlyList<string> paths)
    {
        var links = new List<string>();
        var errors = new List<string>();
        foreach (string path in paths)
        {
            if (!LinkContainer.IsContainerPath(path))
            {
                errors.Add(UiText.Format("{0}: only .elc and .dlc files can be dropped here.", Path.GetFileName(path)));
                continue;
            }

            try
            {
                links.Add(await LinkContainer.ReadAsync(path));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
            {
                errors.Add(Path.GetFileName(path) + ": " + DownloadAdvice.FirstLine(Log.Redact(ex.Message)));
            }
        }

        if (errors.Count > 0)
            await _dialogs.ShowMessageAsync(UiText.Get("Could not read the file"), string.Join(Environment.NewLine, errors), UiText.Get("OK"));

        return links.Count == 0 ? null : string.Join(Environment.NewLine, links);
    }

    /// <summary>
    /// Holds links for the next batch. Returns true when they were accepted; false when
    /// there was nothing link-shaped to hold.
    /// </summary>
    public bool Stage(string? links)
    {
        IReadOnlyList<string> found = LinkIntakeService.ExtractLinks(links);
        if (found.Count == 0)
            return false;

        _staged.AddRange(found);
        _dialogs.ShowPendingBatch(_staged.Count);
        LinksStaged?.Invoke(_staged.Count);
        return true;
    }
}
