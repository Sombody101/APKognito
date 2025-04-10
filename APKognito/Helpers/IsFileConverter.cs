using APKognito.Models;
using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

internal class IsFileConverter : IValueConverter
{
    object? IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is FootprintTypes fType
            ? ParseType(fType)
            : (object?)null;
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string ParseType(FootprintTypes fType)
    {
        return fType switch
        {
            FootprintTypes.RenamedApk => "Renamed APK",
            FootprintTypes.Directory => "Directory",
            FootprintTypes.TempDirectory => "Temporary Directory",
            FootprintTypes.File => "File",
            FootprintTypes.TempFile => "Temporary File",
            _ => "[Unknown]"
        };
    }
}