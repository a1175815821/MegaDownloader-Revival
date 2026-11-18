namespace MegaDownloader;

/// <summary>
/// Minimal notification seam between the platform-neutral engine and whichever
/// UI shell is hosting it. The engine must never reference a UI toolkit directly
/// (the old WinForms MessageBox calls made the download/extraction code
/// Windows-only). A host installs <see cref="Sink"/> at startup; without a sink
/// the notification is written to the log so headless runs stay silent.
/// </summary>
public static class UserNotification
{
    /// <summary>Receives (kind, title, message). Called from background threads.</summary>
    public static Action<UserNotificationKind, string, string> Sink;

    public static void ShowInfo(string message, string title)
    {
        Show(UserNotificationKind.Information, title, message);
    }

    public static void ShowError(string message, string title)
    {
        Show(UserNotificationKind.Error, title, message);
    }

    public static void Show(UserNotificationKind kind, string title, string message)
    {
        var sink = Sink;
        if (sink is not null)
        {
            try
            {
                sink(kind, title, message);
                return;
            }
            catch (Exception ex)
            {
                Log.WriteWarning("User notification sink failed: " + Log.SafeException(ex));
            }
        }

        if (kind == UserNotificationKind.Error)
            Log.WriteError(title + ": " + message);
        else
            Log.WriteInfo(title + ": " + message);
    }
}

public enum UserNotificationKind
{
    Information,
    Warning,
    Error
}
