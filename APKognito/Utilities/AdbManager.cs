using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace APKognito.Utilities;

internal class AdbManager
{
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
    /// Runs an ADB command.
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<string> QuickCommand(string arguments, CancellationToken token = default)
    {
        var adbProcess = CreateAdbProcess(null, arguments);
        adbProcess.Start();
        string output = await adbProcess.StandardOutput.ReadToEndAsync(token);
        string error = await adbProcess.StandardError.ReadToEndAsync(token);

        await adbProcess.WaitForExitAsync(token);

        if (adbProcess.ExitCode is not 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new Exception(error);
        }

        return output;
    }

    public static async Task<string> QuickDeviceCommand(string arguments, string? deviceId = null, CancellationToken token = default)
    {
        deviceId ??= adbConfig.CurrentDeviceId;
        return await QuickCommand($"-s {deviceId} {arguments}", token);
    }

    /// <summary>
    /// Gets a formatted list of all available ADB devices.
    /// </summary>
    /// <returns></returns>
    public static async Task<IEnumerable<string>> GetAllDevices()
    {
        string response = await QuickCommand("devices -l");

        return response.Split("\r\n")
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
    public static async Task<string[]> GetDeviceList()
    {
        string response = await QuickCommand("devices");

        return [.. response.Split("\r\n")
            .Where(str => !string.IsNullOrWhiteSpace(str))
            .Skip(1)
            .Select(str => str.Split()[0])];
    }

    /// <summary>
    /// Allows for quick command usage of any application.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<string> QuickGenericCommand(string command, string arguments)
    {
        var proc = new Process()
        {
            StartInfo =
            {
                FileName = command,
                Arguments = command ?? string.Empty,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            }
        };

        proc.Start();
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode is not 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new Exception(error);
        }

        return output;
    }

    public static async Task WakeDevice(string? deviceId = null)
    {
        await QuickDeviceCommand("shell input keyevent KEYCODE_WAKEUP", deviceId);
    }

    /// <summary>
    /// Restarts the ADB server (should only be used when ADB throws a 'No $ADB_VENDOR_KEYS' error.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task RestartAdbServer()
    {
        var adbRestartProcess = CreateAdbProcess(null, "restart-server");
        adbRestartProcess.Start();
        string error = await adbRestartProcess.StandardError.ReadToEndAsync();
        await adbRestartProcess.WaitForExitAsync();

        if (adbRestartProcess.ExitCode is not 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new Exception(error);
        }
    }

    public static bool AdbWorks([Optional] string? platformToolsPath)
    {
        platformToolsPath ??= adbConfig.PlatformToolsPath;

        return Directory.Exists(platformToolsPath) || File.Exists(Path.Combine(platformToolsPath, "adb.exe"));
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
}
