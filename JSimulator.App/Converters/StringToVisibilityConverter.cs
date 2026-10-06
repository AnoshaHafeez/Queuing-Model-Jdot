using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JSimulator.App.Converters;

/// <summary>
/// Collapses a control when the bound string is null or empty; visible otherwise.
/// Used for status/message TextBlocks that should only take up space once they
/// have something to say (e.g. <c>ReportStatus</c> after a PDF export).
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
