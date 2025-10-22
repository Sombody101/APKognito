using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Helpers;
using APKognito.Utilities;
using APKognito.Utilities.JavaTools;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Windows;

namespace APKognito.ViewModels.Pages;

public partial class AdbConsoleViewModel
{
    internal static IReadOnlyCollection<CommandInfo> GetCommands()
    {
        if (s_commands.Count is 0)
        {
            RegisterCommands();
        }

        return s_commands.ToList();
    }

    private static readonly List<CommandInfo> s_commands = [];

    private static int s_longestCommandName = 0;

    [Command("help", "Prints this help information.", NO_USAGE)]
    private void GetHelpInfoCommand(ParsedCommand __)
    {
        StringBuilder output = new();

        foreach (CommandInfo? command in s_commands.Where(c => c.IsVisible))
        {
            _ = output.Append(':')
                .Append(command.CommandName.PadRight(s_longestCommandName))
                .Append(' ');

            _ = command.CommandUsage.Length is not 0
                ? output.AppendLine(command.CommandUsage)
                    .Append('\t')
                : output.AppendLine("(no parameters)\t");

            _ = output.AppendLine(command.HelpInfo).AppendLine();
        }

        WriteGenericLogLine(output.ToString());
    }

    [Command("clear", "Clears the log buffer.", NO_USAGE)]
    private void ClearLogsCommand(ParsedCommand _)
    {
        ClearLogs();
    }

    [Command("active", "Gets information about the currently running ADB process.", NO_USAGE)]
    private void ActiveStatusCommand(ParsedCommand _)
    {
        if (!_adbManager.IsRunning)
        {
            Log("There is no active ADB process.");
            return;
        }

        Process process = _adbManager.AdbProcess!;

        Log("Active ADB process:");
        Log($"Start time:\t\t{process.StartTime}");
    }

    [Command("set",
        "Sets and saves a custom cmdlet. Use '--list' to see all currently set cmdlets.",
        "[--list] || <command name> <command body>...")]
    private void SetCommandletCommand(ParsedCommand ctx)
    {
        if (ctx.ArgCount is 0 || ctx[0] == "--list")
        {
            if (_adbConfig.UserCmdlets.Count is 0)
            {
                Log("No cmdlets set.");
                return;
            }

            foreach (KeyValuePair<string, string> command in _adbConfig.UserCmdlets)
            {
                Log($"{command.Key}: {command.Value}");
            }

            return;
        }

        if (ctx.ArgCount is 0)
        {
            LogError("No cmdlet name or body supplied.");
            return;
        }

        if (ctx.ArgCount is 1)
        {
            LogError("No cmdlet body supplied.");
            return;
        }

        string cmdlet = ctx[0]!;
        _adbConfig.UserCmdlets[cmdlet] = string.Join(' ', ctx[1..]);
        Log($"Cmdlet '::{cmdlet}' created.");
        _configFactory.SaveConfig(_adbConfig);
    }

    [Command("unset", "Removes a custom cmdlet.", "<cmdlet name>")]
    private void RemoveCommandletCommand(ParsedCommand ctx)
    {
        if (ctx.ArgCount > 1)
        {
            LogError("Too many arguments.");
            return;
        }

        if (ctx.ArgCount is not 1)
        {
            LogError("No cmdlet name supplied.");
            return;
        }

        string cmdlet = ctx[0] ?? "[NO CMDLET]";
        if (_adbConfig.UserCmdlets.Remove(cmdlet))
        {
            Log($"Removed cmdlet '{cmdlet}'");
            _configFactory.SaveConfig(_adbConfig);
        }
        else
        {
            LogError($"Commandlet '{cmdlet}' not defined, no cmdlet removed.");
        }
    }

    [Command("echo", "Prints all arguments to the console.", "[text ...]")]
    private void EchoCommand(ParsedCommand ctx)
    {
        Log(string.Join(' ', ctx.Args));
    }

    [Command("vars", "Prints all set variables.", NO_USAGE)]
    private void PrintAllVariablesCommand(ParsedCommand __)
    {
        if (_adbHistory.Variables.Count is 0)
        {
            Log("There are no set variables.");
            return;
        }

        StringBuilder builder = new();
        foreach (KeyValuePair<string, string> pair in _adbHistory.Variables)
        {
            _ = builder.Append(pair.Key).Append("=\'").Append(pair.Value).AppendLine("'");
        }

        Log(builder.ToString());
    }

    [Command("get-crash", "Gets the crash information for applications", NO_USAGE)]
    private async Task GetDeviceCrashInfoAsync(ParsedCommand command)
    {
        await Task.Delay(5000);
        Log("Worked");
    }

    [Command(VARIABLE_SETTER)]
    private void SetVariableCommand(ParsedCommand ctx)
    {
        // Args 0: Variable name
        // Args 1: Variable value
        _adbHistory.SetVariable(ctx.Args[0], ctx.Args[1]);
    }

