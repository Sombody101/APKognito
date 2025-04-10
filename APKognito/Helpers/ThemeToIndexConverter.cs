using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace APKognito.Helpers;

internal sealed class ThemeToIndexConverter : IValueConverter
{
    object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ApplicationTheme.Dark)
        {
            return 1;
        }

        return value is ApplicationTheme.HighContrast 
            ? 2 
            : 0;
    }

    object IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is 1)
        {
            return ApplicationTheme.Dark;
        }

        return value is 2 
            ? ApplicationTheme.HighContrast 
            : ApplicationTheme.Light;
    }
}