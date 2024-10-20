using APKognito.Models;
using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

internal class IsFileConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FootprintType fType)
        {
            return fType switch
            {
                FootprintType.RenamedApk => "Renamed APK",
                FootprintType.Directory => "Directory",
                FootprintType.TempDirectory => "Temporary Directory",
                FootprintType.File => "File",
                FootprintType.TempFile => "Temporary File",
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