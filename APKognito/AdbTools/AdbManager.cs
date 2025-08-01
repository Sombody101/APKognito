﻿using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;

namespace APKognito.AdbTools;

internal sealed class AdbManager : IDisposable
{
    private const string APKOGNITO_DIRECTORY = $"{ANDROID_EMULATED}/apkognito";

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
    public static async Task<AdbCommandOutput> QuickCommandAsync(string arguments, bool noThrow = false, CancellationToken token = default)
    {
        using Process adbProcess = CreateAdbProcess(null, arguments);
        _ = adbProcess.Start();
        AdbCommandOutput commandOutput = await AdbCommandOutput.GetCommandOutputAsync(adbProcess);
        await adbProcess.WaitForExitAsync(token);

        if (!s_noCommandRecurse && commandOutput.StdErr.StartsWith("adb.exe: device unauthorized."))
        {
            LoggableObservableObject.CurrentLoggableObject?.SnackWarning("Command failed!", "An ADB command failed to execute! Running an ADB server restart... (may take some time).");
            _ = await QuickCommandAsync("kill-server", token: token);
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
    public static async Task<AdbDeviceInfo[]> GetAllDevicesAsync(bool noThrow = false)
    {
        AdbCommandOutput response = await QuickCommandAsync("devices -l", noThrow: noThrow);

        IEnumerable<Task<AdbDeviceInfo>> enumeration = response.StdOut.Split("\r\n")
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
                            AdbCommandOutput output = await QuickCommandAsync($"-s {deviceId} shell getprop ro.product.model");

                            if (!output.DeviceNotAuthorized)
                            {
                                device.DeviceName = output.StdOut.Trim();
                                device.DeviceAuthorized = true;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            FileLogger.LogException(ex);
                        }
                        goto case "unauthorized";
                }

                return device;
            });

        return await Task.WhenAll(enumeration);
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
        AdbCommandOutput output = await AdbCommandOutput.GetCommandOutputAsync(adbRestartProcess);
        await adbRestartProcess.WaitForExitAsync();

        output.ThrowIfError(noThrow, adbRestartProcess.ExitCode);
        return output;
    }

    public static async Task<AdbCommandOutput> KillAdbServerAsync(bool noThrow = false)
    {
        using Process adbKillProcess = CreateAdbProcess(null, "kill-server");
        _ = adbKillProcess.Start();
        AdbCommandOutput output = await AdbCommandOutput.GetCommandOutputAsync(adbKillProcess);
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

        ResourceSet? resources = AdbScripts.ResourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);

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
                AdbCommandOutput output = await QuickDeviceCommandAsync($"push \"{tempFile}\" \"{APKOGNITO_DIRECTORY}\"", noThrow: true);

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
