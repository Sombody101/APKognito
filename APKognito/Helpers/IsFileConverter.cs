using APKognito.Models;
using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

internal class IsFileConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FootprintTypes fType)
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

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}