﻿using APKognito.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace APKognito.Cli;

internal static class CliMain
{
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    public static void Main(ParsedArgs args)
    {
        if (!args.RunningCli)
        {
            return;
        }

        AttachConsole();

        if (args.GetCode is not null)
        {
            Console.WriteLine($"Code: {(ExitCode)args.GetCode}\nValue: {args.GetCode}/{(int)Enum.GetValues<ExitCode>().Cast<ExitCode>().Max()}");
        }

        // Check basic input argument first (mostly used for auto publish script)
        if (args.GetVersion)
        {
            Console.WriteLine(Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "[Null]");
        }

        if (args.StartApp)
        {
            // Start the GUI
            return;
        }

        Exit((int)ExitCode.NoError);
    }

    public static void AttachConsole()
    {
        if (!AttachConsole(-1))
        {
            // No console handle from the parent
            FileLogger.LogFatal($"CLI usage attempted. Failed to get parent console handle. Given args:\r\n{string.Join("\r\n\t", Environment.GetCommandLineArgs())}");
            Environment.Exit((int)ExitCode.ParentConsoleHandleNotFound);
        }
    }

    [DoesNotReturn]
    public static void Exit(ExitCode code)
    {
        Environment.Exit((int)code);
    }
}