    [Command("sys")]
    private void SysInternalCommand(ParsedCommand ctx)
    {
        if (ctx.ArgCount is not 1)
        {
            LogError("Invalid argument count.");
            return;
        }

        string option = ctx.Args[0];
        switch (option)
        {
            case "get_heap":
                Log($"GC: {GBConverter.FormatSizeFromBytes(GC.GetTotalMemory(false))}");
                Log($"Private: {GBConverter.FormatSizeFromBytes(Process.GetCurrentProcess().PrivateMemorySize64)}");
                return;

            default:
                LogError($"Unknown sys option '{option}'");
                return;
        }
    }

    /*
     * Installer commands
     */

    [Command("install-adb", "Auto installs platform tools.", "[--force|-f]")]
    private static async Task InstallAdbCommandAsync(ParsedCommand ctx, IViewLogger logger, CancellationToken token)
    {
        bool adbFunctional = AdbManager.AdbWorks();
        if (!ctx.ContainsArgs("--force", "-f")
            && adbFunctional)
        {
            logger.LogError("ADB is already installed. Run with '--force' to force a reinstall.");
            return;
        }

        string appDataPath = App.AppDataDirectory.FullName;
        logger.Log($"Installing platform tools to: {appDataPath}\\platform-tools");

        string zipFile = $"{appDataPath}\\adb.zip";

        using (IDisposable? scope = logger.BeginScope("CLIENT"))
        {
            _ = await WebGet.DownloadAsync(Constants.ADB_INSTALL_URL, zipFile, logger, token);
        }

        logger.Log("Extracting platform tools.");

        // Only to keep track of whatever file causes an error (likely ADB.exe if timing is right)
        string lastFile = string.Empty;
        try
        {
            if (adbFunctional)
            {
                logger.Log("Attempting to kill ADB.");
                AdbCommandOutput adbOutput = await AdbManager.KillAdbServerAsync(noThrow: false);
                logger.LogDebug(adbOutput.ToString());
            }

            using ZipArchive archive = new(File.OpenRead(zipFile), ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                string entryPath = Path.Combine(appDataPath, entry.FullName);

                if (entry.FullName.EndsWith('/'))
                {
                    _ = Directory.CreateDirectory(entryPath);
                    continue;
                }

                lastFile = Path.GetFileName(entryPath);
                entry.ExtractToFile(entryPath, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to install platform tools [{lastFile}]: {ex.Message}");
            return;
        }

        File.Delete(zipFile);

        logger.Log("Updating ADB configuration.");

        ConfigurationFactory configFactory = App.GetService<ConfigurationFactory>()!;
        AdbConfig adbConfig = configFactory.GetConfig<AdbConfig>();

        adbConfig.PlatformToolsPath = $"{appDataPath}\\platform-tools";
        configFactory.SaveConfig(adbConfig);

        logger.Log("Testing adb...");
        AdbCommandOutput output = await AdbManager.QuickCommandAsync("--version", token: token);
        logger.Log(output.StdOut);

        if (output.Errored)
        {
            logger.LogError("Failed to install platform tools!");
            return;
        }

        logger.LogSuccess("Platform tools installed successfully!");

        await App.Current.Dispatcher.InvokeAsync(App.GetService<MainWindowViewModel>()!.AddAdbDeviceTray);
    }

    [Command("install-java", "Installs JDK 24 (guided install)", NO_USAGE)]
    private static async Task InstallJavaCommandAsync(ParsedCommand __, IViewLogger logger, CancellationToken token)
    {
        logger.Log("Installing JDK 24...");

        string tempDirectory = Path.Combine(Path.GetTempPath(), "APKognito-JavaTmp");
        _ = Directory.CreateDirectory(tempDirectory);
        _ = DirectoryManager.ClaimDirectory(tempDirectory);

        string javaDownload = Path.Combine(tempDirectory, "jdk-24.exe");

        if (!File.Exists(javaDownload))
        {
            using (IDisposable? scope = logger.BeginScope("CLIENT"))
            {
                bool result = await WebGet.DownloadAsync(AdbManager.JDK_24_INSTALL_EXE_LINK, javaDownload, logger, token);

                if (!result)
                {
                    logger.LogError("Failed to install JDK 24.");
                    return;
                }
            }
        }
        else
        {
            logger.Log("Using previously downloaded installer.");
        }

        using Process installer = new()
        {
            StartInfo = new()
            {
                FileName = javaDownload,
                UseShellExecute = true,
                Verb = "runas",
            }
        };

        try
        {
            logger.Log("Waiting for installer to exit...");
            _ = installer.Start();
            await installer.WaitForExitAsync(token);

            // 0: Install successful
            // 1602: Any kind of user decline during the installer (including if it was already installed and user was prompted for removal), or, if the installer feels like fucking with you.
            if (installer.ExitCode is not 0)
            {
                logger.LogWarning("Java install likely aborted! Checking Java executable path...");
            }
            else
            {
                logger.LogSuccess("JDK 24 installed successfully! Checking Java executable path...");
            }

            JavaVersionInformation? foundVersion = JavaVersionCollector.RefreshJavaVersions().FirstOrDefault(v => v.Version.Major is 24);

            if (foundVersion is not null)
            {
                logger.LogSuccess($"Detected {foundVersion}");
            }
            else
            {
                logger.LogError("Failed to detect newly installed JDK. Try restarting APKognito, or you computer, or reinstalling.");
                return;
            }

            File.Delete(javaDownload);
        }
        catch (Win32Exception)
        {
            logger.LogWarning("Installer canceled.");
            return;
        }
        finally
        {
            if (File.Exists(javaDownload))
            {
                logger.Log($"The JDK installer has not been deleted in case you want to install later. You can find it in the Drive Footprint page, or:\n{javaDownload}");
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class CommandAttribute : Attribute
    {
        public string CommandName { get; }

        public string HelpInfo { get; }

        public string CommandUsage { get; }

        public bool IsVisible { get; }

        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "It's literally used in this class, how does Sonar not see that?")]
        [SuppressMessage("CodeQuality",
            "IDE0079:Remove unnecessary suppression",
            Justification = "Without the suppression, there's a warning for the suppression that should suppress another warning. Am I having a stroke?")]
        public CommandAttribute(string commandName, string helpInfo, string usage, bool visible = true)
        {
            CommandName = commandName;
            HelpInfo = helpInfo;
            CommandUsage = usage;
            IsVisible = visible;
        }

        /// <summary>
        /// Creates an invisible command. It will not be listed when using the ':help' command.
        /// </summary>
        /// <param name="commandName"></param>
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "")]
        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "")]
        public CommandAttribute(string commandName)
        {
            CommandName = commandName;
            HelpInfo = CommandUsage = string.Empty;
            IsVisible = false;
        }

