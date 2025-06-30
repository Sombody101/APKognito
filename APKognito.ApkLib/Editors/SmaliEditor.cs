#if DEBUG
#define ASYNC_RENAME_DISABLED
#endif

using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Exceptions;
using APKognito.ApkLib.Interfaces;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

// TODO: Port SmaliEditor from legacy ApkLib
public sealed class SmaliEditor : Additionals<SmaliEditor>,
    IReportable<SmaliEditor>
{
    private readonly ILogger _logger;
    private readonly SmaliRenameConfiguration _renameConfiguration;
    private readonly PackageNameData? _nameData;

    private IProgress<ProgressInfo>? _reporter;


    public SmaliEditor(SmaliRenameConfiguration renameConfig, PackageNameData? nameData)
        : this(renameConfig, nameData, null)
    {
    }

    public SmaliEditor(SmaliRenameConfiguration renameConfig, PackageNameData? nameData, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(renameConfig);

        _renameConfiguration = renameConfig;
        _nameData = nameData;
        _logger = MockLogger.MockIfNull(logger);
    }

    public async Task RunAsync(CancellationToken token = default)
    {
        InvalidConfigurationException.ThrowIfNull(_nameData);

        Directory.CreateDirectory(_nameData.ApkSmaliTempDirectory);

        int workingOnFile = 0;

        IEnumerable<string> renameFiles = GetSmaliFiles();

#if DEBUG
        _logger.LogDebug("Beginning threaded rename on {Count:n0} smali files.", renameFiles.Count());
#else
        _logger.LogInformation("Renaming Smali files.");
#endif

        _reporter.ReportProgressTitle("Renaming file");

#if ASYNC_RENAME_DISABLED
        foreach (string file in renameFiles)
        {
            workingOnFile++;
            _reporter.ReportProgressMessage(workingOnFile.ToString());

            await ReplaceTextInFileAsync(file, token);
        }
#else
        object lockobj = new();
        await Parallel.ForEachAsync(renameFiles, token,
            async (filePath, subcToken) =>
            {
                Interlocked.Increment(ref workingOnFile);

                if (Monitor.TryEnter(lockobj))
                {
                    _reporter.ReportProgressMessage(workingOnFile.ToString());
                }

                await ReplaceTextInFileAsync(filePath, subcToken);
            }
        );
#endif
    }

    /// <inheritdoc/>
    public SmaliEditor SetReporter(IProgress<ProgressInfo>? reporter)
    {
        _reporter = reporter;
        return this;
    }

    private IEnumerable<string> GetSmaliFiles()
    {
        string smaliDirectory = Path.Combine(_nameData!.ApkAssemblyDirectory, "smali");
        IEnumerable<string> renameFiles = Directory.EnumerateFiles(smaliDirectory, "*.smali", SearchOption.AllDirectories)
            .Append($"{_nameData.ApkAssemblyDirectory}\\AndroidManifest.xml")
            .Append($"{_nameData.ApkAssemblyDirectory}\\apktool.yml")
            .FilterByAdditions(Inclusions, Exclusions);

        foreach (string directory in Directory.GetDirectories(_nameData.ApkAssemblyDirectory, "smali_*"))
        {
            renameFiles = renameFiles.Concat(Directory.EnumerateFiles(directory, "*.smali", SearchOption.AllDirectories));
        }

        string libDirectory = Path.Combine(_nameData.ApkAssemblyDirectory, "lib");
        if (Directory.Exists(libDirectory))
        {
            renameFiles = Directory.EnumerateFiles(libDirectory, "*.config.so", SearchOption.AllDirectories)
                .Concat(renameFiles);
        }

        return renameFiles.Concat(_renameConfiguration.ExtraInternalPackagePaths
            .Where(p => p.FileType is FileType.RegularText)
            .Select(p => Path.Combine(_nameData.ApkAssemblyDirectory, p.FilePath)));
    }

    private async Task ReplaceTextInFileAsync(string filePath, CancellationToken cToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Failed to find file {SubtractPathFrom}", PackageNameData.SubtractPathFrom(_nameData!.ApkAssemblyDirectory, filePath));
            return;
        }

        FileInfo fileInfo = new(filePath);

        if (fileInfo.Length < _renameConfiguration.MaxSmaliLoadSize)
        {
            string content = await File.ReadAllTextAsync(filePath, cToken);
            string newContent = Replace(content);
            await File.WriteAllTextAsync(filePath, newContent, cToken);
            return;
        }

        string tempSmaliFile = Path.Combine(_nameData!.ApkSmaliTempDirectory, $"${fileInfo.Name}_{Random.Shared.Next():x}");
        using StreamReader reader = new(fileInfo.FullName);
        using StreamWriter writer = new(tempSmaliFile);

        string? line;
        while ((line = await reader.ReadLineAsync(cToken)) is not null)
        {
            if (!string.IsNullOrEmpty(line)
                && line.Length >= _nameData!.OriginalCompanyName.Length
                && !line.StartsWith('#'))
            {
                line = Replace(line);
            }

            await writer.WriteLineAsync(line);
        }

        reader.Close();
        writer.Close();

        File.Delete(fileInfo.FullName);
        File.Move(tempSmaliFile, fileInfo.FullName);

        string Replace(string original)
        {
            return _renameConfiguration.BuildAndCacheRegex(_nameData!.OriginalCompanyName)
                .Replace(original, _nameData.NewCompanyName);
        }
    }
}
