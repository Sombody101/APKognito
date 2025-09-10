using System.Text;
using System.Text.RegularExpressions;
using Ionic.Zip;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Utilities;

internal sealed class ArchiveReplace(IProgress<ProgressInfo>? _reporter, ILogger _logger)
{
    public async Task ModifyArchiveStringsAsync(string archivePath, Regex pattern, string replacementValue, string[]? extraFiles, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementValue);

        if (!File.Exists(archivePath))
        {
            throw new NonexistentAssetException(Path.GetFileName(archivePath), Path.GetDirectoryName(archivePath) ?? "[NO_DIRECTORY]");
        }

        extraFiles ??= [];

        await ModifyArchiveStringsInternalAsync(archivePath, pattern, replacementValue, extraFiles, token);
    }

    private async Task ModifyArchiveStringsInternalAsync(string archivePath, Regex pattern, string replacement, string[] extraFiles, CancellationToken token)
    {
        _reporter.ReportProgressTitle("Renaming OBB internal");

        // Not really indexing, but it sounds cooler :p
        _reporter.ReportProgressMessage("Indexing...");

        using ZipFile zip = new(archivePath);

        List<ZipEntry> selectedFiles = [.. zip.Entries.Where(e => e.FileName.Contains("catalog") || extraFiles.Contains(e.FileName))];

        foreach (ZipEntry entry in selectedFiles)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            _logger?.LogDebug("Renaming OBB entry '{FileName}'", entry.FileName);
            _reporter.ReportProgressMessage(entry.FileName);

            await ProcessTextEntryAsync(zip, entry, pattern, replacement, token);

            _reporter.ReportProgressMessage("Searching...");
        }

        _logger?.LogInformation("Saving asset changes...");

        // Not great for the thread pool, but it will lock the UI
        await Task.Run(zip.Save, token);
    }

    private static async Task ProcessTextEntryAsync(ZipFile zip, ZipEntry entry, Regex pattern, string replacement, CancellationToken token)
    {
        string originalContent = string.Empty;

        using (MemoryStream ms = new())
        {
            entry.Extract(ms);
            ms.Position = 0;
            using StreamReader reader = new(ms, Encoding.UTF8);
            originalContent = await reader.ReadToEndAsync(token);
        }

        string updatedContent = pattern.Replace(originalContent, replacement);

        zip.RemoveEntry(entry.FileName);
        _ = zip.AddEntry(entry.FileName, updatedContent);
    }
}

public class NonexistentAssetException(string assetName, string assetDirectory)
    : Exception($"The asset '{assetName}' could not be found in the selected asset directory: {assetDirectory}")
{
}
