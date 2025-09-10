//#define MOCK_RELEASE

#if DEBUG &&! MOCK_RELEASE
using Spectre.Console;
#endif

namespace APKognito.ConsoleAbstractions;

public static class ConsoleAbstraction
{
    public static void WriteException(Exception ex)
    {
#if DEBUG &&! MOCK_RELEASE
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
#if DEBUG &&! MOCK_RELEASE
        AnsiConsole.MarkupLine(value);
#else
        Console.WriteLine(value);
#endif
    }

    public static string RemoveMarkup(this string value)
    {
#if DEBUG &&! MOCK_RELEASE
        value = StringExtensions.RemoveMarkup(value);
#endif

        return value;
    }
}
