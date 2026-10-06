using System.Globalization;
using System.Windows.Data;
using JSimulator.App.Services;

namespace JSimulator.App.Converters;

/// <summary>
/// Formats a double using the decimal-place preference from the Settings
/// page instead of a fixed <c>StringFormat</c>, so changing that setting
/// updates every bound metric live. ConverterParameter <c>"P"</c> formats
/// as a percentage with a fixed single decimal (percentages don't need the
/// same precision knob as raw counts/times); any other parameter formats
/// as a fixed-point number using <see cref="SettingsService.DecimalPlaces"/>.
/// </summary>
public class MetricFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double d) return value?.ToString() ?? string.Empty;

        if (string.Equals(parameter as string, "P", StringComparison.OrdinalIgnoreCase))
            return d.ToString("P1", culture);

        int places = Math.Clamp(SettingsService.Instance.DecimalPlaces, 0, 10);
        return d.ToString("N" + places, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