        public override string ToString()
        {
            return CommandName;
        }
    }

    internal sealed record CommandInfo
    {
        public string CommandName { get; }
        public string HelpInfo { get; }
        public string CommandUsage { get; }
        public bool IsVisible { get; }

        internal MethodInfo CommandMethod { get; }

        public bool IsAsync => CommandMethod.ReturnType == typeof(Task) || CommandMethod.ReturnType.IsSubclassOf(typeof(Task));

        internal CommandInfo(CommandAttribute commandAttribute, MethodInfo commandMethod)
        {
            CommandName = commandAttribute.CommandName;
            HelpInfo = commandAttribute.HelpInfo;
            CommandUsage = commandAttribute.CommandUsage;
            IsVisible = commandAttribute.IsVisible;
            CommandMethod = commandMethod;
        }
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "It's okay here.")]
    private static void RegisterCommands()
    {
        if (s_commands.Count > 0)
        {
            return;
        }

        MethodInfo[] methods = typeof(AdbConsoleViewModel).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (MethodInfo method in methods)
        {
            CommandAttribute? commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            if (commandAttribute != null)
            {
                var commandInfo = new CommandInfo(commandAttribute, method);
                s_commands.Add(commandInfo);

                if (commandAttribute.CommandName.Length > s_longestCommandName)
                {
                    s_longestCommandName = commandAttribute.CommandName.Length;
                }
            }
        }
    }

    private static async Task InvokeCommandAsync(CommandInfo commandInfo, ParsedCommand parsedCommand, IViewLogger logger, AdbConsoleViewModel? targetObject, CancellationToken token)
    {
        ParameterInfo[] parameters = commandInfo.CommandMethod.GetParameters();
        var arguments = new List<object>();

        foreach (Type? param in parameters.Select(p => p.ParameterType))
        {
            if (param == typeof(ParsedCommand))
            {
                arguments.Add(parsedCommand);
            }
            else if (param == typeof(IViewLogger))
            {
                arguments.Add(logger);
            }
            else if (param == typeof(CancellationToken))
            {
                arguments.Add(token);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported parameter type: {param.Name} for command method {commandInfo.CommandMethod.Name}");
            }
        }

        try
        {
            AdbConsoleViewModel? target = null;

            if (!commandInfo.CommandMethod.IsStatic)
            {
                if (targetObject is null)
                {
                    throw new InvalidOperationException("Unable to invoke command with null target object.");
                }

                target = targetObject;
            }

            object? result = commandInfo.CommandMethod.Invoke(target, [.. arguments]);

            if (commandInfo.IsAsync && result is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException ex)
        {
            logger.Log($"Command execution error: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            logger.Log($"An unexpected error occurred during command invocation: {ex.Message}");
        }
    }
}
