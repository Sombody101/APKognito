#if DEBUG
// #define ASYNC_RENAME_DISABLED
#endif

using System.Runtime.CompilerServices;
using System.Text;
using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Exceptions;
using APKognito.ApkLib.Interfaces;
using APKognito.ApkLib.Utilities;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

public sealed class SmaliEditor : Additionals<SmaliEditor>
{
    private readonly SmaliRenameConfiguration _renameConfiguration;
    private readonly PackageRenameState _nameData;

    private readonly ILogger _logger;
    private readonly IProgress<ProgressInfo>? _reporter;

    public SmaliEditor(SmaliRenameConfiguration renameConfig, PackageRenameState nameData)
        : this(renameConfig, nameData, null, null)
    {
    }

    public SmaliEditor(SmaliRenameConfiguration renameConfig, PackageRenameState nameData, ILogger? logger, IProgress<ProgressInfo>? reporter)
    {
        ArgumentNullException.ThrowIfNull(renameConfig);

        _renameConfiguration = renameConfig;
        _nameData = nameData;
        _logger = MockLogger.MockIfNull(logger);
        _reporter = reporter;
    }

    public async Task RunAsync(IProgress<ProgressInfo>? reporter = null, CancellationToken token = default)
    {
        InvalidConfigurationException.ThrowIfNull(_nameData);
        Directory.CreateDirectory(_nameData.SmaliAssemblyDirectory);

#if DEBUG
#if ASYNC_RENAME_DISABLED
        _logger.LogDebug("Beginning single-threaded rename on smali files.");
#else
        _logger.LogDebug("Beginning multi-threaded rename on smali files.");
#endif
#else
        _logger.LogInformation("Renaming Smali files.");
#endif

        reporter ??= _reporter;

        reporter.Clear();
        reporter.ReportProgressTitle("Renaming file");

        int workingOnFile = 0;

#if ASYNC_RENAME_DISABLED
        var renameFiles = _renameConfiguration.ScanSmaliBeforeReplace
            ? GetSmaliFilesAsync(reporter, token)
            : GetSmaliFiles();

        foreach (string file in renameFiles)
        {
            workingOnFile++;
            _reporter.ReportProgressMessage(workingOnFile.ToString());

            await ReplaceTextInFileAsync(file, token);
        }
#else
        var progressChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();
        _ = Task.Run(async () =>
        {
            await foreach (string message in progressChannel.Reader.ReadAllAsync())
            {
                reporter.ReportProgressMessage(message);
            }
        }, token);

        await (_renameConfiguration.ScanSmaliBeforeReplace
            ? Parallel.ForEachAsync(GetSmaliFilesAsync(reporter, token), token, RenameFile)
            : Parallel.ForEachAsync(GetSmaliFiles(), token, RenameFile));

        async ValueTask RenameFile(string filePath, CancellationToken subcToken)
        {
            subcToken.ThrowIfCancellationRequested();

            int count = Interlocked.Increment(ref workingOnFile);
            await progressChannel.Writer.WriteAsync($"{count}: {Path.GetFileName(filePath)}", subcToken);

            await ReplaceTextInFileAsync(filePath, subcToken);
        }
#endif

        reporter.ReportProgressTitle("Finished");
        reporter.ReportProgressMessage("Finished");
    }

    private IEnumerable<string> GetSmaliFiles()
    {
        string apkAssemblyDirectory = _nameData.PackageAssemblyDirectory;

        IEnumerable<string> smaliFiles = Directory.EnumerateFiles(
            Path.Combine(apkAssemblyDirectory, "smali"),
            "*.smali",
            SearchOption.AllDirectories);

        IEnumerable<string> additionalSmaliFiles = Directory.EnumerateDirectories(apkAssemblyDirectory, "smali_*")
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.smali", SearchOption.AllDirectories));

        IEnumerable<string> allFiles = smaliFiles
            .Concat(additionalSmaliFiles)
            .Append(Path.Combine(apkAssemblyDirectory, "AndroidManifest.xml"))
            .Append(Path.Combine(apkAssemblyDirectory, "apktool.yml"));

        string libDirectory = Path.Combine(apkAssemblyDirectory, "lib");
        if (Directory.Exists(libDirectory))
        {
            allFiles = allFiles.Concat(
                Directory.EnumerateFiles(libDirectory, "*.config.so", SearchOption.AllDirectories));
        }

        IEnumerable<string> extraFiles = _renameConfiguration.ExtraInternalPackagePaths
            .Where(p => p.FileType is FileType.RegularText)
            .Select(p => Path.Combine(apkAssemblyDirectory, p.FilePath));

        allFiles = allFiles.Concat(extraFiles);

        return allFiles.FilterByAdditions(Inclusions, Exclusions);
    }

