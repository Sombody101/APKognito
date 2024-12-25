using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;

namespace APKognito.Utilities;

internal class AdbManager
{
    private const string APKOGNITO_DIRECTORY = "/storage/emulated/0/apkognito";

    public Process? AdbProcess { get; private set; }

    public bool IsRunning => AdbProcess?.HasExited is false;

    private static readonly AdbConfig adbConfig = ConfigurationFactory.GetConfig<AdbConfig>();

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

        commandOutput.ThrowIfError(noThrow);

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

        commandOutput.ThrowIfError(noThrow);

        return commandOutput;
    }

    /// <summary>
    /// Gets a formatted list of all available ADB devices.
    /// </summary>
    /// <returns></returns>
    public static async Task<IEnumerable<string>> GetAllDevices(bool noThrow = false)
    {
        CommandOutput response = await QuickCommand("devices -l", noThrow: noThrow);

        return response.StdOut.Split("\r\n")
            // Trim empty lines
            .Where(str => !string.IsNullOrWhiteSpace(str))
            // Skip ADB list header
            .Skip(1)
            .Select(str =>
            {
                string[] split = [.. str.Split().Where(str => !string.IsNullOrWhiteSpace(str))];

                string deviceActivity = split[1];
                return deviceActivity switch
                {
                    "unauthorized" => $"{deviceActivity} - [ADB Not Enabled]",
                    "offline" => $"{deviceActivity} - [Device Offline]",
                    _ => $"{split[0]} - {split.First(str => str.StartsWith("model:"))[6..].Replace('_', ' ')}",
                };
            });
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

        output.ThrowIfError(noThrow);
    }

    public static bool AdbWorks([Optional] string? platformToolsPath)
    {
        platformToolsPath ??= adbConfig.PlatformToolsPath;

        return Directory.Exists(platformToolsPath) || File.Exists(Path.Combine(platformToolsPath, "adb.exe"));
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
        if (scriptVersion == App.GetVersion() || forceUpload)
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

        _ = await QuickDeviceCommand($"shell echo '#!/bin/sh\n\necho \"{App.GetVersion()}\"'");

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

/// <summary>
/// Holds references to the STDOUT and STDERR output of an ADB command.
/// </summary>
public readonly struct CommandOutput
{
    /// <summary>
    /// All text data from STDOUT of an ADB command.
    /// </summary>
    public readonly string StdOut { get; }

    /// <summary>
    /// All text data from STDERR of an ADB command.
    /// </summary>
    public readonly string StdErr { get; }

    public readonly bool Errored => !string.IsNullOrWhiteSpace(StdErr);

    public readonly void ThrowIfError(bool noThrow = false)
    {
        if (!noThrow && Errored)
        {
            throw new AdbCommandException(StdErr);
        }
    }

    public CommandOutput(string stdout, string stderr)
    {
        StdOut = stdout;
        StdErr = stderr;
    }

    public static async Task<CommandOutput> GetCommandOutput(Process proc)
    {
        return new(
            await proc.StandardOutput.ReadToEndAsync(),
            await proc.StandardError.ReadToEndAsync()
        );
    }

    public class AdbCommandException : Exception
    {
        public AdbCommandException(string error)
            : base(error)
        { }
    }
}