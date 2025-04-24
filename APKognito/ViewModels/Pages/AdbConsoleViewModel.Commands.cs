using APKognito.AdbTools;
using APKognito.Helpers;
using APKognito.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

using ActionCommand = System.Action<APKognito.ViewModels.Pages.AdbConsoleViewModel.ParsedCommand>;
using AsyncCommand = System.Func<APKognito.ViewModels.Pages.AdbConsoleViewModel.ParsedCommand, System.Threading.Tasks.Task>;

namespace APKognito.ViewModels.Pages;

public partial class AdbConsoleViewModel
{
    private readonly List<CommandInfo> commands = [];

    private int longestCommandName = 0;

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
    private void CacheInternalCommands()
    {
        foreach (MethodInfo methodInfo in typeof(AdbConsoleViewModel).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            CommandAttribute? attribute = methodInfo.GetCustomAttribute<CommandAttribute>();
            if (attribute is null)
            {
                continue;
            }

            try
            {
                CommandInfo commandInfo = new(attribute)
                {
                    IsAsync = methodInfo.ReturnType == typeof(Task)
                };

                if (commandInfo.IsAsync)
                {
                    commandInfo.AsyncCommand = (AsyncCommand)Delegate.CreateDelegate(typeof(AsyncCommand), this, methodInfo);
                }
                else
                {
                    commandInfo.Command = (ActionCommand)Delegate.CreateDelegate(typeof(ActionCommand), this, methodInfo);
                }

                if (attribute.CommandName.Length > longestCommandName)
                {
                    longestCommandName = attribute.CommandName.Length;
                }

                commands.Add(commandInfo);
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize '{attribute.CommandName}' ({methodInfo.Name}()): {ex.Message}");
            }
        }
    }

#pragma warning disable IDE0051 // Remove unused private members

