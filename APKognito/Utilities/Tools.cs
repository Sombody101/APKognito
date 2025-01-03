﻿using Humanizer;

namespace APKognito.Utilities;

internal static class Tools
{
    public static string? Truncate(this string? str, int maxLength)
    {
        if (str is not null)
        {
            int snipLength = str.Length <= maxLength
                ? str.Length
                : maxLength;

            return str[0..snipLength];
        }

        return null;
    }

    public static string Redact(this string? data)
    {
        return data?.Replace(Environment.UserName, FileLogger.ReplacementUsername) ?? string.Empty;
    }

    public static void InvertVisibility(this UIElement elm)
    {
        if (elm.Visibility is Visibility.Visible)
        {
            elm.Visibility = Visibility.Collapsed;
            return;
        }

        elm.Visibility = Visibility.Visible;
    }

    public static string PluralizeIf(this string word, bool condition, bool knownToBeSingular = false)
    {
        return condition 
            ? word.Pluralize(knownToBeSingular) 
            : word;
    }

    public static string PluralizeIfMany(this string word, int count, bool knownToBeSingular = false)
    {
        return PluralizeIf(word, count is not 1, knownToBeSingular);
    }
}
