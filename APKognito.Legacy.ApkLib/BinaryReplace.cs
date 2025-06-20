using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Ionic.Zip;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace APKognito.Legacy.ApkLib;

internal class BinaryReplace
{
    private const int BUFFER_SIZE = 1024 * 1024;

    private readonly string binaryFilePath;
    private readonly ILogger? logger;
    private readonly Encoding encoding = Encoding.UTF8;

    private readonly IProgress<ProgressInfo>? progressReporter;

    public BinaryReplace(string filePath,
        IProgress<ProgressInfo>? progressReporter,
        ILogger? _logger
    )
    {
        binaryFilePath = filePath;
        logger = _logger;
        this.progressReporter = progressReporter;
    }

    public async Task ModifyElfStringsAsync(Regex pattern, string replacement, CancellationToken token)
    {
        string elfReadPath = $"{binaryFilePath}.apkspare";

        IELF elfFile = null!;

        try
        {
            File.Copy(binaryFilePath, elfReadPath, true);

            try
            {
                elfFile = ELFReader.Load(elfReadPath);
            }
            catch (ArgumentException aex)
            {
                logger?.LogWarning(aex, "Object file '{GetFileName}' is not an ELF.", Path.GetFileName(binaryFilePath));
                return;
            }

            IEnumerable<StringTable<ulong>> stringTableSections = elfFile.GetSections<StringTable<ulong>>()
                .Where(t => t.Name is not ".shstrtab");

            using FileStream binaryStream = new(binaryFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, BUFFER_SIZE);

            foreach (StringTable<ulong> section in stringTableSections)
            {
                await ReplaceStringsInSectionAsync(binaryStream, (long)section.Offset, (long)section.Size, pattern, replacement, token);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error processing ELF file '{BinaryFilePath}'", binaryFilePath);
            throw;
        }
        finally
        {
            elfFile?.Dispose();

            if (File.Exists(elfReadPath))
            {
                File.Delete(elfReadPath);
            }
        }
    }

    public async Task ModifyArchiveStringsAsync(Regex pattern, string replacement, string[] extraFiles, CancellationToken token)
    {
        logger?.LogInformation("Renaming OBB file '{GetFileName}'", Path.GetFileName(binaryFilePath));
        ReportUpdate("Renaming OBB internal", ProgressUpdateType.Title);

        // Not really indexing, but it sounds cooler :p
        ReportUpdate("Indexing...");

        using ZipFile zip = new(binaryFilePath);

        List<ZipEntry> selectedFiles = zip.Entries
            .Where(e => e.FileName.Contains("catalog") || extraFiles.Contains(e.FileName))
            .ToList();

        foreach (ZipEntry entry in selectedFiles)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            logger?.LogDebug("Renaming OBB entry '{FileName}'", entry.FileName);
            ReportUpdate(entry.FileName);

            await ProcessTextEntryAsync(zip, entry, pattern, replacement, token);
        }

        zip.Save();
    }

    private async Task ReplaceStringsInSectionAsync(
        FileStream elfStream,
        long sectionOffset,
        long sectionSize,
        Regex searchPattern,
        string replacement,
        CancellationToken token
    )
    {
        long originalPosition = elfStream.Position;
        _ = elfStream.Seek(sectionOffset, SeekOrigin.Begin);
        byte[] sectionData = new byte[sectionSize];
        int bytesRead = await elfStream.ReadAsync(sectionData.AsMemory(0, (int)sectionSize), token);

        if (bytesRead != sectionSize)
        {
            logger?.LogWarning("Could only read {BytesRead} bytes from section at offset {SectionOffset} (expected {SectionSize}).", bytesRead, sectionOffset, sectionSize);
            _ = elfStream.Seek(originalPosition, SeekOrigin.Begin);
            return;
        }

        int currentOffsetInSection = 0;
        while (currentOffsetInSection < sectionData.Length)
        {
            int stringStart = currentOffsetInSection;
            while (currentOffsetInSection < sectionData.Length && sectionData[currentOffsetInSection] != 0)
            {
                currentOffsetInSection++;
            }

            if (currentOffsetInSection > stringStart)
            {
                ReplaceBinarySubstring(searchPattern, replacement, sectionData, encoding, currentOffsetInSection, stringStart);
            }

            currentOffsetInSection++;
        }

        _ = elfStream.Seek(sectionOffset, SeekOrigin.Begin);
        await elfStream.WriteAsync(sectionData.AsMemory(0, (int)sectionSize), token);

        _ = elfStream.Seek(originalPosition, SeekOrigin.Begin);
    }

    private void ReplaceBinarySubstring(Regex searchPattern, string replacement, byte[] sectionData, Encoding encoding, int currentOffsetInSection, int stringStart)
    {
        int stringLength = currentOffsetInSection - stringStart;
        string originalString = encoding.GetString(sectionData, stringStart, stringLength);

        string newString = searchPattern.Replace(originalString, replacement);

        ReportUpdate(newString);

        if (newString.Length != originalString.Length)
        {
            logger?.LogWarning("Replacement string '{NewString}' is longer than the original '{OriginalString}'. Skipping.", newString, originalString);
            return;
        }

        byte[] newStringBytes = encoding.GetBytes(newString);
        Array.Copy(newStringBytes, 0, sectionData, stringStart, newStringBytes.Length);
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

    public void ReportUpdate(string update, ProgressUpdateType updateType = ProgressUpdateType.Content)
    {
        progressReporter?.Report(new(update, updateType));
    }
}