    [Command("help", "Prints this help information.", NO_USAGE)]
    private void GetHelpInfoCommand(ParsedCommand __)
    {
        StringBuilder output = new();

        foreach (CommandInfo? command in commands.Where(c => c.IsVisible))
        {
            _ = output.Append(':')
                .Append(command.CommandName.PadRight(longestCommandName))
                .Append(' ');

            _ = command.CommandUsage.Length is not 0
                ? output.AppendLine(command.CommandUsage)
                    .Append('\t')
                : output.AppendLine("(no parameters)")
                    .Append('\t');

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
        if (!adbManager.IsRunning)
        {
            Log("There is no active ADB process.");
            return;
        }

        Process process = adbManager.AdbProcess!;

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
            if (adbConfig.UserCmdlets.Count is 0)
            {
                Log("No cmdlets set.");
                return;
            }

            foreach (KeyValuePair<string, string> command in adbConfig.UserCmdlets)
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
        adbConfig.UserCmdlets[cmdlet] = string.Join(' ', ctx[1..]);
        Log($"Cmdlet '::{cmdlet}' created.");
        configFactory.SaveConfig(adbConfig);
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
        if (adbConfig.UserCmdlets.Remove(cmdlet))
        {
            Log($"Removed cmdlet '{cmdlet}'");
            configFactory.SaveConfig(adbConfig);
        }
        else
        {
            LogError($"Commandlet '{cmdlet}' not defined, no cmdlet removed.");
        }
    }

    [Command("install-adb", "Auto installs platform tools.", "[--force|-f]")]
    private void InstallAdbCommand(ParsedCommand ctx)
    {
        bool result = ThreadPool.QueueUserWorkItem(async (__) =>
        {
            if (AdbManager.AdbWorks()
                && !ctx.Args.Contains("--force")
                && !ctx.Args.Contains("-f"))
            {
                LogError("ADB is already installed. Run with '--force' to force a reinstall.");
                return;
            }

            string appDataPath = App.AppDataDirectory.FullName;
            WriteGenericLogLine($"Installing platform tools to: {appDataPath}\\platform-tools");

            string zipFile = $"{appDataPath}\\adb.zip";
            _ = await WebGet.DownloadAsync(Constants.ADB_INSTALL_URL, zipFile, this, CancellationToken.None);

            WriteGenericLogLine("Unpacking platform tools.");

            // Keeps track of the most recent file extraction attempt and shows it to the user
            // in the try/catch.
            string lastFile = string.Empty;
            try
            {
                using ZipArchive archive = new(File.OpenRead(zipFile), ZipArchiveMode.Read);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
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
                LogError($"Failed to install platform tools [{lastFile}]: {ex.Message}");
                return;
            }

            File.Delete(zipFile);

            WriteGenericLogLine("Updating ADB configuration.");
            adbConfig.PlatformToolsPath = $"{appDataPath}\\platform-tools";
            configFactory.SaveConfig(adbConfig);

            WriteGenericLogLine("Testing adb...");
            AdbCommandOutput output = await AdbManager.QuickCommandAsync("--version");
            WriteGenericLogLine(output.StdOut);

            if (output.Errored)
            {
                LogError("Failed to install platform tools!");
                return;
            }

            LogSuccess("Platform tools installed successfully!");
        });

        if (!result)
        {
            LogError("Failed to queue download on the thread pool. Close some apps or try again later.");
        }
    }

    [Command("install-java", "Installs JDK 24 (guided install)", NO_USAGE)]
    private async Task InstallJavaCommandAsync(ParsedCommand __)
    {
        Log("Installing JDK 24...");

        string tempDirectory = Path.Combine(Path.GetTempPath(), "APKognito-JavaTmp");
        _ = Directory.CreateDirectory(tempDirectory);
        DriveUsageViewModel.ClaimDirectory(tempDirectory);

        string javaDownload = Path.Combine(tempDirectory, "jdk-24.exe");

        if (File.Exists(javaDownload))
        {
            Log("Using previous install attempt executable.");
            await FinishInstall();
            return;
        }

        AddIndentString("+ ");
        bool result = await WebGet.DownloadAsync(AdbManager.JDK_24_INSTALL_EXE_LINK, javaDownload, this, CancellationToken.None);
        ResetIndent();

        if (!result)
        {
            LogError("Failed to install JDK 24.");
            return;
        }

        await FinishInstall();

        async Task FinishInstall()
        {
            Process installer = new()
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
                Log("Waiting for installer to exit...");
                _ = installer.Start();
                await installer.WaitForExitAsync();


                if (installer.ExitCode is not 0)
                {
                    LogWarning("Java install aborted!");
                    return;
                }

                LogSuccess("JDK 24 installed successfully! Checking Java executable path...");
                _ = new JavaVersionLocator().GetJavaPath(out _, this);

                File.Delete(javaDownload);
            }
            catch (Win32Exception)
            {
                LogWarning("Installer canceled.");
                return;
            }
            finally
            {
                if (File.Exists(javaDownload))
                {
                    Log($"The JDK installer has not been deleted in case you want to install later. You can find it in the Drive Footprint page, or:\n{javaDownload}");
                }
            }
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
        if (adbHistory.Variables.Count is 0)
        {
            Log("There are no set variables.");
            return;
        }

        StringBuilder builder = new();
        foreach (KeyValuePair<string, string> pair in adbHistory.Variables)
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
        adbHistory.SetVariable(ctx.Args[0], ctx.Args[1]);
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
                Log($"GC: {GBConverter.FormatSizeFromBytes(GC.GetTotalMemory(true))}");
                Log($"Private: {GBConverter.FormatSizeFromBytes(Process.GetCurrentProcess().PrivateMemorySize64)}");
                return;

            default:
                LogError($"Unknown sys option '{option}'");
                return;
        }
    }

#pragma warning restore IDE0051 // Remove unused private members

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CommandAttribute : Attribute
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

    private sealed class CommandInfo
    {
        public string CommandName { get; }

        public string HelpInfo { get; }

        public string CommandUsage { get; }

        public bool IsVisible { get; }

        public bool IsAsync { get; set; } = false;

        public AsyncCommand? AsyncCommand { get; set; } = null;

        public ActionCommand? Command { get; set; } = null;

        public CommandInfo(CommandAttribute commandAttribute)
        {
            CommandName = commandAttribute.CommandName;
            HelpInfo = commandAttribute.HelpInfo;
            CommandUsage = commandAttribute.CommandUsage;
            IsVisible = commandAttribute.IsVisible;
        }
    }
}
