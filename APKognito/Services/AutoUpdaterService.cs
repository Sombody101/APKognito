﻿using APKognito.Configurations;
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
    public static readonly string UpdatesFolder = Path.Combine(App.AppData!.FullName, "updates");

    private const string releaseUrl = "https://api.github.com/repos/Sombody101/APKognito/releases";
    private const int latest = 0;

    private readonly UpdateConfig config;
    private readonly CacheStorage cache;
    private readonly Version currentVersion;

    private Timer? _timer = null;

    public AutoUpdaterService(ConfigurationFactory factory)
    {
        config = factory.GetConfig<UpdateConfig>();
        cache = factory.GetConfig<CacheStorage>();
        currentVersion = Assembly.GetExecutingAssembly().GetName().Version!;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        FileLogger.Log($"Update service starting. (@{config.CheckDelay}m)");

        // Update cleanup
        if (App.RestartedFromUpdate)
        {
            FileLogger.Log("Clearing old update files.");

            try
            {
                //Directory.Delete(AutoUpdaterService.UpdatesFolder, true);
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
        FileLogger.Log("Update service exiting.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellation)
    {
        if (inUpdate)
        {
            // Controlled by ImplementUpdate.
            // If true, a request for a previous update that was ignored is active.
            return;
        }

        if (!config.CheckForUpdates)
        {
            FileLogger.Log("Check for updates disabled. Skipping.");
            goto LogUpdate;
        }

        string?[] jsonData = await Installer.FetchAsync(releaseUrl, [
            [latest, "tag_name"],
            [latest, "assets", 0, "browser_download_url"],
        ]);

        // Only accept debug releases for debug builds, public releases for release builds
        if (!jsonData[0]!.StartsWith(App.IsDebugRelease ? 'd' : 'v'))
        {
            FileLogger.Log($"Most recent release isn't public: {jsonData[0]}");
            goto LogUpdate;
        }

        if (!Version.TryParse( /* Remove the 'v' (v.1.5.1.1) */ jsonData[0]?[1..], out Version? newVersion))
        {
            // The version isn't viable
            FileLogger.LogError($"Aborting update, invalid release tag: {jsonData[0]}");
            goto LogUpdate;
        }

        if (newVersion == currentVersion)
        {
            FileLogger.Log("Currently using newest release.");
        }
        else if (newVersion < currentVersion)
        {
            FileLogger.LogWarning($"Found new release version {jsonData[0]}, but currently using v{currentVersion}. No need to update.");
            goto LogUpdate;
        }
        else if (cache.UpdateSourceLocation is not null)
        {
            FileLogger.Log("Comparing fetched release with fetch from previous session.");

            string[] updateInfo = cache.UpdateSourceLocation.Split('\0');

            if (!File.Exists(updateInfo[0]))
            {
                FileLogger.LogError("Previously downloaded update files are gone, proceeding with new release fetch.");
                goto FetchNew;
            }

            if (updateInfo.Length is 2
                && Version.TryParse(updateInfo[1][1..], out Version? lastVersion)
                && lastVersion >= newVersion)
            {
                FileLogger.Log($"Installing previous session update ({lastVersion} >= {newVersion})");
                await ImplementUpdate(updateInfo[0]);
                return;
            }
            else
            {
                FileLogger.Log("Proceeding with new release fetch.");
            }
        }

    FetchNew:
        FileLogger.Log($"Downloading release {jsonData[0]}");

        _ = Directory.CreateDirectory(UpdatesFolder);
        string downloadZip = Path.Combine(UpdatesFolder, $"APKognito-{jsonData[0]![1..]}.zip");
        if (!await Installer.DownloadAsync(jsonData[1]!, downloadZip))
        {
            goto LogUpdate;
        }

        cache.UpdateSourceLocation = $"{downloadZip}\0{jsonData[0]}";

        await ImplementUpdate(downloadZip);

    LogUpdate:
        LogNextUpdate(config.CheckDelay);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private static bool inUpdate = false;
    private async Task ImplementUpdate(string updateFilePath)
    {
        if (inUpdate)
        {
            return;
        }

        inUpdate = true;

        while (HomeViewModel.Instance?.RunningJobs is true)
        {
            // Wait for any jobs to finish, then prompt
            await Task.Delay(1000);
        }

        // Never in my life did I think "Yes. I'll just 'await await, await' it".
        var result = await await App.Current.Dispatcher.InvokeAsync(async () =>
        {
            return await new MessageBox()
            {
                Title = "Update ready!",
                Content = "An update has been downloaded and is ready to install. Would you like to do it now? (Requires restart)",
                PrimaryButtonText = "Update and Restart"
            }.ShowDialogAsync();
        });

        if (result is not MessageBoxResult.Primary)
        {
            FileLogger.Log("User denied update installation.");
            goto ContinueApp;
        }

        FileLogger.Log("User accepted update installation, unpacking then restarting.");

        ZipFile.ExtractToDirectory(updateFilePath, UpdatesFolder, true);
        string unpackedPath = Path.Combine(UpdatesFolder, Path.GetFileNameWithoutExtension(updateFilePath));

        const string script = "-c \"Start-Sleep -Seconds 2; Copy-Item -Verbose -Recurse -Path '{0}\\*' -Destination '{1}'; Start-Process -FilePath '{1}\\APKognito.exe' -Args '{2}'\"";

        string command = string.Format(script, unpackedPath, AppDomain.CurrentDomain.BaseDirectory, App.UpdateInstalledArgument);

        _ = Process.Start(new ProcessStartInfo()
        {
            Arguments = command,
            WindowStyle = ProcessWindowStyle.Maximized,
            CreateNoWindow = false,
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        Environment.Exit(0);

    ContinueApp:
        inUpdate = false;
    }

    private static void LogNextUpdate(int minuteCount)
    {
        FileLogger.Log($"Next update will be at {DateTime.UtcNow.AddMinutes(minuteCount)}");
    }
}