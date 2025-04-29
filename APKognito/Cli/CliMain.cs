#if DEBUG
﻿#define DEBUG_WITHOUT_CONSOLE

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

        // This should always be active for a release build, even if disabled for debugging.
#if (DEBUG && !DEBUG_WITHOUT_CONSOLE) || RELEASE
        AttachConsole();
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