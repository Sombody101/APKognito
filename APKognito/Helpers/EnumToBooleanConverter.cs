using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace APKognito.Helpers;

internal class EnumToBooleanConverter : IValueConverter
{
    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not String enumString)
        {
            throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
        }

        if (!Enum.IsDefined(typeof(ApplicationTheme), value))
        {
            throw new ArgumentException("ExceptionEnumToBooleanConverterValueMustBeAnEnum");
        }

        var enumValue = Enum.Parse(typeof(ApplicationTheme), enumString);

        return enumValue.Equals(value);
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not String enumString)
        {
            throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
        }

        return Enum.Parse(typeof(ApplicationTheme), enumString);
    }
}