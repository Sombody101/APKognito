using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public class EnumValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value.GetType().IsEnum)
        {
            return value.ToString()!;
        }

        return "[Enum: FailedConvert, not enum]";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string enumName && Enum.TryParse(targetType, enumName, out var result))
        {
            return result;
        }

        return null!;
    }
}
