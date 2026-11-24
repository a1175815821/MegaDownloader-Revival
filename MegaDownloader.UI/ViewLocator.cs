using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MegaDownloader.UI.ViewModels;

namespace MegaDownloader.UI;

/// <summary>
/// Maps view models to views by naming convention:
/// <c>MegaDownloader.UI.ViewModels.FooViewModel</c> becomes
/// <c>MegaDownloader.UI.Views.FooView</c>.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        // Page view models live under ViewModels.Pages while their views stay in Views.
        var name = data.GetType().FullName!
            .Replace(".ViewModels.Pages.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is null)
            return new TextBlock { Text = $"View not found: {name}" };

        return (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
