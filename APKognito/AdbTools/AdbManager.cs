using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;

namespace APKognito.AdbTools;

internal sealed class AdbManager : IDisposable
{
    private const string APKOGNITO_DIRECTORY = $"/data/local/tmp/apkognito";

    public const string ANDROID_EMULATED = "/storage/emulated/0";
    public const string ANDROID_EMULATED_BASE = $"{ANDROID_EMULATED}/Android";
    public const string ANDROID_DATA = $"{ANDROID_EMULATED_BASE}/data";
    public const string ANDROID_OBB = $"{ANDROID_EMULATED_BASE}/obb";

    public const string PLATFORM_TOOLS_INSTALL_LINK = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";
    // They replaced 23 with 24, and I have no idea where to find it anymore
    //public const string JDK_23_INSTALL_LINK = "https://www.oracle.com/java/technologies/downloads/?er=221886#jdk23-windows";
    public const string JDK_24_INSTALL_EXE_LINK = "https://download.oracle.com/java/24/latest/jdk-24_windows-x64_bin.exe";

    public Process? AdbProcess { get; private set; }

    public bool IsRunning => AdbProcess?.HasExited is false;

    private static readonly AdbConfig s_adbConfig = App.GetService<ConfigurationFactory>()!.GetConfig<AdbConfig>();

    private static bool s_noCommandRecurse = false;

    public void RunCommand(
        string arguments,
        Action<object?, DataReceivedEventArgs> stdOutRec,
        Action<object?, DataReceivedEventArgs> stdErrRec,
        Action<object?, EventArgs>? appExited,
        [Optional] string? overrideAdbPath)
    {
        AdbProcess = CreateAdbProcess(overrideAdbPath, arguments);

        var stdOutHandler = new DataReceivedEventHandler(stdOutRec);
        var stdErrHandler = new DataReceivedEventHandler(stdErrRec);

        // Assign after to please the compiler.
        EventHandler exitHandler = null!;
        exitHandler = new EventHandler(HandleAdbExit);

        AdbProcess.OutputDataReceived += stdOutHandler;
        AdbProcess.ErrorDataReceived += stdErrHandler;
        AdbProcess.Exited += exitHandler;

        AdbProcess.EnableRaisingEvents = true;

        _ = AdbProcess.Start();
        AdbProcess.BeginOutputReadLine();
        AdbProcess.BeginErrorReadLine();

        void HandleAdbExit(object? sender, EventArgs e)
        {
            AdbProcess.OutputDataReceived -= stdOutHandler;
            AdbProcess.ErrorDataReceived -= stdErrHandler;
            AdbProcess.Exited -= exitHandler;

            if (appExited is not null)
            {
                appExited(sender, e);
            }
        }
    }

    public async Task<AdbCommandOutput> RunCommandAsync(string arguments,
        Action<object?, DataReceivedEventArgs> stdOutRec,
        Action<object?, DataReceivedEventArgs> stdErrRec,
        Action<object?, EventArgs>? appExited,
        [Optional] string? overrideAdbPath,
        CancellationToken token = default)
    {
        RunCommand(arguments, stdOutRec, stdErrRec, appExited, overrideAdbPath);

        await AdbProcess!.WaitForExitAsync(token);
        return new(string.Empty, string.Empty, AdbProcess.ExitCode);
    }

