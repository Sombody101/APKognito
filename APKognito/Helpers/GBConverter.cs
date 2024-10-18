using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public class GBConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long totalUsedSpace)
        {
            return totalUsedSpace >= (1024 * 1024 * 1024)
                ? $"{totalUsedSpace / 1024f / 1024f / 1024f:0.00} GB"
                : $"{totalUsedSpace / 1024f / 1024f:n0} MB";
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}