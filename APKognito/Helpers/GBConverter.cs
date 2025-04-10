using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public class GBConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is long totalUsedSpace 
            ? FormatSizeFromBytes(totalUsedSpace) 
            : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public static string FormatSizeFromBytes(long size)
    {
        return size switch
        {
            >= (1024 * 1024 * 1024) => $"{size / 1024f / 1024f / 1024f:0.00} GB",
            >= (1024 * 1024) => $"{size / 1024f / 1024f:n0} MB",
            >= 1024 => $"{size / 1024f:n0} KB",
            _ => $"{size} bytes"
        };
    }
}