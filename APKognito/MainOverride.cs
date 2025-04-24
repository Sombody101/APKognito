using APKognito.Cli;

namespace APKognito;

// Is it a bad idea to do this? Maybe.
// Does that have any influence on what I'm doing? Well, it is here, isn't it?
internal static class MainOverride
{
    public static bool RestartedFromUpdate { get; private set; }

    /// <summary>
    /// Application Entry Point.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        // Tells AutoUpdateService to cleanup update files
        if (Array.Exists(args, str => str == Constants.UpdateInstalledArgument))
        {
            RestartedFromUpdate = true;
        }
        else
        {
            ParsedArgs pargs = new(args);
            CliMain.Main(pargs);
        }

        App app = new();
        app.InitializeComponent();
        return app.Run();
    }
}