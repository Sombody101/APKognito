﻿using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Exceptions;
using APKognito.ApkLib.Interfaces;
using APKognito.ApkLib.Utilities;
using Ionic.Zip;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

public sealed class AssetEditor : Additionals<AssetEditor>, IReportable<AssetEditor>
{
    private readonly AssetRenameConfiguration _renameConfiguration;
    private readonly ILogger _logger;
    private readonly PackageNameData _nameData;

    private IProgress<ProgressInfo>? _reporter;

    public AssetEditor(AssetRenameConfiguration renameConfig, PackageNameData nameData)
        : this(renameConfig, nameData, null)
    {
    }

    public AssetEditor(AssetRenameConfiguration renameConfig, PackageNameData nameData, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(renameConfig);
        ArgumentNullException.ThrowIfNull(nameData);

        _renameConfiguration = renameConfig;
        _nameData = nameData;
        _logger = MockLogger.MockIfNull(logger);
    }

    public async Task RenameAssetAsync(string assetPath, string outputDirectory, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        await RenameAssetInternalAsync(assetPath, outputDirectory, _renameConfiguration.CopyAssets
            ? AssetTransferMode.Copy
            : AssetTransferMode.Move, token);
    }

    public async Task RenameAssetInternallyAsync(string assetPath, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        await RenameArchiveEntriesInternalAsync(assetPath, token);
    }

    public async Task CopyAssetsAsync(string? assetDirectory, string outputLocation)
    {
        await DirectoryEditor.CopyDirectoryAsync(
            BaseRenameConfiguration.Coalesce(assetDirectory, _renameConfiguration.AssetDirectory),
            outputLocation,
            true
        );
    }

    public void MoveAssets(string? assetsDirectory, string outputLocation)
    {
        // This relies on the user not using a separate drive for output files due to speed. Although it's very likely.
        Directory.Move(
            BaseRenameConfiguration.Coalesce(assetsDirectory, _renameConfiguration.AssetDirectory),
            outputLocation
        );
    }

    public async Task<string?> RunAsync(string? assetDirectory = null, string? outputDirectory = null, CancellationToken token = default)
    {
        InvalidConfigurationException.ThrowIfNullEmptyOrWhitespace(_nameData.RenamedOutputDirectoryInternal, "No set output renamed package output directory. Remember to run PackageCompressor.GatherPackageMetadata()!");

        return await RunInternalAsync(
            BaseRenameConfiguration.Coalesce(assetDirectory, _renameConfiguration.AssetDirectory),
            outputDirectory ?? Path.Combine(_nameData.OriginalPackageName, _nameData.RenamedOutputDirectoryInternal, _nameData.NewPackageName),
            token
        );
    }

    /// <inheritdoc/>
    public AssetEditor SetReporter(IProgress<ProgressInfo>? reporter)
    {
        _reporter = reporter;
        return this;
    }

    private async Task<string?> RunInternalAsync(string assetDirectory, string outputDirectory, CancellationToken token)
    {
        if (assetDirectory is null)
        {
            // Assume the asset directory is next to the package. Otherwise, this needs to be an overridden path.
            string parentDirectory = Path.GetDirectoryName(_nameData.FullSourceApkPath)!;
            assetDirectory = Path.Combine(parentDirectory, _nameData.OriginalPackageName);
        }

        if (!Directory.Exists(assetDirectory))
        {
            _logger.LogInformation("No assets directory found. Not renaming archives.");
            _logger.LogDebug("Attempted directory: {AssetDirectory}", assetDirectory);
            return null;
        }

        AssetTransferMode mode = _renameConfiguration.CopyAssets
            ? AssetTransferMode.Copy
            : AssetTransferMode.Move;

        Directory.CreateDirectory(outputDirectory);

        _logger.LogInformation("Renaming assets.");

        foreach (string assetPath in GetAssetPaths(assetDirectory))
        {
            if (!assetPath.EndsWith(".obb"))
            {
                string fileName = Path.GetFileName(assetPath);
                await MoveAssetAsync(assetPath, Path.Combine(outputDirectory, fileName), mode, token);
                continue;
            }

            await RenameAssetInternalAsync(assetPath, outputDirectory, mode, token);

            if (_renameConfiguration.RenameObbArchiveEntries)
            {
                await RenameArchiveEntriesInternalAsync(assetPath, token);
            }
        }

        return outputDirectory;
    }

    private async Task RenameAssetInternalAsync(string assetPath, string outputDirectory, AssetTransferMode mode, CancellationToken token)
    {
        _reporter.ReportProgressMessage(Path.GetFileName(assetPath));

        string newAssetName = Path.GetFileName(assetPath).Replace(_nameData.OriginalCompanyName, _nameData.NewCompanyName);

        _logger.LogInformation("Renaming asset file: {GetFileName}{InternalRenameInfoLogDelimiter}{NewAssetName} (Mode: {Mode})", Path.GetFileName(assetPath), _renameConfiguration.InternalRenameInfoLogDelimiter, newAssetName, mode);

        string assetOutputPath = Path.Combine(outputDirectory, newAssetName);
        await MoveAssetAsync(assetPath, assetOutputPath, mode, token);
    }

    private async ValueTask MoveAssetAsync(string assetPath, string targetDirectory, AssetTransferMode mode, CancellationToken token)
    {
        if (mode is AssetTransferMode.Copy)
        {
            await CopyLargeAsync(assetPath, targetDirectory, _renameConfiguration.AssetCopyBuffer, token);
        }
        else
        {
            File.Move(assetPath, targetDirectory);
        }
    }

    private async Task RenameArchiveEntriesInternalAsync(string assetPath, CancellationToken token)
    {
        try
        {
            var binaryReplace = new ArchiveReplace(_reporter, _logger);
            await binaryReplace.ModifyArchiveStringsAsync(
                assetPath,
                _renameConfiguration.BuildAndCacheRegex(_nameData.OriginalCompanyName),
                _nameData.NewCompanyName,
                [.. _renameConfiguration.RenameObbsInternalExtras],
                token
            );
        }
        catch (ZipException zex)
        {
            _logger.LogWarning(zex, "The file '{FileName}' is not an archive. Skipping.", Path.GetFileName(assetPath));
        }
    }

    private IEnumerable<string> GetAssetPaths(string assetDirectory)
    {
        return Directory.EnumerateFiles(assetDirectory)//, $"*{_nameData.FullSourceApkFileName}.obb")
            .Concat(_renameConfiguration.ExtraInternalPackagePaths)
            .FilterByAdditions(Inclusions, Exclusions);
    }

    private static async Task CopyLargeAsync(string sourceFile, string targetFile, int bufferSize, CancellationToken token)
    {
        using FileStream sourceStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
        using FileStream destinationStream = new(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);

        await sourceStream.CopyToAsync(destinationStream, token);
    }
}

public enum AssetTransferMode
{
    /// <summary>
    /// Copies the asset file when renaming. Can be undone by deleting the renamed file, but takes more space.
    /// </summary>
    Copy,

    /// <summary>
    /// Moves the asset file when renaming. Cannot be undone by ApkLib, but takes less space.
    /// </summary>
    Move,

    /// <summary>
    /// Renames the asset file during the upload stage. Uploading is not handled by ApkLib, and might be something implemented
    /// only on APKognito.
    /// </summary>
    //Defer,
}
