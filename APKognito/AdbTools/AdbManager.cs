using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;

namespace APKognito.AdbTools;

internal class AdbManager
{
    private const string APKOGNITO_DIRECTORY = $"{ANDROID_EMULATED}/apkognito";

    public const string ANDROID_EMULATED = "/storage/emulated/0";
    public const string ANDROID_EMULATED_BASE = $"{ANDROID_EMULATED}/Android";
    public const string ANDROID_DATA = $"{ANDROID_EMULATED_BASE}/data";
    public const string ANDROID_OBB = $"{ANDROID_EMULATED_BASE}/obb";

    public Process? AdbProcess { get; private set; }

    public bool IsRunning => AdbProcess?.HasExited is false;

    private static readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

    private static bool _noCommandRecurse = false;

    public void RunCommand(
        string arguments,
        Action<object?, DataReceivedEventArgs> stdOutRec,
        Action<object?, DataReceivedEventArgs> stdErrRec,
        Action<object?, EventArgs> appExited,
        [Optional] string? overrideAdbPath)
    {
        AdbProcess = CreateAdbProcess(overrideAdbPath, arguments);

        AdbProcess.OutputDataReceived += new DataReceivedEventHandler(stdOutRec);
        AdbProcess.ErrorDataReceived += new DataReceivedEventHandler(stdErrRec);
        AdbProcess.Exited += new EventHandler(appExited);

        _ = AdbProcess.Start();
        AdbProcess.BeginOutputReadLine();
        AdbProcess.BeginErrorReadLine();
    }

