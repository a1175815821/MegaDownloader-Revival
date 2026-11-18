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

    /// <summary>Opens the link-intake window; returns true when a package was added.</summary>
    Task<bool> ShowAddLinksAsync();

    /// <summary>Opens the properties window for a <see cref="Fichero"/> or <see cref="Paquete"/>.</summary>
    Task<bool> ShowPropertiesAsync(object item);

    /// <summary>Opens the offline link encoder.</summary>
    Task ShowEncodeLinksAsync();

    /// <summary>Opens the ELC generator (account picker, server encode, result).</summary>
    Task ShowElcGenerateAsync();
}
