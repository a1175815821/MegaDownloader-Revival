using MegaDownloader.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MegaDownloader.UI.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private ViewModelBase? _currentPage;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public ViewModelBase? CurrentPage => _currentPage;

    public event EventHandler? CurrentPageChanged;

    public void NavigateTo(ViewModelBase page)
    {
        if (ReferenceEquals(_currentPage, page))
            return;

        _currentPage = page;
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NavigateTo<T>() where T : ViewModelBase =>
        NavigateTo(_services.GetRequiredService<T>());
}
