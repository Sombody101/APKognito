using System.Text.RegularExpressions;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.ConsoleCommands;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages;

public partial class AdbConsoleViewModel : LoggableObservableObject, IViewable
{
    public const string NO_USAGE = "";
    public const string VARIABLE_SETTER = "__VARIABLE_SETTER";

    private readonly AdbManager _adbManager;
    private readonly ConfigurationFactory _configFactory;
    private readonly AdbConfig _adbConfig;
    private readonly AdbHistory _adbHistory;

    private readonly CommandHost _commandHost;

    private int _historyIndex;

    #region Properties

    [ObservableProperty]
    public partial string CommandBuffer { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int CursorPosition { get; set; } = 0;

    #endregion Properties

    public AdbConsoleViewModel(
        ConfigurationFactory configFactory,
        ISnackbarService snackbarService
    ) : base(configFactory)
    {
        SetSnackbarProvider(snackbarService);
        LogIconPrefixes = false;
        _adbManager = new();

        _configFactory = configFactory;
        _adbConfig = _configFactory.GetConfig<AdbConfig>();
        _adbHistory = _configFactory.GetConfig<AdbHistory>();

        _historyIndex = _adbHistory.CommandHistory.Count;
        _commandHost = new();
    }

    #region Commands

    [RelayCommand]
    private async Task OnExecuteAsync()
    {
        try
        {
            WriteGenericLogLine($"{_adbHistory.GetVariable("PS1")}{CommandBuffer}");

            _adbHistory.CommandHistory.Add(CommandBuffer);
            _historyIndex = _adbHistory.CommandHistory.Count;
            _configFactory.SaveConfig(_adbHistory);

            if (!string.IsNullOrWhiteSpace(CommandBuffer))
            {
                FileLogger.Log($"Running command: {CommandBuffer}");
                await EnterCommandAsync();
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
        if (_historyIndex - 1 > _adbHistory.CommandHistory.Count || _historyIndex is 0)
        {
            return;
        }

        CommandBuffer = _adbHistory.CommandHistory[--_historyIndex];
        CursorPosition = CommandBuffer.Length;
    }

    [RelayCommand]
    private void OnHistoryDown()
    {
        if (_historyIndex >= _adbHistory.CommandHistory.Count)
        {
            return;
        }

        CommandBuffer = _adbHistory.CommandHistory[_historyIndex++];
        CursorPosition = CommandBuffer.Length;
    }

    #endregion Commands

    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "They do different things.")]
    public async Task EnterCommandAsync(string? command = null)
    {
        // Allows for the command to be overridden if passed
        command ??= CommandBuffer;
        CommandBuffer = string.Empty;

        // Variable resolution
        foreach (Match match in ParsedCommand.VariableUsageRegex().Matches(command))
        {
            string variableValue = _adbHistory.GetVariable(match.Value[1..]);

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
        if (await RunInternalCommandAsync(command))
        {
            return;
        }

        if (!_adbManager.IsRunning)
        {
            _adbManager.RunCommand(
                command,
                // Regular output
                (sender, e) => WriteGenericLogLine(e.Data ?? string.Empty),
                // Error
                (sender, e) => WriteGenericLogLine(e.Data ?? string.Empty, Brushes.Red),
                // Exit
                (sender, e) => WriteGenericLogLine($"Adb exited with code {_adbManager.AdbProcess!.ExitCode}"));
        }
        else
        {
            await _adbManager.AdbProcess!.StandardInput.WriteLineAsync(command);
            await _adbManager.AdbProcess.StandardInput.FlushAsync();
        }
    }

    private async ValueTask<bool> RunInternalCommandAsync(string rawCommand, IViewLogger? logger = null)
    {
        rawCommand = rawCommand.Trim();

        if (!rawCommand.StartsWith(':'))
        {
            // Not an internal command
            return false;
        }

        ParsedCommand command = new(rawCommand[1..]);

        if (command.IsCmdlet)
        {
            // This is a cmdlet
            if (!_adbConfig.UserCmdlets.TryGetValue(command.Command, out string? cmdletBody))
            {
                LogError($"Unknown cmdlet '{command.Command}'");

                string closest = StringMatch.GetClosestMatch(command.Command, _adbConfig.UserCmdlets.Select(cmd => cmd.Key));
                Log($"Did you mean '::{closest}'?");

                return true;
            }

            await EnterCommandAsync($"{cmdletBody} {string.Join(' ', command.Args)}");
            return true;
        }

        CommandInfo? wantedCommand = _commandHost.Commands.FirstOrDefault(c => c.CommandName == command.Command);

        if (wantedCommand is null)
        {
            LogError($"Unknown command '{command.Command}'");

            string closest = StringMatch.GetClosestMatch(command.Command, _commandHost.Commands.Select(cmd => cmd.CommandName));
            Log($"Did you mean ':{closest}'?");

            return true;
        }

        try
        {
            await _commandHost.InvokeCommandDirectAsync(wantedCommand, command, logger ?? this);
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

        public string? this[int index] => ArgCount <= index || ArgCount is 0 ? null : Args[index];

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

        public ParsedCommand(string command)
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

        public bool ContainsArgs(params string[] args)
        {
            return Array.Exists(Args, a => args.Contains(a));
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
            {
                ;
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
                ;
            }

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
