using System.Reflection;
using System.Runtime.InteropServices;

namespace APKognito;

// Is it a bad idea to do this? Maybe.
// Does that have any influence on what I'm doing? Well, it is here, isn't it?
internal static class MainOverride
{
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    /// <summary>
    /// Application Entry Point.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            if (!AttachConsole(-1))
            {
                // No console handle from the parent
                return (int)ExitCode.ParentConsoleHandleNotFound;
            }

            // Check basic input argument first (mostly used for auto publish script)
            if (args.Any(arg => arg is "--version" or "-v"))
            {
                Console.WriteLine(Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "[Null]");
                return (int)ExitCode.NoError;
            }

            if (args[0] == "--getcode" && args.Length > 1)
            {
                if (!int.TryParse(args[1], out int i32))
                {
                    return (int)ExitCode.InvalidInputArgument;
                }

                Console.WriteLine($"{(ExitCode)i32}");
            }

            return (int)ExitCode.NoError;
        }

        APKognito.App app = new();
        app.InitializeComponent();
        return app.Run();
    }
}
