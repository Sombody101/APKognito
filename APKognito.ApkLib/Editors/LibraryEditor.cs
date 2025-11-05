using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Exceptions;
using APKognito.ApkLib.Interfaces;
using APKognito.ApkLib.Utilities;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

public sealed class LibraryEditor : Additionals<LibraryEditor>, IPackageEditor
{
    private readonly LibraryRenameConfiguration _renameConfiguration;
    private readonly PackageRenameState _nameData;

    private readonly ILogger _logger;
    private readonly IProgress<ProgressInfo>? _reporter;

    public LibraryEditor(LibraryRenameConfiguration renameConfiguration, PackageRenameState nameData)
        : this(renameConfiguration, nameData, null, null)
    {
    }

    public LibraryEditor(LibraryRenameConfiguration renameConfiguration, PackageRenameState nameData, ILogger? logger, IProgress<ProgressInfo>? reporter)
    {
        ArgumentNullException.ThrowIfNull(renameConfiguration);
        ArgumentNullException.ThrowIfNull(nameData);

        _renameConfiguration = renameConfiguration;
        _nameData = nameData;
        _logger = MockLogger.MockIfNull(logger);
        _reporter = reporter;
    }

    public Task ExecuteAsync(PackageRenameState state, BaseRenameConfiguration config, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public async Task RunAsync(string? libraryDirectory = null, CancellationToken token = default)
    {
        InvalidConfigurationException.ThrowIfNull(_renameConfiguration);

        libraryDirectory ??= Path.Combine(_nameData.PackageAssemblyDirectory, "lib");

        if (!_renameConfiguration.EnableLibraryRenaming && !_renameConfiguration.EnableLibraryFileRenaming)
        {
            _logger.LogInformation("Skipping binaries.");
            return;
        }

        if (!Directory.Exists(libraryDirectory))
        {
            _logger.LogInformation("No libs found. Not renaming binaries.");
            return;
        }

        if (_nameData.OldCompanyName.Length != _nameData.NewCompanyName.Length)
        {
            throw new InvalidOperationException("The package replacement name cannot be a different length of the original name.");
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
        string originalName = Path.GetFileName(libraryPath);
        string newFileName = _renameConfiguration.BuildAndCacheRegex(_nameData.OldCompanyName)
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
        using IDisposable? scope = _logger.BeginScope('\t');

        var binaryReplace = new BinaryReplace(libraryPath, _reporter, _logger);
        await binaryReplace.ModifyElfStringsAsync(
            _renameConfiguration.BuildAndCacheRegex(_nameData.OldCompanyName),
            _nameData.NewCompanyName,
            token
        );
    }

    public IEnumerable<string> GetLibraryPaths(string libraryDirectory)
    {
        return Directory.EnumerateFiles(libraryDirectory, "*.so", SearchOption.AllDirectories)
            .Concat(_renameConfiguration.ExtraInternalPackagePaths
                .Where(p => p.FileType is FileType.Elf)
                .Select(p => Path.Combine(_nameData.PackageAssemblyDirectory, p.FilePath)))
            .Where(p => !p.EndsWith(".config.so") && !p.Equals("libscript.so")) // These are usually a Json or JS file, not an ELF.
            .FilterByAdditions(Inclusions, Exclusions);
    }
}
