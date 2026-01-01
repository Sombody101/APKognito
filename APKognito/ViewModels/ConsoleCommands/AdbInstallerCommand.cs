using System.IO;
using System.IO.Compression;
using APKognito.AdbTools;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Windows;
using static APKognito.ViewModels.Pages.AdbConsoleViewModel;

namespace APKognito.ViewModels.ConsoleCommands;

internal static class AdbInstallerCommand
{
    [Command("install-adb", "Auto installs platform tools.", "[--force|-f]")]
    public static async Task InstallAdbCommandAsync(ParsedCommand ctx, IViewLogger logger, CancellationToken token)
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
}
