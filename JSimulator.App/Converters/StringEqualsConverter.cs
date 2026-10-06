using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JSimulator.App.Converters;

/// <summary>
/// Two-way converter used to bind a group of RadioButtons to a single string
/// property (e.g. a "current tab" name). IsChecked is true when the bound
/// property equals the RadioButton's ConverterParameter; checking a button
/// writes that parameter back to the source property.
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is not null)
            return parameter;
        return Binding.DoNothing;
    }
}
