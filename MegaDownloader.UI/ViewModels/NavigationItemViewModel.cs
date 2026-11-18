namespace MegaDownloader.UI.ViewModels;

/// <summary>One entry in the left navigation rail.</summary>
public sealed class NavigationItemViewModel
{
    public NavigationItemViewModel(string key, string title, ViewModelBase page)
    {
        Key = key;
        Title = title;
        Page = page;
    }

    public string Key { get; }

    public string Title { get; }

    public ViewModelBase Page { get; }
}
