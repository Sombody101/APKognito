using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

[ValueConversion(typeof(bool), typeof(bool))]
public class BoolInterterConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b 
            ? !b 
            : (object?)null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}