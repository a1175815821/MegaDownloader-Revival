using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MegaDownloader.UI.Services;

namespace MegaDownloader.UI.ViewModels;

public sealed class NavigationItemViewModel(string title, Geometry icon, ViewModelBase page) : ObservableObject
{
    public string Title => UiText.Get(title);
    public Geometry Icon { get; } = icon;
    public ViewModelBase Page { get; } = page;

    public override string ToString() => Title;

    internal void ApplyLanguage() => OnPropertyChanged(nameof(Title));
}
