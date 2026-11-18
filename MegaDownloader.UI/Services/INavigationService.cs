using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI.Services;

/// <summary>
/// Page-level navigation for the shell. Phase 0 keeps a single active page in a
/// content host; later phases add per-page back stacks where they earn their keep.
/// </summary>
public interface INavigationService
{
    ViewModelBase? CurrentPage { get; }

    event EventHandler? CurrentPageChanged;

    void NavigateTo(ViewModelBase page);

    void NavigateTo<T>() where T : ViewModelBase;
}
