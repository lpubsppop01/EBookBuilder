using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Lpubsppop01.EBookBuilder.App.Converters;

/// <summary>
/// Binds a radio button's <c>IsChecked</c> to an enum value.
/// </summary>
/// <remarks>
/// <para>
/// The usage is the same as in the original WPF version. Bind the same enum property to
/// several radio buttons and give each one its own value with <c>ConverterParameter</c>.
/// </para>
/// <code>
/// &lt;RadioButton IsChecked="{Binding ImageFormatKind,
///     Converter={StaticResource EnumToBoolConverter},
///     ConverterParameter=JPEG}" /&gt;
/// </code>
/// <para>
/// Unchecking (when false is returned) does nothing. Returning <c>Binding.DoNothing</c>
/// keeps the value from being overwritten when another radio button is selected.
/// </para>
/// </remarks>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Write back only the value of the button that became true. The false side is ignored.
        if (value is not true || parameter is null || targetType is null) return BindingOperations.DoNothing;

        return Enum.TryParse(targetType, parameter.ToString(), out var result)
            ? result
            : BindingOperations.DoNothing;
    }
}
