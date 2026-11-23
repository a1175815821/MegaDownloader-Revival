using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MegaDownloader.UI.ViewModels;

/// <summary>Turns a left inset in DIPs into a row margin.</summary>
public sealed class IndentToThicknessConverter : IValueConverter
{
    public static readonly IndentToThicknessConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double indent = value is double d ? d : 0d;
        return new Thickness(indent, 0, 0, 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
