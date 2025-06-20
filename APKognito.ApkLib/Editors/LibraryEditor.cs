using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Exceptions;
using APKognito.ApkLib.Interfaces;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

public sealed class LibraryEditor : Additionals<LibraryEditor>,
    IReportable<LibraryEditor>
{
    private readonly LibraryRenameConfiguration _renameConfiguration;
    private readonly ILogger _logger;
    private readonly PackageNameData? _nameData;

    private IProgress<ProgressInfo>? _reporter;

    public LibraryEditor(LibraryRenameConfiguration renameConfiguration, PackageNameData? nameData)
        : this(renameConfiguration, nameData, null)
    {
    }

    public LibraryEditor(LibraryRenameConfiguration renameConfiguration, PackageNameData? nameData, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(renameConfiguration);

        _renameConfiguration = renameConfiguration;
        _nameData = nameData;
        _logger = MockLogger.MockIfNull(logger);
    }

    public LibraryEditor SetReporter(IProgress<ProgressInfo>? reporter)
    {
        _reporter = reporter;
        return this;
    }

    public async Task RunAsync(string? libraryDirectory = null, CancellationToken token = default)
    {
        InvalidConfigurationException.ThrowIfNull(_renameConfiguration);
        InvalidConfigurationException.ThrowIfNull(_nameData);

        libraryDirectory ??= Path.Combine(_nameData!.ApkAssemblyDirectory, "lib");

        if (!_renameConfiguration.EnableLibraryRenaming && !_renameConfiguration.EnableLibraryFileRenaming)
        {
            // Early return
            _logger.LogInformation("Skipping binaries.");
            return;
        }

        if (!Directory.Exists(libraryDirectory))
        {
            _logger.LogInformation("No libs found. Not renaming binaries.");
            return;
        }

        _logger.LogInformation("Renaming libraries.");

        _reporter.ReportProgressTitle("Renaming libraries");

        IEnumerable<string> libraries = GetLibraryPaths(libraryDirectory);

        foreach (string library in libraries)
        {
            // Rename internally, then rename the file.
            // This was done in reverse in APKognito, but it's cleaner this way.

            if (_renameConfiguration.EnableLibraryRenaming)
            {
                await RenameLibraryTablesAsync(library, token);
            }

            if (_renameConfiguration.EnableLibraryFileRenaming)
            {
                RenameLibraryFile(library);
            }
        }
    }

    /// <summary>
    /// Renames a given library file with the <see cref="LibraryRenameConfiguration.re"/>
    /// </summary>
    public void RenameLibraryFile(string libraryPath)
    {
        InvalidConfigurationException.ThrowIfNull(_nameData);

        string originalName = Path.GetFileName(libraryPath);
        string newFileName = _renameConfiguration.BuildAndCacheRegex(_nameData.OriginalCompanyName)
            .Replace(originalName, _nameData.NewCompanyName);

        string newFilePath = Path.Combine(Path.GetDirectoryName(libraryPath)!, newFileName);

        string formattedOptionalReplacement = originalName != newFileName
            ? $"{_renameConfiguration.InternalRenameInfoLogDelimiter}{newFileName}"
            : string.Empty;

        // The actual rename action has to be deferred to prevent access exceptions :p
        _logger.LogInformation("Renaming lib file: {OriginalName}{FormattedOptionalReplacement}", originalName, formattedOptionalReplacement);
        File.Move(libraryPath, newFilePath);
    }

    /// <summary>
    /// Runs the 
    /// </summary>
    public async Task RenameLibraryTablesAsync(string libraryPath, CancellationToken token = default)
    {
        InvalidConfigurationException.ThrowIfNull(_nameData);

        using IDisposable? scope = _logger.BeginScope('\t');

        var binaryReplace = new BinaryReplace(libraryPath, _reporter, _logger);
        await binaryReplace.ModifyElfStringsAsync(
            _renameConfiguration.BuildAndCacheRegex(_nameData.OriginalCompanyName),
            _nameData.NewCompanyName,
            token
        );
    }

    public IEnumerable<string> GetLibraryPaths(string libraryDirectory)
    {
        return Directory.EnumerateFiles(libraryDirectory, "*.so", SearchOption.AllDirectories)
            .Concat(_renameConfiguration.ExtraInternalPackagePaths
                .Where(p => p.FileType is FileType.Elf)
                .Select(p => Path.Combine(_nameData!.ApkAssemblyDirectory, p.FilePath)))
            .Where(p => !p.EndsWith(".config.so") && !p.Equals("libscript.so")) // These are usually a Json or JS file, not an ELF.
            .FilterByAdditions(Inclusions, Exclusions);
    }
}
