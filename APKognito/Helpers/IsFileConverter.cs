using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

internal class IsFileConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isFile)
        {
            return isFile
                ? "File"
                : "Directory";
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
