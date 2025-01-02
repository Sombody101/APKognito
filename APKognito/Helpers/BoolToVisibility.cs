using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public class BoolToVisibility : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool visible)
        {
            return visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
