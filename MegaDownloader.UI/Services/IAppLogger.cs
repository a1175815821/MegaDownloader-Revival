namespace MegaDownloader.UI.Services;

/// <summary>
/// Minimal logging seam. The WinForms app wrote ad-hoc text files; the Avalonia
/// shell keeps one rolling daily file under the app data directory so diagnostics
/// survive a crash without pulling in a full logging stack.
/// </summary>
public interface IAppLogger
{
    void Info(string message);

    void Warn(string message, Exception? exception = null);

    void Error(string message, Exception? exception = null);
}
