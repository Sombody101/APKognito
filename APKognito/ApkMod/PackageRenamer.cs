using System.Diagnostics;
using System.IO;
using System.Text;
using APKognito.ApkLib;
using APKognito.ApkLib.Automation;
using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Editors;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkMod;

public sealed class PackageRenamer
{
    private readonly ApkRenameSettings _renameSettings;
    private readonly AdvancedApkRenameSettings _advRenameSettings;
    private readonly ILogger _logger;
    private readonly IProgress<ProgressInfo> _reporter;

    public PackageRenamer(ApkRenameSettings renameSettings, AdvancedApkRenameSettings advRenameSettings, ILogger logger, IProgress<ProgressInfo> reporter)
    {
        ArgumentNullException.ThrowIfNull(renameSettings);
        ArgumentNullException.ThrowIfNull(advRenameSettings);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(reporter);

        _renameSettings = renameSettings;
        _advRenameSettings = advRenameSettings;
        _logger = logger;
        _reporter = reporter;
    }

    public async Task<PackageRenameResult> RenamePackageAsync(PackageToolingPaths toolingPaths, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(toolingPaths);

        PackageRenameConfiguration renameConfig = MapRenameSettings(_renameSettings, _advRenameSettings);

        string sourceApkName = Path.GetFileName(_renameSettings.SourceApkPath);
        if (sourceApkName.EndsWith(".apk"))
        {
            sourceApkName = sourceApkName[..^4];
        }

        PackageNameData nameData = new()
        {
            ApkAssemblyDirectory = _renameSettings.TempDirectory,
            ApkSmaliTempDirectory = Path.Combine(Path.GetDirectoryName(_renameSettings.TempDirectory)!, "$smali"),
            FullSourceApkPath = _renameSettings.SourceApkPath,
            FullSourceApkFileName = sourceApkName,
            NewCompanyName = _renameSettings.ApkReplacementName,
            RenamedPackageOutputBaseDirectory = _renameSettings.OutputBaseDirectory
        };

        try
        {
            _reporter.Report(new(string.Empty, ProgressUpdateType.Title));
            _reporter.Report(new(string.Empty, ProgressUpdateType.Content));

            PackageEditorContext context = new PackageEditorContext(renameConfig, nameData, toolingPaths, _logger)
                .SetReporter(_reporter);

            /* Unpack */

            PackageCompressor compressor = context.CreatePackageCompressor();
            await TimeAsync(async () =>
            {
                await compressor.UnpackPackageAsync(token: token);
                compressor.GatherPackageMetadata();
            }, nameof(compressor.UnpackPackageAsync));

            AutoConfigModel? automationConfig = _renameSettings.AutoPackageEnabled
                ? await GetParsedAutoConfigAsync(_renameSettings.AutoPackageConfig)
                : null;

            _ = await GetCommandResultAsync(automationConfig, CommandStage.Unpack, nameData);

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

            /* Assets */

            string? outputAssetDirectory = null;
            await TimeAsync(async () =>
            {
                AssetEditor assetEditor = context.CreateAssetEditor()
                    .WithStageResult(await GetCommandResultAsync(automationConfig, CommandStage.Assets, nameData));
                outputAssetDirectory = await assetEditor.RunAsync(token: token);
            }, nameof(AssetEditor));

            _ = await GetCommandResultAsync(automationConfig, CommandStage.Pack, nameData);

            /* Pack and Sign */

            await TimeAsync(async () => await compressor.PackPackageAsync(token: token), nameof(compressor.PackPackageAsync));
            await TimeAsync(async () => await compressor.SignPackageAsync(token: token), nameof(compressor.SignPackageAsync));

            /* Finalize and return paths */

            string outputPackagePath = Path.Combine(nameData.FinalOutputDirectory, $"{nameData.NewPackageName}.apk");

            return new PackageRenameResult()
            {
                Successful = true,
                OutputLocations = new(outputPackagePath, outputAssetDirectory, nameData.NewPackageName)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename {FullSourceApkFileName}.", nameData.FullSourceApkFileName);
            FileLogger.LogException($"Failed to rename {nameData.FullSourceApkFileName}", ex);

            return new PackageRenameResult()
            {
                OutputLocations = new(string.Empty, null, string.Empty),
                ResultStatus = ex.Message
            };
        }
    }

    private static PackageRenameConfiguration MapRenameSettings(ApkRenameSettings settings, AdvancedApkRenameSettings advancedSettings)
    {
        string assetDirectory = Path.Combine(
            Path.GetDirectoryName(settings.SourceApkPath)!,
            Path.GetFileNameWithoutExtension(settings.SourceApkPath)
        );

        return new()
        {
            ClearTempFilesOnRename = settings.ClearTempFilesOnRename,
            RenameRegex = advancedSettings.PackageReplaceRegexString,
            DirectoryRenameConfiguration = new()
            {
                // BaseDirectory = string.Empty
            },
            LibraryRenameConfiguration = new()
            {
                EnableLibraryFileRenaming = advancedSettings.RenameLibs,
                EnableLibraryRenaming = advancedSettings.RenameLibsInternal,
                ExtraInternalPackagePaths = advancedSettings.ExtraInternalPackagePaths
            },
            SmaliRenameConfiguration = new()
            {
                ExtraInternalPackagePaths = advancedSettings.ExtraInternalPackagePaths
            },
            AssetRenameConfiguration = new()
            {
                AssetDirectory = assetDirectory,
                CopyAssets = settings.CopyFilesWhenRenaming,
                RenameObbArchiveEntries = advancedSettings.RenameObbsInternal,
                ExtraInternalPackagePaths = [.. advancedSettings.RenameObbsInternalExtras],
            }
        };
    }

    private async Task<AutoConfigModel?> GetParsedAutoConfigAsync(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            _logger.LogInformation("Found auto config is null or empty (won't be parsed).");
            return null;
        }

        using MemoryStream configStream = new(Encoding.Default.GetBytes(config));
        using StreamReader streamReader = new(configStream);

        // We all know damn well this is not compiling, but "parsing" didn't sound as cool :p
        _logger.LogInformation("Compiling auto configuration...");

        var parser = new ConfigParser(streamReader);
        return await parser.BeginParseAsync();
    }

    private async Task<CommandStageResult?> GetCommandResultAsync(AutoConfigModel? config, CommandStage stage, PackageNameData nameData)
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

    private static async Task TimeAsync(Func<Task> action, string? tag = "Action")
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            FileLogger.Log($"--- {tag}: Start");
            await action();
        }
        finally
        {
            sw.Stop();
            FileLogger.Log($"--- {tag}: {sw}");
        }
    }
}
