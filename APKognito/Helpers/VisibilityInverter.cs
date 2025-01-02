using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public class VisibilityInverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility switch
            {
                Visibility.Visible => Visibility.Collapsed,
                Visibility.Collapsed => Visibility.Visible,
                _ => Visibility.Visible
            };
        }
        else if (value is bool visible)
        {
            return !visible
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
