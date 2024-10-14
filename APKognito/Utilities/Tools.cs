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
        return data?.Replace(Environment.UserName, FileLogger.ReplacmentUsername) ?? string.Empty;
    }
}
