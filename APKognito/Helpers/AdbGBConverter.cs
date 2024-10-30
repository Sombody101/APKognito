using APKognito.Models;
using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public class AdbGBConverter : IValueConverter
{
    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is KeyValuePair<long, AdbFolderType> pair)
        {
            if (pair.Value is not AdbFolderType.File)
            {
                return string.Empty;
            }

            long size = pair.Key;
            return size switch
            {
                >= (1024 * 1024 * 1024) => $"{size / 1024f / 1024f / 1024f:0.00} GB",
                >= (1024 * 1024) => $"{size / 1024f / 1024f:n0} MB",
                >= 1024 => $"{size / 1024f:n0} KB",
                _ => $"{size} bytes"
            };
        }

        return value;
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}