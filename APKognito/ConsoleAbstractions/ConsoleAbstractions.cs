#if DEBUG
using Spectre.Console;
#endif

namespace APKognito.ConsoleAbstractions;

public static class ConsoleAbstraction
{
    public static void WriteException(Exception ex)
    {
#if DEBUG
        AnsiConsole.WriteException(ex);
#else
        Console.WriteLine(ex);
#endif
    }

    /*
     * WriteLine
     */

    public static void WriteLine()
    {
        Console.WriteLine();
    }

    public static void WriteLine(string value)
    {
#if DEBUG
        AnsiConsole.MarkupLine(value);
#else
        Console.WriteLine(value);
#endif
    }

    public static string RemoveMarkup(this string value)
    {
#if DEBUG
        value = StringExtensions.RemoveMarkup(value);
#endif

        return value;
    }
}
