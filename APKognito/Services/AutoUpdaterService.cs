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
    public static readonly string UpdatesFolder = Path.Combine(App.AppDataDirectory!.FullName, "updates");

    private readonly UpdateConfig config;
    private readonly CacheStorage cache;
    private readonly Version currentVersion;

    private Timer? _timer = null;

    public AutoUpdaterService()
    {
        config = ConfigurationFactory.Instance.GetConfig<UpdateConfig>();
        cache = ConfigurationFactory.Instance.GetConfig<CacheStorage>();
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
            async _ =>
            {
                await CheckForUpdatesAsync(cancellationToken);
                LogNextUpdate(config.CheckDelay);
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(config.CheckDelay)
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = _timer?.Change(Timeout.Infinite, 0);
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
            return;
        }

        // Fetch the download URL and release tag
        string?[] jsonData = await WebGet.FetchAsync(Constants.GITHUB_API_URL_LATEST, HomeViewModel.Instance, cToken, [
            ["tag_name"],
            ["assets", 0, "browser_download_url"],
        ]);

        if (jsonData.Length is 0)
        {
            FileLogger.LogError("Returned JSON release info is null. Aborting update check.");
            return;
        }

        string tagName = jsonData[0]!;
        string downloadUrl = jsonData[1]!;

        // Only accept debug releases for debug builds, public releases for release builds
#if DEBUG && EMULATE_RELEASE_ON_DEBUG
        if (!tagName!.StartsWith('v'))
        {
            FileLogger.Log($"Most recent release isn't a public build: {tagName}");
#else
        if (!tagName!.StartsWith(App.Version.VersionPrefix))
        {
            FileLogger.Log($"Most recent release isn't a {App.Version.VersionIdentifier} build: {tagName}");
#endif
            return;
        }

        // Check that the release tag is valid (for the current build)
        if (!Version.TryParse( /* Remove the prefix letter (v1.5.1.1) */ tagName[App.Version.VersionPrefix.Length..], out Version? newVersion))
        {
            // The version isn't viable
            FileLogger.LogError($"Aborting update, invalid release tag: {tagName}");
            return;
        }

        switch (await ValidatePackageVersionAsync(newVersion, jsonData))
        {
            case ValidationResult.UpdateComplete:
            case ValidationResult.CancelUpdate:
                return;

            case ValidationResult.ContinueToUpdate:
                // Do nothing
                break;
        }

        FileLogger.Log($"Downloading release {tagName}");

        _ = Directory.CreateDirectory(UpdatesFolder);
        string downloadZip = Path.Combine(UpdatesFolder, $"APKognito-{tagName}.zip");
        if (!await WebGet.DownloadAsync(downloadUrl!, downloadZip, null, cToken))
        {
            FileLogger.LogFatal("Failed to download latest release.");
            return;
        }

        // A cheap way to encode the update info in a way that the user won't dick around easily (they still can if they try hard enough, but a binary file should scare them away)
        cache.UpdateSourceLocation = $"{downloadZip}\0{tagName}";
        await ImplementUpdateAsync(downloadZip, tagName!);
    }

    private async Task<ValidationResult> ValidatePackageVersionAsync(Version newVersion, string?[] jsonData)
    {
        // Already running the newest version
        if (newVersion == currentVersion)
        {
            FileLogger.Log($"Currently using newest release. ({newVersion} == {currentVersion})");
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
            string updatePath = updateInfo[0];
            string updateVersion = updateInfo[1];

            if (!File.Exists(updatePath))
            {
                FileLogger.LogError("Previously downloaded update files are gone, proceeding with new release fetch.");
                return ValidationResult.ContinueToUpdate;
            }

            if (updateInfo.Length is 2
                && Version.TryParse(updateVersion[1..], out Version? lastVersion)
                && lastVersion >= newVersion)
            {
                FileLogger.Log($"Installing previous session update ({lastVersion} >= {newVersion})");
                await ImplementUpdateAsync(updatePath, updateVersion);
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
    private async Task ImplementUpdateAsync(string updateFilePath, string newVersion)
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
            return await new MessageBox()
            {
                Title = "Update Ready!",
                Content = $"An update has been downloaded and is ready to install.\n\n" +
                          $"Current Version: {App.Version.VersionPrefix}{currentVersion}\n" +
                          $"New Version:     {newVersion}\n\n" +
                          "Would you like to install it now?\n\n" +
                          "APKognito will restart automatically after the installation " +
                          "(approximately 10 seconds).",
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
                ConfigurationFactory.Instance.SaveConfig(config);
                return;

            default:
                FileLogger.Log("User denied update installation.");
                goto ContinueApp;
        }

        FileLogger.Log("User accepted update installation, unpacking then restarting.");

        string unpackedPath = Path.Combine(UpdatesFolder, Path.GetFileNameWithoutExtension(updateFilePath));

        ZipFile.ExtractToDirectory(updateFilePath, unpackedPath, true);

        const string script = "-c Write-Host 'Waiting for APKognito to exit...'; Start-Sleep -Seconds 5; " +
            "Write-Host 'Installing APKognito'; Copy-Item -Recurse -Path '{0}\\*' -Destination '{1}'; " +
            "Write-Host 'Starting APKognito!'; Start-Process -FilePath '{1}APKognito.exe' -Args '{2}'";
        string command = string.Format(script, unpackedPath, AppDomain.CurrentDomain.BaseDirectory, Constants.UpdateInstalledArgument);

        _ = Process.Start(new ProcessStartInfo()
        {
            Arguments = command,
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