using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace NativeTavern.Converters;
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = !string.IsNullOrWhiteSpace(value as string);
        if (string.Equals(parameter as string, "Inverse", StringComparison.OrdinalIgnoreCase))
            hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