    /// <summary>
    /// Runs an ADB command without a device ID.
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<AdbCommandOutput> QuickCommandAsync(string arguments, bool noThrow = false, CancellationToken token = default)
    {
        using Process adbProcess = CreateAdbProcess(null, arguments);
        _ = adbProcess.Start();
        AdbCommandOutput commandOutput = await AdbCommandOutput.ReadCommandOutputAsync(adbProcess, token);
        await adbProcess.WaitForExitAsync(token);

        if (!s_noCommandRecurse && commandOutput.StdErr.StartsWith("adb.exe: device unauthorized."))
        {
            LoggableObservableObject.GlobalFallbackLogger?.SnackWarning("Command failed!", "An ADB command failed to execute! Running an ADB server restart... (may take some time).");
            await KillAdbServerAsync();
            s_noCommandRecurse = true;

            return await QuickCommandAsync(arguments, noThrow, token);
        }
        else if (s_noCommandRecurse && commandOutput.DeviceNotAuthorized)
        {
            // If the error persists, then ADB is not enabled or authorized on the device.
            s_noCommandRecurse = false;
            return commandOutput;
        }

        commandOutput.ThrowIfError(noThrow, adbProcess.ExitCode);

        return commandOutput;
    }

    /// <summary>
    /// The same as <see cref="QuickCommand(string, CancellationToken)"/>, but automatically adds the currently selected device ID
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="deviceId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async Task<AdbCommandOutput> QuickDeviceCommandAsync(string arguments, string? deviceId = null, bool noThrow = false, CancellationToken token = default)
    {
        deviceId ??= s_adbConfig.CurrentDeviceId;
        return await QuickCommandAsync($"-s {deviceId} {arguments}", noThrow, token);
    }

    // This sort of reimplements everything to get the separate streams... Bad choices.
    public static async Task<AdbCommandOutput?> LoggedDeviceCommandAsync(string arguments, IViewLogger logger, string? deviceId = null, bool captureOutput = false, CancellationToken token = default)
    {
        deviceId ??= s_adbConfig.CurrentDeviceId;

        StringBuilder? stdOut = null;
        StringBuilder? stdErr = null;
        int exitCode = 0;

        if (captureOutput)
        {
            stdOut = new();
            stdErr = new();
        }

        using AdbManager adbInstance = new();
        await adbInstance.RunCommandAsync($"-s {deviceId} {arguments}",
            // StdOut
            (sender, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                if (captureOutput)
                {
                    stdOut!.AppendLine(e.Data);
                }

                logger.Log(e.Data);
            },
            // StdErr
            (sender, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                if (captureOutput)
                {
                    stdErr!.AppendLine(e.Data);
                }

                logger.LogError(e.Data);
            },
            // Exit
            (sender, e) =>
            {
                exitCode = ((Process)sender!).ExitCode;
            },
            token: token);

        return captureOutput
            ? new(stdOut!.ToString(), stdErr!.ToString(), exitCode)
            : null;
    }

    /// <summary>
    /// Allows for quick command usage of any application.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<ICommandOutput> QuickGenericCommandAsync(string command, string arguments, bool noThrow = false)
    {
        using Process proc = new()
        {
            StartInfo =
            {
                FileName = command,
                Arguments = arguments ?? string.Empty,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            }
        };

        _ = proc.Start();
        CommandOutput commandOutput = await CommandOutput.GetCommandOutputAsync(proc);

        await proc.WaitForExitAsync();

        commandOutput.ThrowIfError(noThrow, proc.ExitCode);

        return commandOutput;
    }

    /// <summary>
    /// Gets a formatted list of all available ADB devices.
    /// </summary>
    /// <returns></returns>
    public static async Task<IEnumerable<AdbDeviceInfo>> GetLongDeviceListAsync(bool noThrow = false)
    {
        AdbCommandOutput response = await QuickCommandAsync("devices -l", noThrow: noThrow);

        return response.StdOut.Split("\r\n")
            // Trim empty lines
            .Where(str => !string.IsNullOrWhiteSpace(str))
            // Skip ADB list header
            .Skip(1)
            .Select(HandleDeviceEntry);

        static AdbDeviceInfo HandleDeviceEntry(string str)
        {
            string[] split = [.. str.Split().Where(str => !string.IsNullOrWhiteSpace(str))];
            string deviceId = split[0];

            AdbDeviceInfo device = new(deviceId);

            string deviceStatus = split[2];
            if (deviceStatus is "unauthorized")
            {
                device.DeviceName = "[ADB Not Enabled]";
            }
            else if (deviceStatus is "offline")
            {
                device.DeviceName = "[Device Offline]";
            }
            else
            {
                try
                {
                    string modelSegment = split[3];

                    int splitIndex = modelSegment.IndexOf(':');

                    if (splitIndex is -1)
                    {
                        device.DeviceName = "[Unavailable]";
                    }

                    device.DeviceName = modelSegment[(splitIndex + 1)..].Replace('_', ' ');
                    device.DeviceAuthorized = true;
                }
                catch (Exception ex)
                {
                    FileLogger.LogException(ex);
                }
            }

            return device;
        }
    }

