using System.Diagnostics;
using System.IO;
using APKognito.AdbTools;
using APKognito.ApkLib;
using APKognito.ApkLib.Automation;
using APKognito.ApkLib.Automation.Parser;
using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Editors;
using APKognito.ApkMod.Bootstrap;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Exceptions;
using APKognito.Helpers;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkMod;

public sealed class PackageRenamer
{
    private readonly IViewLogger _logger;
    private readonly IProgress<ProgressInfo> _reporter;
    private readonly ConfigurationFactory _configurationFactory;

    public PackageRenamer(
        ConfigurationFactory configFactory,
        IViewLogger logger,
        IProgress<ProgressInfo> reporter)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(reporter);

        _configurationFactory = configFactory;
        _logger = logger;
        _reporter = reporter;
    }

    public async Task<PackageRenameResult> RenamePackageAsync(
        RenameConfiguration modRenameConfiguration,
        bool pushAfterRename,
        CancellationToken token = default)
    {
        PackageRenameConfiguration renameConfig = MapRenameSettings(modRenameConfiguration);

        string sourceApkName = Path.GetFileName(modRenameConfiguration.SourcePackagePath);
        if (sourceApkName.EndsWith(".apk"))
        {
            sourceApkName = sourceApkName[..^4];
        }

        PackageNameData nameData = new()
        {
            ApkAssemblyDirectory = modRenameConfiguration.TempDirectory,
            ApkSmaliTempDirectory = Path.Combine(Path.GetDirectoryName(modRenameConfiguration.TempDirectory)!, "$smali"),
            FullSourceApkPath = modRenameConfiguration.SourcePackagePath,
            FullSourceApkFileName = sourceApkName,
            NewCompanyName = modRenameConfiguration.ReplacementCompanyName,
            RenamedPackageOutputBaseDirectory = modRenameConfiguration.OutputBaseDirectory
        };

        return await RunSafePackageRenameAsync(modRenameConfiguration, renameConfig, nameData, pushAfterRename, token);
    }

    public async Task SideloadPackageAsync(RenameOutputLocations locations, CancellationToken token = default)
    {
        await PushRenamedApkAsync(locations, token);
    }

    public async Task SideloadPackageAsync(string fullPackagePath, RenamedPackageMetadata metadata, CancellationToken token = default)
    {
        var locations = new RenameOutputLocations(
            fullPackagePath,
            metadata.RelativeAssetsPath is not null
                ? Path.Combine(Path.GetDirectoryName(fullPackagePath)!, metadata.RelativeAssetsPath)
                : null,
            metadata.PackageName
        );

        await PushRenamedApkAsync(locations, token);
    }

    private async Task<PackageRenameResult> RunSafePackageRenameAsync(
        RenameConfiguration modRenameConfig,
        PackageRenameConfiguration libRenameConfig,
        PackageNameData nameData,
        bool pushAfterRename,
        CancellationToken token)
    {
        try
        {
            PackageRenameResult result = await StartRenameInternalAsync(modRenameConfig, libRenameConfig, nameData, token);

            if (!result.Successful)
            {
                return result;
            }

            if (pushAfterRename)
            {
                await PushRenamedApkAsync(result.OutputLocations, token);
            }

            result.RenamedPackageMetadata = new()
            {
                PackageName = nameData.NewPackageName,
                OriginalPackageName = nameData.OriginalPackageName,
                RelativeAssetsPath = result.OutputLocations.AssetsDirectory is not null
                    ? Path.GetRelativePath(Path.GetDirectoryName(result.OutputLocations.OutputApkPath)!, result.OutputLocations.AssetsDirectory)
                    : null,
                RenameDate = DateTimeOffset.UtcNow,
                ApkognitoVersion = App.Version.GetVersion()
            };

            string claimFile = DirectoryManager.ClaimDirectory(Path.GetDirectoryName(result.OutputLocations.OutputApkPath)!);
            MetadataManager.WriteMetadata(claimFile, result.RenamedPackageMetadata);

            return result;
        }
        catch (TaskCanceledException tcex)
        {
            // Handle cancellation
            _logger.LogWarning(tcex, "Job canceled.");
            return new()
            {
                ResultStatus = "Job canceled.",
                Successful = false,
                OutputLocations = new(null!, null, string.Empty)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename failed.");
            return new()
            {
                ResultStatus = ex.Message,
                Successful = false,
                OutputLocations = new(null!, null, string.Empty)
            };
        }
    }

    private async Task<PackageRenameResult> StartRenameInternalAsync(
        RenameConfiguration modRenameConfig,
        PackageRenameConfiguration renameConfig,
        PackageNameData nameData,
        CancellationToken token)
    {
        _reporter.Report(new(string.Empty, ProgressUpdateType.Reset));

        AutoConfig? automationConfig = modRenameConfig.AdvancedConfig.AutoPackageEnabled
            ? GetParsedAutoConfigAsync(modRenameConfig.AdvancedConfig.AutoPackageConfig)
            : null;

        PackageEditorContext context = new PackageEditorContext(renameConfig, nameData, modRenameConfig.ToolingPaths, _logger)
            .SetReporter(_reporter);

        /* Unpack */

        PackageCompressor compressor = context.CreatePackageCompressor();
        await TimeAsync(async () =>
        {
            await compressor.UnpackPackageAsync(token: token);
            context.GatherPackageMetadata();
        }, nameof(compressor.UnpackPackageAsync));

        PackageBootstrapper bootstrapper = new(nameData.ApkAssemblyDirectory, new()
        {
            NewPackageName = "io.sombody101.movies",
        }, _logger);

        await TimeAsync(bootstrapper.RunAsync);

        _ = await GetCommandResultAsync(automationConfig, CommandStage.Unpack, nameData);

        _logger.LogInformation("Changing '{OriginalName}' |> '{NewName}'", nameData.OriginalPackageName, nameData.NewPackageName);
        goto StartPack;

        /* Directories */

        await TimeAsync(async () =>
        {
            context.CreateDirectoryEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Directory, nameData))
                .Run();
        }, nameof(DirectoryEditor));

        /* Libraries */

        await TimeAsync(async () =>
        {
            LibraryEditor libraryEditor = context.CreateLibraryEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Library, nameData));
            await libraryEditor.RunAsync(token: token);
        }, nameof(LibraryEditor));

        /* Smali */

        await TimeAsync(async () =>
        {
            SmaliEditor smaliEditor = context.CreateSmaliEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Smali, nameData));
            await smaliEditor.RunAsync(token: token);
        }, nameof(SmaliEditor));

    StartPack:

        /* Pack and Sign */

        await TimeAsync(async () => await compressor.PackPackageAsync(token: token), nameof(compressor.PackPackageAsync));
        await TimeAsync(async () => await compressor.SignPackageAsync(token: token), nameof(compressor.SignPackageAsync));

        /* Assets */

        string? outputAssetDirectory = null;
        await TimeAsync(async () =>
        {
            AssetEditor assetEditor = context.CreateAssetEditor()
                .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Assets, nameData));
            outputAssetDirectory = await assetEditor.RunAsync(token: token);
        }, nameof(AssetEditor));

        _ = await GetCommandResultAsync(automationConfig, CommandStage.Pack, nameData);

        /* Cleanup */

        await Task.Run(async () => await CleanUpTempsAsync(renameConfig, nameData, token), token);

        /* Finalize and return paths */

        string outputPackagePath = Path.Combine(nameData.FinalOutputDirectory, $"{nameData.NewPackageName}.apk");

        return new PackageRenameResult()
        {
            Successful = true,
            OutputLocations = new(outputPackagePath, outputAssetDirectory, nameData.NewPackageName)
        };
    }

    private static PackageRenameConfiguration MapRenameSettings(RenameConfiguration settings)
    {
        string assetDirectory = Path.Combine(
            Path.GetDirectoryName(settings.SourcePackagePath)!,
            Path.GetFileNameWithoutExtension(settings.SourcePackagePath)
        );

        return new()
        {
            // This will look weird if you're not using a font with ligatures.
            // Both my IDE and the main log output box use Fira Code, so it looks like an arrow.
            ReplacementInfoDelimiter = " |> ",
            ClearTempFilesOnRename = settings.UserRenameConfig.ClearTempFilesOnRename,
            RenameRegex = settings.AdvancedConfig.PackageReplaceRegexString,
            CompressorConfiguration = new()
            {
                ExtraJavaOptions = settings.AdvancedConfig.JavaFlags.Split().Where(s => !string.IsNullOrWhiteSpace(s))
            },
            DirectoryRenameConfiguration = new()
            {
            },
            LibraryRenameConfiguration = new()
            {
                EnableLibraryFileRenaming = settings.AdvancedConfig.RenameLibs,
                EnableLibraryRenaming = settings.AdvancedConfig.RenameLibsInternal,
                ExtraInternalPackagePaths = settings.AdvancedConfig.ExtraInternalPackagePaths,
            },
            SmaliRenameConfiguration = new()
            {
                ExtraInternalPackagePaths = settings.AdvancedConfig.ExtraInternalPackagePaths,
                SmaliBufferSize = settings.AdvancedConfig.SmaliBufferSize,
                MaxSmaliLoadSize = settings.AdvancedConfig.SmaliCutoffLimit,
            },
            AssetRenameConfiguration = new()
            {
                AssetDirectory = assetDirectory,
                CopyAssets = settings.UserRenameConfig.CopyFilesWhenRenaming,
                RenameObbArchiveEntries = settings.AdvancedConfig.RenameObbsInternal,
                ExtraInternalPackagePaths = [.. settings.AdvancedConfig.RenameObbsInternalExtras],
            }
        };
    }

    private AutoConfig? GetParsedAutoConfigAsync(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            _logger.LogInformation("Found auto config is null or empty (won't be parsed).");
            return null;
        }

        // We all know damn well this is not compiling, but "parsing" didn't sound as cool :p
        _logger.LogInformation("Compiling auto configuration...");

        return Tools.Time(() =>
        {
            return new AutoConfigParser(_logger).ParseDocument(config);
        });
    }

    private async Task<CommandStageResult?> GetCommandResultAsync(AutoConfig? config, CommandStage stage, PackageNameData nameData)
    {
        if (config is null)
        {
            return null;
        }

        RenameStage? foundStage = config.GetStage(stage);

        if (foundStage is null)
        {
            _logger.LogDebug("No stage found for {Stage}, no alterations made.", stage);
            return null;
        }

        _logger.LogInformation("-- Entering auto configuration script for stage {Stage}.", stage);

        try
        {
            Dictionary<string, string> variables = new()
            {
                { "originalCompany", nameData.OriginalCompanyName },
                { "originalPackage", nameData.OriginalPackageName },
                { "newCompany", nameData.NewCompanyName },
                { "newPackage", nameData.NewPackageName },
            };

            using IDisposable? scope = _logger.BeginScope("[SCRIPT]");
            return await new CommandDispatcher(foundStage, nameData.ApkAssemblyDirectory, variables, _logger)
                .DispatchCommandsAsync();
        }
        finally
        {
            _logger.LogInformation("-- Exiting auto configuration script for stage {Stage}.", stage);
        }
    }

    private async Task PushRenamedApkAsync(RenameOutputLocations locations, CancellationToken cancellationToken)
    {
        if (locations.OutputApkPath is null)
        {
            _logger.LogError("Renamed APK path is null. Cannot push to device.");
            return;
        }

        AdbDeviceInfo? currentDevice = _configurationFactory.GetConfig<AdbConfig>().GetCurrentDevice();

        if (currentDevice is null)
        {
            const string error = "Failed to get ADB device profile. Make sure your device is connected and selected in the Android Device menu";
            _logger.LogError(error);
            throw new AdbPushFailedException(Path.GetFileName(locations.NewPackageName), error);
        }

        FileInfo apkInfo = new(locations.OutputApkPath);

        if (string.IsNullOrWhiteSpace(locations.NewPackageName))
        {
            _logger.LogError("Failed to get new package name from location output data. Aborting package upload.");
            return;
        }

        _logger.Log($"Installing {locations.NewPackageName} to {currentDevice.DeviceId} ({GBConverter.FormatSizeFromBytes(apkInfo.Length)})");

        await AdbManager.WakeDeviceAsync();
        _ = await AdbManager.QuickDeviceCommandAsync(@$"install -g ""{apkInfo.FullName}""", token: cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(locations.AssetsDirectory))
        {
            if (!Directory.Exists(locations.AssetsDirectory))
            {
                _logger.LogError("Failed to find the assets directory at: {Path}", locations.AssetsDirectory);
                return;
            }

            await UploadPackageAssetsAsync(locations, currentDevice, cancellationToken);
        }

        _logger.Log($"Install complete.");
    }

    private async Task UploadPackageAssetsAsync(RenameOutputLocations locations, AdbDeviceInfo currentDevice, CancellationToken cancellationToken)
    {
        if (locations.AssetsDirectory is null)
        {
            _logger.Log("No assets to upload.");
            return;
        }

        string[] assets = Directory.GetFiles(locations.AssetsDirectory);

        string obbDirectory = $"{AdbManager.ANDROID_OBB}/{locations.NewPackageName}";
        _logger.Log($"Pushing {assets.Length} asset(s) to {currentDevice.DeviceId}: {obbDirectory}");

        _ = await AdbManager.QuickDeviceCommandAsync(@$"shell [ -d ""{obbDirectory}"" ] && rm -r ""{obbDirectory}""; mkdir ""{obbDirectory}""", token: cancellationToken);

        using IDisposable? scope = _logger.BeginScope("UPLD");

        int assetIndex = 0;
        foreach (string file in assets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var assetInfo = new FileInfo(file);
            _logger.Log($"Pushing [{++assetIndex}/{assets.Length}]: {assetInfo.Name} ({GBConverter.FormatSizeFromBytes(assetInfo.Length)})");

            _ = await AdbManager.QuickDeviceCommandAsync(@$"push ""{file}"" ""{obbDirectory}""", token: cancellationToken);
        }
    }

    private async Task CleanUpTempsAsync(PackageRenameConfiguration renameConfig, PackageNameData nameData, CancellationToken token = default)
    {
        _logger.LogInformation("Cleaning up...");

        if (renameConfig.ClearTempFilesOnRename)
        {
            _logger.LogDebug("Cleaning temp directory `{AssemblyDirectory}`", nameData.ApkAssemblyDirectory);
            await DirectoryManager.DeleteDirectoryAsync(nameData.ApkAssemblyDirectory, token);
        }

        if (renameConfig.AssetRenameConfiguration?.CopyAssets is false)
        {
            _logger.LogDebug("CopyWhenRenaming disabled, deleting directory `{FullSourceApkPath}`", nameData.FullSourceApkPath);

            try
            {
                File.Delete(nameData.FullSourceApkPath);

                string obbDirectory = Path.GetDirectoryName(nameData.FullSourceApkPath)
                    ?? throw new RenameFailedException("Failed to clean OBB directory ");

                await DirectoryManager.DeleteDirectoryAsync(obbDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear source APK.");
            }
        }
    }

    private async Task TimeAsync(Func<Task> action, string? tag = "Action")
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            FileLogger.Log($"--- {tag}: Start");
            await action();
            sw.Stop();
        }
        finally
        {
            sw.Stop();
            FileLogger.Log($"--- {tag}: {sw}");
            _logger.LogDebug($"{tag}: {sw}");
        }
    }
}