    /// <summary>
    /// Runs an ADB command without a device ID.
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<CommandOutput> QuickCommand(string arguments, CancellationToken token = default, bool noThrow = false)
    {
        Process adbProcess = CreateAdbProcess(null, arguments);
        _ = adbProcess.Start();
        CommandOutput commandOutput = await CommandOutput.GetCommandOutput(adbProcess);
        await adbProcess.WaitForExitAsync(token);

        if (!_noCommandRecurse && commandOutput.StdErr.StartsWith("adb.exe: device unauthorized."))
        {
            LoggableObservableObject.CurrentLoggableObject?.SnackWarning("Command failed!", "An ADB command failed to execute! Running an ADB server restart... (may take some time).");
            await QuickCommand("kill-server");
            _noCommandRecurse = true;

            return await QuickCommand(arguments, token, noThrow);
        }
        else if (_noCommandRecurse && commandOutput.DeviceNotAuthorized)
        {
            // If the error persists, then ADB is not enabled or authorized on the device.
            _noCommandRecurse = false;
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
    public static async Task<CommandOutput> QuickDeviceCommand(string arguments, string? deviceId = null, CancellationToken token = default, bool noThrow = false)
    {
        deviceId ??= adbConfig.CurrentDeviceId;
        return await QuickCommand($"-s {deviceId} {arguments}", token, noThrow);
    }

    /// <summary>
    /// Allows for quick command usage of any application.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<CommandOutput> QuickGenericCommand(string command, string arguments, bool noThrow = false)
    {
        Process proc = new()
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
        CommandOutput commandOutput = await CommandOutput.GetCommandOutput(proc);

        await proc.WaitForExitAsync();

        commandOutput.ThrowIfError(noThrow, proc.ExitCode);

        return commandOutput;
    }

    /// <summary>
    /// Gets a formatted list of all available ADB devices.
    /// </summary>
    /// <returns></returns>
    public static async Task<AdbDeviceInfo[]> GetAllDevices(bool noThrow = false)
    {
        CommandOutput response = await QuickCommand("devices -l", noThrow: noThrow);

        var enumeration = response.StdOut.Split("\r\n")
            // Trim empty lines
            .Where(str => !string.IsNullOrWhiteSpace(str))
            // Skip ADB list header
            .Skip(1)
            .Select(async str =>
            {
                string[] split = [.. str.Split().Where(str => !string.IsNullOrWhiteSpace(str))];
                string deviceId = split[0];

                AdbDeviceInfo device = new(deviceId);

                switch (split[2])
                {
                    case "unauthorized":
                        device.DeviceName = "[ADB Not Enabled]";
                        break;

                    case "offline":
                        device.DeviceName = "[Device Offline]";
                        break;

                    default:
                        try
                        {
                            CommandOutput output = await QuickCommand($@"-s {deviceId} shell getprop ro.product.model");

                            if (output.DeviceNotAuthorized)
                            {
                                goto case "unauthorized";
                            }

                            device.DeviceName = output.StdOut.Trim();
                            device.DeviceAuthorized = true;
                        }
                        catch (Exception ex)
                        {
                            FileLogger.LogException(ex);
                            goto case "unauthorized";
                        }
                        break;
                }

                return device;
            });

        return await Task.WhenAll(enumeration);
    }

    /// <summary>
    /// Gets a standard list of all device IDs
    /// </summary>
    /// <returns></returns>
    public static async Task<string[]> GetDeviceList(bool noThrow = false)
    {
        CommandOutput response = await QuickCommand("devices", noThrow: noThrow);

        return [.. response.StdOut.Split("\r\n")
            .Where(str => !string.IsNullOrWhiteSpace(str))
            .Skip(1)
            .Select(str => str.Split()[0])];
    }

    public static async Task WakeDevice(string? deviceId = null, bool noThrow = false)
    {
        _ = await QuickDeviceCommand("shell input keyevent KEYCODE_WAKEUP", deviceId, noThrow: noThrow);
    }

    /// <summary>
    /// Restarts the ADB server (should only be used when ADB throws a 'No $ADB_VENDOR_KEYS' error.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task RestartAdbServer(bool noThrow = false)
    {
        Process adbRestartProcess = CreateAdbProcess(null, "restart-server");
        _ = adbRestartProcess.Start();
        CommandOutput output = await CommandOutput.GetCommandOutput(adbRestartProcess);
        await adbRestartProcess.WaitForExitAsync();

        output.ThrowIfError(noThrow, adbRestartProcess.ExitCode);
    }

    public static bool AdbWorks([Optional] string? platformToolsPath, LoggableObservableObject? snackService = null)
    {
        platformToolsPath ??= adbConfig.PlatformToolsPath;
        bool isInstalled = Directory.Exists(platformToolsPath) || File.Exists(Path.Combine(platformToolsPath, "adb.exe"));

        if (isInstalled)
        {
            snackService?.SnackError("Platform tools are not installed!", "You can:\n1. Install them by running ':install-adb' in the Console Page.\n" +
                "2. Verify the Platform Tools path in the ADB Configuration Page.\n" +
                "3. Install them manually at https://dl.google.com/android/repository/platform-tools-latest-windows.zip, then set the path in the ADB Configuration Page.");
        }

        return isInstalled;
    }

    /// <summary>
    /// Uploads ADB scripts to <see cref="APKOGNITO_DIRECTORY"/> on the selected device.
    /// Used for gathering device or package information faster.
    /// </summary>
    /// <returns></returns>
    public static async Task<(int successful, int total, ScriptPushResult result)> UploadAdbScripts(bool forceUpload = false)
    {
        if (!AdbWorks())
        {
            return (0, 0, ScriptPushResult.NoAdbDevice);
        }

        ResourceSet? resources = AdbScripts.ResourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);

        if (resources is null)
        {
            return (0, 0, ScriptPushResult.NoScriptResources);
        }

        string scriptVersion = (await QuickDeviceCommand($"shell sh {APKOGNITO_DIRECTORY}/version", noThrow: true)).StdOut;
        if (scriptVersion == App.Version.GetFullVersion() || forceUpload)
        {
            FileLogger.Log("Skipping script update, version file matches current version.");

            return (0, 0, ScriptPushResult.ScriptsUpToDate);
        }

        FileLogger.Log("Uploading ADB script files to device.");

        _ = await QuickDeviceCommand($"shell [ -d {APKOGNITO_DIRECTORY} ] && rm -r {APKOGNITO_DIRECTORY}; mkdir {APKOGNITO_DIRECTORY}");

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
                CommandOutput output = await QuickDeviceCommand($"push \"{tempFile}\" \"{APKOGNITO_DIRECTORY}\"", noThrow: true);

                // STDERR and STDOUT are being swapped for some reason.
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

        _ = await QuickDeviceCommand($"shell echo '#!/bin/sh\n\necho \"{App.Version.GetFullVersion()}\"'");

        return (pushedCount, scriptCount, ScriptPushResult.Success);
    }

    public static async Task<CommandOutput> InvokeScript(string scriptName, string scriptArguments, bool noThrow = false)
    {
        return await QuickDeviceCommand($"shell sh \"{APKOGNITO_DIRECTORY}/{scriptName}\" {scriptArguments}", noThrow: noThrow);
    }

    private static Process CreateAdbProcess([Optional] string? overrideAdbPath, [Optional] string? arguments)
    {
        string adbDirectory = string.IsNullOrWhiteSpace(overrideAdbPath)
            ? adbConfig.PlatformToolsPath
            : overrideAdbPath;

        string adbPath = Path.Combine(adbDirectory, "adb.exe");

        if (!Directory.Exists(adbDirectory))
        {
            throw new DirectoryNotFoundException($"PlatformTools are not installed at '{adbDirectory}'.\nInstall them from https://dl.google.com/android/repository/platform-tools-latest-windows.zip, or you can run ':install-adb' to auto install them.");
        }

        if (!File.Exists(adbPath))
        {
            throw new FileNotFoundException($"Failed to locate ADB at {adbPath}. Verify that the directory is correct.");
        }

        // This comment it useless. It's just to keep the formatter from turning this entire method into an ugly ternary statement.
        // Like something Disney would make.
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

    public enum ScriptPushResult
    {
        NoAdbDevice,
        Success,
        NoScriptResources,
        ScriptsUpToDate,
    }
}