    /// <summary>
    /// Gets a standard list of all device IDs
    /// </summary>
    /// <returns></returns>
    public static async Task<string[]> GetDeviceListAsync(bool noThrow = false)
    {
        AdbCommandOutput response = await QuickCommandAsync("devices", noThrow: noThrow);

        return [.. response.StdOut.Split("\r\n")
            .Where(str => !string.IsNullOrWhiteSpace(str))
            .Skip(1)
            .Select(str => str.Split()[0])];
    }

    public static async Task<string> CreateApplicationCrashLogAsync(bool noThrow = false)
    {
        AdbCommandOutput response = await QuickCommandAsync($"logcat -b crash -d", noThrow: noThrow);

        string outputFile = Path.Combine(App.AppDataDirectory.FullName, $"applog-{Random.Shared.Next():x4}");

        string logs = response.StdOut;
        if (string.IsNullOrWhiteSpace(outputFile))
        {
            logs = "[No logs found]";
        }

        await File.WriteAllTextAsync(outputFile, logs);

        return outputFile;
    }

    public static async Task WakeDeviceAsync(string? deviceId = null, bool noThrow = false)
    {
        _ = await QuickDeviceCommandAsync("shell input keyevent KEYCODE_WAKEUP", deviceId, noThrow: noThrow);
    }

    /// <summary>
    /// Restarts the ADB server (should only be used when ADB throws a 'No $ADB_VENDOR_KEYS' error.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<AdbCommandOutput> RestartAdbServerAsync(bool noThrow = false)
    {
        using Process adbRestartProcess = CreateAdbProcess(null, "restart-server");
        _ = adbRestartProcess.Start();
        AdbCommandOutput output = await AdbCommandOutput.ReadCommandOutputAsync(adbRestartProcess);
        await adbRestartProcess.WaitForExitAsync();

        output.ThrowIfError(noThrow, adbRestartProcess.ExitCode);
        return output;
    }

    public static async Task<AdbCommandOutput> KillAdbServerAsync(bool noThrow = false)
    {
        using Process adbKillProcess = CreateAdbProcess(null, "kill-server");
        _ = adbKillProcess.Start();
        AdbCommandOutput output = await AdbCommandOutput.ReadCommandOutputAsync(adbKillProcess);
        await adbKillProcess.WaitForExitAsync();

        output.ThrowIfError(noThrow, adbKillProcess.ExitCode);
        return output;
    }

    public static bool AdbWorks([Optional] string? platformToolsPath, IViewLogger? logger = null)
    {
        return s_adbConfig.AdbWorks(platformToolsPath, logger);
    }

    /// <summary>
    /// Uploads ADB scripts to <see cref="APKOGNITO_DIRECTORY"/> on the selected device.
    /// Used for gathering device or package information faster.
    /// </summary>
    /// <returns></returns>
    public static async Task<(int successful, int total, ScriptPushResult result)> UploadAdbScriptsAsync(bool forceUpload = false)
    {
        if (!AdbWorks())
        {
            return (0, 0, ScriptPushResult.NoAdbDevice);
        }

        ResourceSet? resources = AdbScripts.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);

        if (resources is null)
        {
            return (0, 0, ScriptPushResult.NoScriptResources);
        }

