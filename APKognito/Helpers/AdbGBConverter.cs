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
            return GBConverter.FormatSizeFromBytes(size);
        }

        return value;
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}