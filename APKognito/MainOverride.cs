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
        // Check basic input argument first (mostly used for auto publish script)
        if (args.Any(arg => arg is "--version" or "-v"))
        {
            if (!AttachConsole(-1))
            {
                // No console handle from the parent
                return 5012;
            }

            Console.WriteLine(Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "[Null]");
            return 0;
        }

        APKognito.App app = new APKognito.App();
        app.InitializeComponent();
        app.Run();

        return 0;
    }
}
