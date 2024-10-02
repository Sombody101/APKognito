using APKognito.Models;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace APKognito.Helpers;

internal class RenameSessionConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string[] apks)
        {
            StringBuilder output = new();

            foreach (string[] apkPair in apks.Select(RenameSession.FormatForView))
            {
                _ = output.Append(apkPair[0]).Append("  ")
                    .Append(apkPair[1])
                    .Append("  →  ")
                    .AppendLine(apkPair[2]);
            }

            return output.ToString();
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
