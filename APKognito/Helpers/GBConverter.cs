using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public class GBConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int totalUsedSpace
            ? totalUsedSpace >= 1024
                ? $"{totalUsedSpace / 1024f:0.00} GB"
                : (object)$"{totalUsedSpace:n0} MB"
            : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
