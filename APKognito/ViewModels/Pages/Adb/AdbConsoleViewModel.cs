using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class AdbConsoleViewModel : LoggableObservableObject, IViewable
{
    private readonly ISnackbarService snackbarService;
    private readonly AdbManager adbManager;
    private readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    #region Properties

    [ObservableProperty]
    private ObservableCollection<AdbFolderInfo> _adbFolders = [];

    [ObservableProperty]
    private ObservableCollection<ComboBoxItem> _deviceList = [];

    [ObservableProperty]
    private ComboBoxItem _selectedDevice;

    [ObservableProperty]
    private double _maxHeight = 500;

    [ObservableProperty]
    private string _commandBuffer;

    #endregion Properties

    public AdbConsoleViewModel(ISnackbarService _snackbarService)
    {
        snackbarService = _snackbarService;
        adbManager = new();

        if (commands.Count is 0)
        {
            CacheInternalCommands();
        }
    }

    #region Commands

    [RelayCommand]
    private async Task OnExecute()
    {
        WriteGenericLogLine($"> {CommandBuffer}");
        await EnterCommand();
    }

    #endregion Commands

    public async Task RefreshDevicesList()
    {
        try
        {
            string[] devices = [.. await AdbManager.GetAllDevices()];

            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                DeviceList.Clear();

                if (devices.Length is 1)
                {
                    SelectedDevice = new() { Content = devices[0] };
                    DeviceList.Add(SelectedDevice);
                }
                else
                {
                    foreach (string device in devices)
                    {
                        DeviceList.Add(new() { Content = device });
                    }
                }
            });
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            snackbarService.Show(
                "Failed to get devices",
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                TimeSpan.FromSeconds(10)
            );
        }
    }

    public async Task EnterCommand(string? command = null)
    {
        // Allows for the command to be overridden if passed
        command ??= CommandBuffer;

        if (RunInternalCommand(command))
        {
            CommandBuffer = string.Empty;
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
            await adbManager.AdbProcess!.StandardInput.WriteLineAsync(CommandBuffer);
            await adbManager.AdbProcess.StandardInput.FlushAsync();
        }

        CommandBuffer = string.Empty;
    }

    private bool RunInternalCommand(string rawCommand)
    {
        rawCommand = rawCommand.Trim();

        if (!rawCommand.StartsWith(':'))
        {
            // Not an internal command
            return false;
        }

        ParsedCommand command = new(rawCommand[1..]);

        KeyValuePair<CommandAttribute, Action> wantedPair = commands.FirstOrDefault(commandPair => commandPair.Key.CommandName == command.Command);

        if (wantedPair.Equals(default(KeyValuePair<CommandAttribute, Action>)))
        {
            WriteGenericLogLine($"Unknown command '{rawCommand}'");
            return true;
        }

        try
        {
            wantedPair.Value.Invoke();
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

    private sealed class ParsedCommand
    {
        public string Command { get; }

        public string[] Args { get; }

        public ParsedCommand(string command)
        {
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
    }

    /*
     * Internal Commands
     */

    private readonly Dictionary<CommandAttribute, Action> commands = [];
    private int longestCommandName = 0;

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
    private void CacheInternalCommands()
    {
        foreach (MethodInfo methodInfo in typeof(AdbConsoleViewModel).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            CommandAttribute? attribute = methodInfo.GetCustomAttribute<CommandAttribute>();
            if (attribute is not null)
            {
                Action action = (Action)Delegate.CreateDelegate(typeof(Action), this, methodInfo);
                commands.Add(attribute, action);

                if (attribute.CommandName.Length > longestCommandName)
                {
                    longestCommandName = attribute.CommandName.Length;
                }
            }
        }
    }

    [Command("help", "Prints this help information.")]
    private void GetHelpInfoCommand()
    {
        StringBuilder output = new();

        foreach (var pair in commands.Select(p => p.Key))
        {
            _ = output.Append(':').Append(pair.CommandName.PadRight(longestCommandName + 3))
                .AppendLine(pair.HelpInfo);
        }

        WriteGenericLogLine(output.ToString());
    }

    [Command("clear", "Clears the log buffer.")]
    private void ClearLogsCommand()
    {
        ClearLogs();
    }

    [Command("active", "Gets information about the currently running ADB process.")]
    private void ActiveStatusCommand()
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

    [Command("install-adb", "Auto installs platform tools.")]
    private void InstallAdbCommand()
    {
        bool result = ThreadPool.QueueUserWorkItem(async (__) =>
        {
            string appDataPath = App.AppData.FullName;
            WriteGenericLogLine($"Installing platform tools to: {appDataPath}\\platform-tools");

            string zipFile = $"{appDataPath}\\adb.zip";
            _ = await Installer.DownloadAsync(Constants.ADB_INSTALL_URL, zipFile, this, CancellationToken.None);

            WriteGenericLogLine("Unpacking platform tools.");

            using (ZipArchive archive = new(File.OpenRead(zipFile), ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryPath = Path.Combine(appDataPath, entry.FullName);

                    if (entry.FullName.EndsWith('/'))
                    {
                        _ = Directory.CreateDirectory(entryPath);
                        continue;
                    }

                    entry.ExtractToFile(entryPath);
                }
            }

            File.Delete(zipFile);

            WriteGenericLogLine("Updating ADB configuration.");
            adbConfig.PlatformToolsPath = $"{appDataPath}\\platform-tools";

            WriteGenericLogLine("Testing adb...");
            await EnterCommand("--version");
        });

        if (!result)
        {
            LogError("Failed to queue download on the thread pool. Close some apps or try again later.");
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CommandAttribute : Attribute
    {
        public string CommandName { get; }

        public string HelpInfo { get; }

        public CommandAttribute(string commandName, string helpInfo)
        {
            CommandName = commandName;
            HelpInfo = helpInfo;
        }
    }
}