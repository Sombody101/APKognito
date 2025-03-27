using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

using ActionCommand = Action<AdbConsoleViewModel.ParsedCommand>;

public partial class AdbConsoleViewModel : LoggableObservableObject, IViewable
{
    private const string NO_USAGE = "";
    private const string VARIABLE_SETTER = "__VARIABLE_SETTER";

    private readonly AdbManager adbManager;
    private readonly AdbConfig adbConfig = ConfigurationFactory.Instance.GetConfig<AdbConfig>();
    private readonly AdbHistory adbHistory = ConfigurationFactory.Instance.GetConfig<AdbHistory>();

    private int historyIndex;

    #region Properties

    [ObservableProperty]
    private double _maxHeight = 500;

    [ObservableProperty]
    private string _commandBuffer = string.Empty;

    [ObservableProperty]
    private int _cursorPosition = 0;

    #endregion Properties

    public AdbConsoleViewModel(ISnackbarService _snackbarService)
    {
        SetSnackbarProvider(_snackbarService);
        LogIconPrefixes = false;
        adbManager = new();

        historyIndex = adbHistory.CommandHistory.Count;

        if (commands.Count is 0)
        {
            CacheInternalCommands();
        }
    }

    #region Commands

    [RelayCommand]
    private async Task OnExecute()
    {
        try
        {
            WriteGenericLogLine($"{adbHistory.GetVariable("PS1")}{CommandBuffer}");

            adbHistory.CommandHistory.Add(CommandBuffer);
            historyIndex = adbHistory.CommandHistory.Count;
            ConfigurationFactory.Instance.SaveConfig(adbHistory);

            if (!string.IsNullOrWhiteSpace(CommandBuffer))
            {
                FileLogger.Log($"Running command: {CommandBuffer}");
                await EnterCommand();
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            LogError($"Failed to execute command: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OnHistoryUp()
    {
        if (historyIndex - 1 > adbHistory.CommandHistory.Count || historyIndex is 0)
        {
            return;
        }

        CommandBuffer = adbHistory.CommandHistory[--historyIndex];
        CursorPosition = CommandBuffer.Length;
    }

    [RelayCommand]
    private void OnHistoryDown()
    {
        if (historyIndex >= adbHistory.CommandHistory.Count)
        {
            return;
        }

        CommandBuffer = adbHistory.CommandHistory[historyIndex++];
        CursorPosition = CommandBuffer.Length;
    }

    #endregion Commands

    public async Task EnterCommand(string? command = null)
    {
        // Allows for the command to be overridden if passed
        command ??= CommandBuffer;
        CommandBuffer = string.Empty;

        // Variable resolution
        foreach (Match match in ParsedCommand.VariableUsageRegex().Matches(command))
        {
            string variableValue = adbHistory.GetVariable(match.Value[1..]);

            command = command.Remove(match.Index, match.Length)
                .Insert(match.Index, variableValue);
        }

        Match setterMatch = ParsedCommand.VariableAssignmentRegex().Match(command);
        if (setterMatch.Success)
        {
            // A variable setter is technically an individual action, so this is
            // reformatted to be a command call.
            command = $":{VARIABLE_SETTER} {setterMatch.Groups["var_name"]} {setterMatch.Groups["var_value"]}";
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        // If the command is internal, run it and return
        if (await RunInternalCommand(command))
        {
            return;
        }

        if (!adbManager.IsRunning)
        {
            adbManager.RunCommand(
                command,
                // Regular output
                (sender, e) => WriteGenericLogLine(e.Data ?? string.Empty),
                // Error
                (sender, e) => WriteGenericLogLine(e.Data ?? string.Empty, Brushes.Red),
                // Exit
                (sender, e) => WriteGenericLogLine($"Adb exited with code {adbManager.AdbProcess!.ExitCode}"));
        }
        else
        {
            await adbManager.AdbProcess!.StandardInput.WriteLineAsync(command);
            await adbManager.AdbProcess.StandardInput.FlushAsync();
        }
    }

    private async ValueTask<bool> RunInternalCommand(string rawCommand)
    {
        rawCommand = rawCommand.Trim();

        if (!rawCommand.StartsWith(':'))
        {
            // Not an internal command
            return false;
        }

        ParsedCommand command = new(rawCommand[1..], adbHistory);

        if (command.IsCmdlet)
        {
            // This is a cmdlet
            if (!adbConfig.UserCmdlets.TryGetValue(command.Command, out string? cmdletBody))
            {
                LogError($"Unknown cmdlet '{command.Command}'");

                string closest = StringMatch.GetClosestMatch(command.Command, adbConfig.UserCmdlets.Select(cmd => cmd.Key));
                Log($"Did you mean '::{closest}'?");

                return true;
            }

            await EnterCommand($"{cmdletBody} {string.Join(' ', command.Args)}");
            return true;
        }

        KeyValuePair<CommandAttribute, ActionCommand> wantedPair = commands.FirstOrDefault(commandPair => commandPair.Key.CommandName == command.Command);

        if (wantedPair.Equals(default(KeyValuePair<CommandAttribute, ActionCommand>)))
        {
            LogError($"Unknown command '{command.Command}'");

            string closest = StringMatch.GetClosestMatch(command.Command, commands.Select(cmd => cmd.Key.CommandName));
            Log($"Did you mean ':{closest}'?");

            return true;
        }

        try
        {
            wantedPair.Value.Invoke(command);
        }
        catch (Exception ex)
        {
            LogError($"Unexpected error while running '{command.Command}': {ex.Message}");
            FileLogger.LogException(ex);

#if DEBUG
            LogError(ex.StackTrace ?? "\t[No trace]");
#endif
        }

        return true;
    }

    internal sealed partial class ParsedCommand
    {
        public string Command { get; }

        public string[] Args { get; }

        public int ArgCount => Args.Length;

        public string? this[int index]
        {
            get
            {
                if (ArgCount <= index || ArgCount is 0)
                {
                    return null;
                }

                return Args[index];
            }
        }

        public string[] this[Range range]
        {
            get
            {
                try
                {
                    return Args[range];
                }
                catch
                {
                    return [];
                }
            }
        }

        public bool IsCmdlet { get; }

        public ParsedCommand(string command, AdbHistory adbHistory)
        {
            if (command.StartsWith(':'))
            {
                IsCmdlet = true;
                command = command[1..];
            }

            string[] split = command.Split();

            if (split.Length is 1)
            {
                Command = split[0];
                Args = [];
            }
            else if (split.Length > 1)
            {
                Command = split[0];
                Args = split[1..];
            }
            else
            {
                throw new ArgumentException("Command isn't long enough.");
            }
        }

        public override string ToString()
        {
            return $"{Command} {string.Join(' ', Args)}";
        }

        /*
         * Regex
         */

        [GeneratedRegex(@"\$\{(?<var>[a-zA-Z_][a-zA-Z0-9_]*)\}|\$(?<var>[a-zA-Z0-9_][a-zA-Z0-9_]*)")]
        internal static partial Regex VariableUsageRegex();

        [GeneratedRegex(@"(?<var_name>[a-zA-Z_][a-zA-Z0-9_]*)[ ]*\=[ ]*(?<var_value>.*)")]
        internal static partial Regex VariableAssignmentRegex();
    }

    /*
     * Internal Commands
     */

    private readonly Dictionary<CommandAttribute, ActionCommand> commands = [];

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
                ActionCommand action = (ActionCommand)Delegate.CreateDelegate(typeof(ActionCommand), this, methodInfo);
                commands.Add(attribute, action);

                if (attribute.CommandName.Length > longestCommandName)
                {
                    longestCommandName = attribute.CommandName.Length;
                }
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

        foreach (var command in commands.Select(p => p.Key).Where(p => p.Visible))
        {
            _ = output.Append(':')
                .Append(command.CommandName.PadRight(longestCommandName))
                .Append(' ');

            if (command.CommandUsage.Length is not 0)
            {
                output.AppendLine(command.CommandUsage)
                    .Append('\t');
            }
            else
            {
                output.AppendLine("(no parameters)")
                    .Append(new string('\t', longestCommandName / 8));
            }

            output.AppendLine(command.HelpInfo).AppendLine();
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

            foreach (var command in adbConfig.UserCmdlets)
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
        ConfigurationFactory.Instance.SaveConfig(adbConfig);
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
            ConfigurationFactory.Instance.SaveConfig(adbConfig);
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
            ConfigurationFactory.Instance.SaveConfig(adbConfig);

            WriteGenericLogLine("Testing adb...");
            AdbCommandOutput output = (await AdbManager.QuickCommand("--version"));
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

    [Command("echo", "Prints all arguments to the console.", "[text ...]")]
    private void EchoCommand(ParsedCommand ctx)
    {
        Log(string.Join(' ', ctx.Args));
    }

    [Command("vars", "Prints all set variables.", NO_USAGE)]
    private void PrintAllVariablesCommand(ParsedCommand _)
    {
        if (adbHistory.Variables.Count is 0)
        {
            Log("There are no set variables.");
            return;
        }

        StringBuilder builder = new();
        foreach (var pair in adbHistory.Variables)
        {
            builder.Append(pair.Key).Append("=\'").Append(pair.Value).AppendLine("'");
        }

        Log(builder.ToString());
    }

    [Command(VARIABLE_SETTER)]
    private void SetVariableCommand(ParsedCommand ctx)
    {
        // Args 0: Variable name
        // Args 1: Variable value
        adbHistory.SetVariable(ctx.Args[0], ctx.Args[1]);
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CommandAttribute : Attribute
    {
        public string CommandName { get; }

        public string HelpInfo { get; }

        public string CommandUsage { get; }

        public bool Visible { get; }

        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "It's literally used in this class, how does Sonar not see that?")]
        [SuppressMessage("CodeQuality",
            "IDE0079:Remove unnecessary suppression",
            Justification = "Without the suppression, there's a warning for the suppression that should suppress another warning. Am I having a stroke?")]
        public CommandAttribute(string commandName, string helpInfo, string usage, bool visible = true)
        {
            CommandName = commandName;
            HelpInfo = helpInfo;
            CommandUsage = usage;
            Visible = visible;
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
            Visible = false;
        }

        public override string ToString()
        {
            return CommandName;
        }
    }

    private struct StringMatch
    {
        public static string GetClosestMatch(string input, IEnumerable<string> compare)
        {
            int bestDistance = int.MaxValue;
            int index = int.MaxValue;

            for (int i = 0; i < compare.Count(); i++)
            {
                int distance = CompareStrings(input, compare.ElementAt(i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    index = i;
                }
            }

            return compare.ElementAt(index);
        }

        [SuppressMessage("Minor Code Smell", "S1116:Empty statements should be removed", Justification = "Value is incremented in for loop updation")]
        private static int CompareStrings(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n is 0)
            {
                return m;
            }

            if (m is 0)
            {
                return n;
            }

            for (int i = 0; i <= n; d[i, 0] = i++)
                ;
            for (int j = 0; j <= m; d[0, j] = j++)
                ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1])
                        ? 0
                        : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            return d[n, m];
        }
    }
}