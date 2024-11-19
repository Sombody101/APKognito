// Makes the auto updater fetch a public release rather than a debug or bugfix release (if those are ever used)
// It's setup so that even if left defined will not break release builds.
// #define EMULATE_RELEASE_ON_DEBUG

using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using APKognito.ViewModels.Pages;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace APKognito.Services;

public sealed class AutoUpdaterService : IHostedService, IDisposable
{
    private const int LATEST = 0;

    public static readonly string UpdatesFolder = Path.Combine(App.AppData!.FullName, "updates");

    private readonly UpdateConfig config;
    private readonly CacheStorage cache;
    private readonly Version currentVersion;

    private Timer? _timer = null;

    public AutoUpdaterService()
    {
        config = ConfigurationFactory.GetConfig<UpdateConfig>();
        cache = ConfigurationFactory.GetConfig<CacheStorage>();
        currentVersion = Assembly.GetExecutingAssembly().GetName().Version!;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        FileLogger.Log($"Update service starting. (@{config.CheckDelay}m)");

        // Update cleanup
        if (MainOverride.RestartedFromUpdate)
        {
            FileLogger.Log("Clearing old update files.");

            try
            {
                Directory.Delete(UpdatesFolder, true);
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Failed to clear update files", ex);
            }

            cache.UpdateSourceLocation = null;
        }

        _timer = new Timer(
            async _ => await CheckForUpdatesAsync(cancellationToken),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(config.CheckDelay)
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = (_timer?.Change(Timeout.Infinite, 0));
        return Task.CompletedTask;
    }

    private async Task CheckForUpdatesAsync(CancellationToken cToken)
    {
        if (inUpdate)
        {
            // Controlled by ImplementUpdate.
            return;
        }

        if (!config.CheckForUpdates)
        {
            FileLogger.Log("Check for updates disabled. Skipping.");
            goto LogUpdateAndExit;
        }

        // Fetch the download URL and release tag
        string?[] jsonData = await Installer.FetchAsync(Constants.GITHUB_API_URL, HomeViewModel.Instance, cToken, [
            [LATEST, "tag_name"],
            [LATEST, "assets", 0, "browser_download_url"],
        ]);

        // Only accept debug releases for debug builds, public releases for release builds
#if DEBUG && EMULATE_RELEASE_ON_DEBUG
        if (!jsonData[0]!.StartsWith('v'))
#else
        if (!jsonData[0]!.StartsWith(App.IsDebugRelease ? 'd' : 'v'))
#endif
        {
#if RELEASE || EMULATE_RELEASE_ON_DEBUG
            FileLogger.Log($"Most recent release isn't a public build: {jsonData[0]}");
#else
            FileLogger.Log($"Most recent release isn't a debug build: {jsonData[0]}");
#endif

            goto LogUpdateAndExit;
        }

        // Check that the release tag is valid (for the current build)
        if (!Version.TryParse( /* Remove the prefix letter (v1.5.1.1) */ jsonData[0]?[1..], out Version? newVersion))
        {
            // The version isn't viable
            FileLogger.LogError($"Aborting update, invalid release tag: {jsonData[0]}");
            goto LogUpdateAndExit;
        }

        switch (await ValidatePackageVersion(currentVersion, jsonData))
        {
            case ValidationResult.UpdateComplete:
            case ValidationResult.CancelUpdate:
                goto LogUpdateAndExit;

            case ValidationResult.ContinueToUpdate:
                // Do nothing
                break;
        }

        FileLogger.Log($"Downloading release {jsonData[0]}");

        _ = Directory.CreateDirectory(UpdatesFolder);
        string downloadZip = Path.Combine(UpdatesFolder, $"APKognito-{jsonData[0]![1..]}.zip");
        if (!await Installer.DownloadAsync(jsonData[1]!, downloadZip, null, cToken))
        {
            FileLogger.LogFatal("Failed to download latest release.");
            goto LogUpdateAndExit;
        }

        // A cheap way to encode the update info in a way that the user won't dick around easily (they still can if they try hard enough, but a binary file should scare them away)
        cache.UpdateSourceLocation = $"{downloadZip}\0{jsonData[0]}";
        await ImplementUpdate(downloadZip, jsonData[0]!);

    LogUpdateAndExit:
        LogNextUpdate(config.CheckDelay);
    }

    private async Task<ValidationResult> ValidatePackageVersion(Version newVersion, string?[] jsonData)
    {
        // Already running the newest version
        if (newVersion == currentVersion)
        {
            FileLogger.Log("Currently using newest release.");
            return ValidationResult.CancelUpdate;
        }
        // New release is older than current (?)
        else if (newVersion < currentVersion)
        {
            FileLogger.Log($"Found new release version {jsonData[0]}, but currently using v{currentVersion}. No need to update.");
            return ValidationResult.CancelUpdate;
        }
        // New release has already been downloaded and is ready to install
        else if (cache.UpdateSourceLocation is not null)
        {
            FileLogger.Log("Comparing fetched release with fetch from previous session.");

            string[] updateInfo = cache.UpdateSourceLocation.Split('\0');

            if (!File.Exists(updateInfo[0]))
            {
                FileLogger.LogError("Previously downloaded update files are gone, proceeding with new release fetch.");
                return ValidationResult.ContinueToUpdate;
            }

            if (updateInfo.Length is 2
                && Version.TryParse(updateInfo[1][1..], out Version? lastVersion)
                && lastVersion >= newVersion)
            {
                FileLogger.Log($"Installing previous session update ({lastVersion} >= {newVersion})");
                await ImplementUpdate(updateInfo[0], updateInfo[1]);
                return ValidationResult.UpdateComplete;
            }
            else
            {
                FileLogger.Log("Proceeding with new release fetch.");
            }
        }

        return ValidationResult.ContinueToUpdate;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private bool inUpdate = false;
    private async Task ImplementUpdate(string updateFilePath, string newVersion)
    {
        if (inUpdate)
        {
            return;
        }

        inUpdate = true;

        // Using 'is true' because RunningJobs is a 'bool?' when using a null conditional
        while (HomeViewModel.Instance?.RunningJobs is true)
        {
            // Wait for any jobs to finish, then prompt
            await Task.Delay(1000);
        }

        // Never in my life did I think "Yes. I'll just 'await await, await' it".
        MessageBoxResult result = await await App.Current.Dispatcher.InvokeAsync(async () =>
        {
            // The update should take about 5 or 6 seconds, but tell the user 10 so they think it's faster than anticipated
            return await new MessageBox()
            {
                Title = "Update ready!",
                Content = $"An update has been downloaded and is ready to install!\n{newVersion[0]}{currentVersion} -> {newVersion}\nWould you like to do it now?\nAPKognito will restart itself when the install is complete (~10 seconds).",
                PrimaryButtonText = "Update",
                SecondaryButtonText = "Stop Updates",
                CloseButtonText = "Cancel",
                Width = 800,
            }.ShowDialogAsync();
        });

        switch (result)
        {
            case MessageBoxResult.Primary:
                // Proceed to update
                break;

            case MessageBoxResult.Secondary:
                // Cancel automatic updates
                config.CheckForUpdates = false;
                ConfigurationFactory.SaveConfig(config);
                return;

            default:
                FileLogger.Log("User denied update installation.");
                goto ContinueApp;
        }

        FileLogger.Log("User accepted update installation, unpacking then restarting.");

        string unpackedPath = Path.Combine(UpdatesFolder, Path.GetFileNameWithoutExtension(updateFilePath));

        ZipFile.ExtractToDirectory(updateFilePath, unpackedPath, true);

        const string script = "-c Start-Sleep -Seconds 5; Copy-Item -Recurse -Path '{0}\\*' -Destination '{1}'; Start-Process -FilePath '{1}APKognito.exe' -Args '{2}'";
        string command = string.Format(script, unpackedPath, AppDomain.CurrentDomain.BaseDirectory, MainOverride.UpdateInstalledArgument);

        _ = Process.Start(new ProcessStartInfo()
        {
            Arguments = command,
            // CreateNoWindow = true,
            FileName = Constants.POWERSHELL_PATH,
        });

        Environment.Exit(0);

    ContinueApp:
        inUpdate = false;
    }

    private static void LogNextUpdate(int minuteCount)
    {
        FileLogger.Log($"Next update check will be at {DateTime.UtcNow.AddMinutes(minuteCount)}");
    }

    private enum ValidationResult
    {
        CancelUpdate,
        UpdateComplete,
        ContinueToUpdate,
    }
}