    public async IAsyncEnumerable<string> GetSmaliFilesAsync(IProgress<ProgressInfo>? reporter, [EnumeratorCancellation] CancellationToken token)
    {
        string[] smalidDirectories = Directory.GetDirectories(_nameData.PackageAssemblyDirectory, "smali_*");

        await foreach (string filePath in FunctionalGrep.FindFilesWithSubstringAsync(_nameData.OldCompanyName, smalidDirectories, reporter, token))
        {
            yield return filePath;
        }

        string libDirectory = Path.Combine(_nameData.PackageAssemblyDirectory, "lib");
        if (Directory.Exists(libDirectory))
        {
            foreach (string libFile in Directory.EnumerateFiles(libDirectory, "*.config.so", SearchOption.AllDirectories))
            {
                yield return libFile;
            }
        }

        yield return Path.Combine(_nameData.PackageAssemblyDirectory, "AndroidManifest.xml");
        yield return Path.Combine(_nameData.PackageAssemblyDirectory, "apktool.yml");

        foreach (string? extraPath in _renameConfiguration.ExtraInternalPackagePaths
            .Where(p => p.FileType is FileType.RegularText)
            .Select(p => Path.Combine(_nameData.PackageAssemblyDirectory, p.FilePath)))
        {
            yield return extraPath;
        }
    }

    private async Task ReplaceTextInFileAsync(string filePath, CancellationToken cToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Failed to find file {SubtractPathFrom}", PackageRenameState.SubtractPathFrom(_nameData.PackageAssemblyDirectory, filePath));
            return;
        }

        FileInfo fileInfo = new(filePath);

        if (fileInfo.Length < _renameConfiguration.MaxSmaliLoadSize)
        {
            await FullFileReplace(filePath, cToken);
        }

        await StreamFileReplace(fileInfo, cToken);

        async Task FullFileReplace(string filePath, CancellationToken cToken)
        {
            string content = await File.ReadAllTextAsync(filePath, cToken);

            // Might seem dumb/redundant, but it reduced driver call overhead by ~12% on large packages
            if (!content.Contains(_nameData.OldCompanyName))
            {
                return;
            }

            string newContent = Replace(content);
            await File.WriteAllTextAsync(filePath, newContent, cToken);
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)] // Hot path
        async Task StreamFileReplace(FileInfo fileInfo, CancellationToken cToken)
        {
            string tempSmaliFile = Path.Combine(_nameData.SmaliAssemblyDirectory, $"${fileInfo.Name}_{Random.Shared.Next():x}");
            using StreamReader reader = new(fileInfo.FullName, Encoding.Default, detectEncodingFromByteOrderMarks: true, bufferSize: _renameConfiguration.SmaliBufferSize);
            using StreamWriter writer = new(tempSmaliFile, append: false, reader.CurrentEncoding, _renameConfiguration.SmaliBufferSize);

            string? line;
            while ((line = await reader.ReadLineAsync(cToken)) is not null)
            {
                if (!string.IsNullOrEmpty(line)
                    && !line.StartsWith('#')
                    && line.Contains(_nameData.OldCompanyName))
                {
                    line = Replace(line);
                }

                await writer.WriteLineAsync(line);
            }

            reader.Close();
            writer.Close();

            File.Delete(fileInfo.FullName);
            File.Move(tempSmaliFile, fileInfo.FullName);
        }

        string Replace(string original)
        {
            return _renameConfiguration.BuildAndCacheRegex(_nameData.OldCompanyName)
                .Replace(original, _nameData.NewCompanyName);
        }
    }
}
