using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

internal class StringFormatConverter : IMultiValueConverter
{
    object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Format(parameter.ToString() ?? string.Empty, values, culture);
    }

    object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}