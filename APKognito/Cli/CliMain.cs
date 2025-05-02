#if DEBUG
//#define DEBUG_WITHOUT_CONSOLE
#endif

using APKognito.Utilities;
using System.Reflection;
using System.Runtime.InteropServices;

namespace APKognito.Cli;

internal static partial class CliMain
{
    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int dwProcessId);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    public static bool ConsoleActive { get; private set; }

    public static void Main(ParsedArgs args)
    {
        if (!args.RunningCli)
        {
            return;
        }

        if (args.StartConsole)
        {
            CreateConsole();
        }

        // This should always be active for a release build, even if disabled for debugging.
#if !DEBUG_WITHOUT_CONSOLE || RELEASE
        AttachConsole(); // Won't do anything if the console was attached/created already
#endif

        if (args.GetCode is not null)
        {
            Console.WriteLine($"Code: {(ExitCode)args.GetCode}\nValue: {args.GetCode}/{(int)Enum.GetValues<ExitCode>().Max()}");
        }

        // Check basic input argument first (mostly used for auto publish script)
        if (args.GetVersion)
        {
            string foundVersion = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "[Null]";
            Console.WriteLine(foundVersion);
        }

        if (args.StartApp)
        {
            // Start the GUI
            return;
        }

        Exit(ExitCode.NoError);
    }

    public static void AttachConsole()
    {
        if (ConsoleActive)
        {
            return;
        }

        if (AttachConsole(-1))
        {
            ConsoleActive = true;
            return;
        }

        // No console handle from the parent
        FileLogger.LogFatal($"CLI usage attempted. Failed to get parent console handle. Given args:\r\n{string.Join("\r\n\t", Environment.GetCommandLineArgs())}");
        Exit(ExitCode.ParentConsoleHandleNotFound);
    }

    public static void CreateConsole()
    {
        if (ConsoleActive)
        {
            return;
        }

        if (AttachConsole(-1) || AllocConsole())
        {
            ConsoleActive = true;

            Console.WriteLine("Console Active");

            return;
        }

        FileLogger.LogFatal("Failed to create console via kernel32::AllocConsole.");
        Exit(ExitCode.ConsoleCreationFailed);
    }

    [DoesNotReturn]
    public static void Exit(ExitCode code)
    {
        Environment.Exit((int)code);
    }
}