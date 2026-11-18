namespace MegaDownloader.UI.Services;

/// <summary>
/// Message/confirm dialogs built in code for now. Phase 3 replaces this with the
/// ported dialog views (and a proper shell modal host) once the settings and
/// account flows land.
/// </summary>
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message, string okText = "OK");

    Task<bool> ConfirmAsync(string title, string message, string okText = "OK", string cancelText = "Cancel");
}
