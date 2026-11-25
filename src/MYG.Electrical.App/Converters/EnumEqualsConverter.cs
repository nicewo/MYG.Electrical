using System.Globalization;
using System.Windows.Data;

namespace MYG.Electrical.App.Converters;

/// <summary>Enum değeri parametreye eşitse true. Araç çubuğundaki seçili araç göstergesi için.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? Enum.Parse(targetType, parameter.ToString()!) : Binding.DoNothing;
}
