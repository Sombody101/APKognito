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

        try
        {
            await ModifyArchiveStringsInternalAsync(archivePath, pattern, replacementValue, extraFiles, token);
        }
        finally
        {
            _reporter.Clear();
        }
    }

    private async Task ModifyArchiveStringsInternalAsync(string archivePath, Regex pattern, string replacement, string[] extraFiles, CancellationToken token)
    {
        _reporter.ReportProgressTitle("Renaming OBB internal");

        // Not really indexing, but it sounds cooler :p
        _reporter.ReportProgressMessage("Indexing...");

        using ZipFile? zip = TryOpenZip(archivePath);

        if (zip is null)
        {
            return;
        }

        List<ZipEntry> selectedFiles = [.. zip.Entries.Where(e => e.FileName.Contains("catalog") || extraFiles.Contains(e.FileName))];

        int editCount = 0;
        foreach (ZipEntry entry in selectedFiles)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            _logger?.LogDebug("Renaming OBB entry '{FileName}'", entry.FileName);
            _reporter.ReportProgressMessage(entry.FileName);

            if (await ProcessTextEntryAsync(zip, entry, pattern, replacement, token))
            {
                editCount++;
            }

            _reporter.ReportProgressMessage("Searching...");
        }

        if (editCount is 0)
        {
            _logger?.LogInformation("No edits made.");
            return;
        }

        _logger?.LogInformation("Saving {Count} asset changes...", editCount);
        _reporter.ReportProgress("Saving OBB", "Saving ", editCount.ToString(), " asset changes...");

        zip.Save();
    }

    private static async Task<bool> ProcessTextEntryAsync(ZipFile zip, ZipEntry entry, Regex pattern, string replacement, CancellationToken token)
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

        if (!ReferenceEquals(originalContent, updatedContent))
        {
            zip.RemoveEntry(entry.FileName);
            _ = zip.AddEntry(entry.FileName, updatedContent);
            return true;
        }

        return false;
    }

    private ZipFile? TryOpenZip(string archivePath)
    {
        FileStream stream = File.Open(archivePath, FileMode.Open, FileAccess.ReadWrite);

        if (!ZipFile.IsZipFile(stream, false))
        {
            _logger.LogWarning("The file '{File}' is not an archive. No edits will be made.", Path.GetFileName(archivePath));
            stream.Dispose();
            return null;
        }

        stream.Position = 0;
        return ZipFile.Read(stream);
    }
}

public class NonexistentAssetException(string assetName, string assetDirectory)
    : Exception($"The asset '{assetName}' could not be found in the selected asset directory: {assetDirectory}")
{
}
