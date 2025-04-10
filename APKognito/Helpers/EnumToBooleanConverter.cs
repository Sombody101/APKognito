﻿using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace APKognito.Helpers;

internal class EnumToBooleanConverter : IValueConverter
{
    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string enumString)
        {
            throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
        }

        if (!Enum.IsDefined(typeof(ApplicationTheme), value))
        {
            throw new ArgumentException("ExceptionEnumToBooleanConverterValueMustBeAnEnum");
        }

        object enumValue = Enum.Parse(typeof(ApplicationTheme), enumString);

        return enumValue.Equals(value);
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return parameter is not string enumString
            ? throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName")
            : Enum.Parse(typeof(ApplicationTheme), enumString);
    }
}