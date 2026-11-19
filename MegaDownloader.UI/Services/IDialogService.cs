namespace MegaDownloader.UI.Services;

public interface IDialogService
{
    Task<bool> ShowGuideAsync();

    Task ShowMessageAsync(string title, string message, string okText = "OK");

    Task<bool> ConfirmAsync(string title, string message, string okText = "OK", string cancelText = "Cancel");

    /// <summary>Opens link intake and returns its feedback when a package was added.</summary>
    Task<string?> ShowAddLinksAsync(string? initialText = null);

    /// <summary>Opens the properties window for a <see cref="Fichero"/> or <see cref="Paquete"/>.</summary>
    Task<bool> ShowPropertiesAsync(object item);

    /// <summary>Opens the offline link encoder.</summary>
    Task ShowEncodeLinksAsync();

    /// <summary>Opens the ELC generator (account picker, server encode, result).</summary>
    Task ShowElcGenerateAsync();
}
