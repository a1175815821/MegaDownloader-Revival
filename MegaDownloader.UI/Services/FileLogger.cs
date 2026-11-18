using System.Diagnostics;
using MegaDownloader.Platform;

namespace MegaDownloader.UI.Services;

public sealed class FileLogger : IAppLogger
{
    private readonly object _gate = new();
    private readonly string _directory;

    public FileLogger(IPlatformServices platform)
    {
        _directory = Path.Combine(platform.GetAppDataDirectory("MegaDownloader"), "logs");
        Directory.CreateDirectory(_directory);
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        Debug.WriteLine(line);

        try
        {
            lock (_gate)
            {
                File.AppendAllText(
                    Path.Combine(_directory, $"app-{DateTime.Now:yyyyMMdd}.log"),
                    line + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // Logging must never take the app down.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
