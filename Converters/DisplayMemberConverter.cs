using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace NativeTavern.Converters;

public sealed class DisplayMemberConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var item = values.Length > 0 ? values[0] : null;
        var memberPath = values.Length > 1 ? values[1] as string : null;
        var fallback = values.Length > 2 ? values[2]?.ToString() : null;
        if (item is null) return fallback ?? string.Empty;
        if (string.IsNullOrWhiteSpace(memberPath)) return item.ToString() ?? string.Empty;

        object? current = item;
        foreach (var member in memberPath.Split('.'))
        {
            if (current is null) return string.Empty;
            var property = current.GetType().GetProperty(member, BindingFlags.Instance | BindingFlags.Public);
            if (property is null) return current.ToString() ?? string.Empty;
            current = property.GetValue(current);
        }
        return current?.ToString() ?? string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ReferenceEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 2 && ReferenceEquals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
