using System.Globalization;
using System.Windows.Data;

namespace NativeTavern.Converters;

public sealed class InitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value as string;
        if (string.IsNullOrWhiteSpace(name)) return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1
            ? string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])))
            : char.ToUpperInvariant(name.Trim()[0]).ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