        string scriptVersion = (await QuickDeviceCommandAsync($"shell sh {APKOGNITO_DIRECTORY}/version", noThrow: true)).StdOut;
        if (scriptVersion == App.Version.GetFullVersion() || forceUpload)
        {
            FileLogger.Log("Skipping script update, version file matches current version.");

            return (0, 0, ScriptPushResult.ScriptsUpToDate);
        }

        FileLogger.Log("Uploading ADB script files to device.");

        _ = await QuickDeviceCommandAsync($"shell [ -d {APKOGNITO_DIRECTORY} ] && rm -r {APKOGNITO_DIRECTORY}; mkdir {APKOGNITO_DIRECTORY}");

        int pushedCount = 0;
        int scriptCount = 0;
        try
        {
            foreach (DictionaryEntry entry in resources)
            {
                scriptCount++;

                string tempFile = $"./{entry.Key}.sh";
                FileLogger.Log($"Pushing {tempFile} to {APKOGNITO_DIRECTORY}");

                await File.WriteAllBytesAsync(tempFile, (byte[])entry.Value!);
                AdbCommandOutput output = await QuickDeviceCommandAsync($"push \"{tempFile}\" \"{APKOGNITO_DIRECTORY}/{entry.Key}.sh\"", noThrow: true);

                if (!output.StdErr.Contains("1 file pushed"))
                {
                    output.ThrowIfError();
                }

                File.Delete(tempFile);

                pushedCount++;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
        }

        _ = await QuickDeviceCommandAsync($"shell echo '#!/bin/sh\n\necho \"{App.Version.GetFullVersion()}\"'");

        return (pushedCount, scriptCount, ScriptPushResult.Success);
    }

    public static async Task<AdbCommandOutput> InvokeScriptAsync(string scriptName, string scriptArguments, bool noThrow = false)
    {
        return await QuickDeviceCommandAsync($"shell sh \"{APKOGNITO_DIRECTORY}/{scriptName}\" {scriptArguments}", noThrow: noThrow);
    }

    public static string GetAdbPath()
    {
        return s_adbConfig.PlatformToolsPath;
    }

    private static async Task<bool> EnsureAdbDevicesAsync()
    {
        return await GetAdbDeviceCountAsync() is 0;
    }

    private static async Task<int> GetAdbDeviceCountAsync()
    {
        AdbCommandOutput result = await QuickCommandAsync("devices");
        string output = result.StdOut.Trim();

        int count = 0;
        foreach (char c in output)
        {
            if (c is '\n')
            {
                ++count;
            }
        }

        return count;
    }

    private static Process CreateAdbProcess([Optional] string? overrideAdbPath, [Optional] string? arguments)
    {
        string adbDirectory = string.IsNullOrWhiteSpace(overrideAdbPath)
            ? s_adbConfig.PlatformToolsPath
            : overrideAdbPath;

        string adbPath = Path.Combine(adbDirectory, "adb.exe");

        if (!Directory.Exists(adbDirectory))
        {
            throw new DirectoryNotFoundException($"PlatformTools are not installed at '{adbDirectory}'.\nInstall them from {PLATFORM_TOOLS_INSTALL_LINK}, or you can run ':install-adb' to auto install them.");
        }

        if (!File.Exists(adbPath))
        {
            throw new FileNotFoundException($"Failed to locate ADB at {adbPath}. Verify that the directory is correct.");
        }

        // This comment is useless. It's just to keep the formatter from turning this entire method into an ugly ternary statement.
        return new Process()
        {
            StartInfo =
            {
                CreateNoWindow = true,
                Arguments = arguments ?? string.Empty,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                FileName = adbPath
            }
        };
    }

    public void Dispose()
    {
        AdbProcess?.Dispose();
        GC.SuppressFinalize(this);
    }

    public enum ScriptPushResult
    {
        NoAdbDevice,
        Success,
        NoScriptResources,
        ScriptsUpToDate,
    }
}
