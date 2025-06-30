using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Exceptions;
using APKognito.ApkLib.Interfaces;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

public sealed class DirectoryEditor : Additionals<DirectoryEditor>,
    IReportable<DirectoryEditor>
{
    private readonly ILogger _logger;
    private readonly DirectoryRenameConfiguration _renameConfig;
    private readonly PackageNameData? _nameData;

    private IProgress<ProgressInfo>? _reporter;

    public DirectoryEditor(DirectoryRenameConfiguration renameConfig, PackageNameData? nameData)
        : this(renameConfig, nameData, null)
    {
    }

    public DirectoryEditor(DirectoryRenameConfiguration renameConfig, PackageNameData? nameData, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(renameConfig);

        _renameConfig = renameConfig;
        _nameData = nameData;
        _logger = MockLogger.MockIfNull(logger);
    }

    public void RenameDirectory(string originalDirectory, string newName, string? baseDirectory = null)
    {
        if (Path.GetFileName(originalDirectory).Equals(newName))
        {
            // The name already matches
            return;
        }

        string trimmedDirectory = baseDirectory is not null && originalDirectory.Length > baseDirectory.Length
            ? $".{originalDirectory[baseDirectory.Length..]}"
            : originalDirectory;

        _logger.LogDebug("Changing {TrimmedDirectory}{RenameLogDelimiter}{NewName}", trimmedDirectory, _renameConfig.InternalRenameInfoLogDelimiter, newName);

        string newFolderPath = Path.Combine(Path.GetDirectoryName(originalDirectory)!, newName);

        if (Directory.Exists(newFolderPath))
        {
            _logger.LogInformation("The directory '{TrimmedDirectory}' has already been renamed to {NewName}, skipping.", trimmedDirectory, newName);
            return;
        }

        Directory.Move(originalDirectory, newFolderPath);
    }

    /// <summary>
    /// A high-level method primarily called by <see cref="PackageEditorContext.StartPackageRename"/>.
    /// </summary>
    /// <param name="originalCompanyName"></param>
    /// <param name="newCompanyName"></param>
    /// <param name="additionals"></param>
    public void ReplaceAllDirectoryNames(string? baseDirectory, string originalCompanyName, string newCompanyName)
    {
        _logger.LogInformation("Renaming Smali directories.");

        IEnumerable<string> ienDirs = Directory.GetDirectories(baseDirectory, $"*{originalCompanyName}*", SearchOption.AllDirectories)
            .FilterByAdditions(Inclusions, Exclusions)
            // Organize them to prevent "race conditions", which happens when a parent directory is renamed before a child directory, thereby throwing a DirectoryNotFoundException.
            .OrderByDescending(s => s.Length);

        _reporter.ReportProgressTitle("Renaming directory");

        foreach (string directory in ienDirs)
        {
            string directoryName = Path.GetFileName(directory);
            string adjustedDirectoryName = directoryName != originalCompanyName
                ? _renameConfig.BuildAndCacheRegex(originalCompanyName).Replace(directoryName, newCompanyName)
                : newCompanyName;

            _reporter.ReportProgressMessage(directoryName);
            RenameDirectory(directory, adjustedDirectoryName, _nameData.ApkAssemblyDirectory);
        }
    }

    public void Run(string? baseDirectory = null)
    {
        InvalidConfigurationException.ThrowIfNull(_nameData);

        ReplaceAllDirectoryNames(
            BaseRenameConfiguration.Coalesce(baseDirectory, _nameData.ApkAssemblyDirectory),
            _nameData.OriginalCompanyName,
            _nameData.NewCompanyName
        );
    }

    public DirectoryEditor SetReporter(IProgress<ProgressInfo>? reporter)
    {
        _reporter = reporter;
        return this;
    }

    public static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, bool recursive = false, IProgress<ProgressInfo>? reporter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDir);

        DirectoryInfo dir = new(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        _ = await Task.Run(() => Directory.CreateDirectory(destinationDir));

        List<FileInfo> files = await Task.Run(() => dir.GetFiles().ToList());

        IEnumerable<Task> copyTasks = files.Select(async file =>
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            using FileStream sourceStream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using FileStream destinationStream = new(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            await sourceStream.CopyToAsync(destinationStream);
        });

        await Task.WhenAll(copyTasks);

        if (recursive)
        {
            List<DirectoryInfo> subDirectories = await Task.Run(() => dir.GetDirectories().ToList());
            foreach (DirectoryInfo subDir in subDirectories)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir, true);
            }
        }
    }
}
