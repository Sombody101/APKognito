using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Ionic.Zip;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace APKognito.ApkMod;

internal class BinaryReplace
{
    private const int BUFFER_SIZE = 1024 * 1024;

    private readonly string binaryFilePath;
    private readonly LoggableObservableObject? logger;

    public BinaryReplace(string filePath, LoggableObservableObject? _logger)
    {
        binaryFilePath = filePath;
        logger = _logger;
    }

    public async Task ModifyElfStringsAsync(Regex pattern, string replacement)
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
            catch (ArgumentException)
            {
                logger?.LogWarning($"Object file '{Path.GetFileName(binaryFilePath)}' is not an ELF.");
                return;
            }

            IReadOnlyList<ELFSharp.ELF.Segments.ISegment> t = elfFile.Segments;

            IEnumerable<StringTable<ulong>> stringTableSections = elfFile.GetSections<StringTable<ulong>>()
                .Where(t => t.Name is not ".shstrtab");

            using FileStream binaryStream = new(binaryFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, BUFFER_SIZE);

            foreach (StringTable<ulong> section in stringTableSections)
            {
                await ReplaceStringsInSectionAsync(binaryStream, (long)section.Offset, (long)section.Size, pattern, replacement);
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogException($"Error processing ELF file '{binaryFilePath}'", ex);
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

    public async Task ModifyArchiveStringsAsync(Regex pattern, string replacement)
    {
        using ZipFile zip = new(binaryFilePath);

        foreach (ZipEntry entry in zip.Entries.Where(e => e.FileName.Contains("catalog")).ToList())
        {
            await ProcessTextEntryAsync(zip, entry, pattern, replacement);
        }

        zip.Save();
    }

    private async Task ReplaceStringsInSectionAsync(
        FileStream elfStream,
        long sectionOffset,
        long sectionSize,
        Regex searchPattern,
        string replacement
    )
    {
        long originalPosition = elfStream.Position; // Store the original stream position
        _ = elfStream.Seek(sectionOffset, SeekOrigin.Begin);
        byte[] sectionData = new byte[sectionSize];
        int bytesRead = await elfStream.ReadAsync(sectionData.AsMemory(0, (int)sectionSize));

        if (bytesRead != sectionSize)
        {
            logger?.LogWarning($"Could only read {bytesRead} bytes from section at offset {sectionOffset} (expected {sectionSize}).");
            _ = elfStream.Seek(originalPosition, SeekOrigin.Begin); // Restore original position
            return;
        }

        Encoding encoding = Encoding.UTF8;

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
                int stringLength = currentOffsetInSection - stringStart;
                string originalString = encoding.GetString(sectionData, stringStart, stringLength);

                if (!originalString.Contains("V4PlayerViewOp"))
                {
                    string newString = searchPattern.Replace(originalString, replacement);

                    if (newString.Length == originalString.Length)
                    {
                        byte[] newStringBytes = encoding.GetBytes(newString);
                        Array.Copy(newStringBytes, 0, sectionData, stringStart, newStringBytes.Length);
                    }
                    else if (newString.Length < originalString.Length)
                    {
                        byte[] newStringBytes = encoding.GetBytes(newString);
                        Array.Copy(newStringBytes, 0, sectionData, stringStart, newStringBytes.Length);

                        // Pad with nulls
                        for (int i = newStringBytes.Length; i < originalString.Length; i++)
                        {
                            sectionData[stringStart + i] = 0;
                        }
                    }
                    else
                    {
                        logger?.LogWarning($"Replacement string '{newString}' is longer than the original '{originalString}'. Skipping.");
                    }
                }
            }

            currentOffsetInSection++; // Move past the null terminator
        }

        // Write the modified section data back to the stream
        _ = elfStream.Seek(sectionOffset, SeekOrigin.Begin);
        await elfStream.WriteAsync(sectionData.AsMemory(0, (int)sectionSize));

        _ = elfStream.Seek(originalPosition, SeekOrigin.Begin); // Restore original position
    }

    private static async Task ProcessTextEntryAsync(ZipFile zip, ZipEntry entry, Regex pattern, string replacement)
    {
        string originalContent = string.Empty;

        using (MemoryStream ms = new())
        {
            entry.Extract(ms);
            ms.Position = 0;
            using StreamReader reader = new(ms, Encoding.UTF8);
            originalContent = await reader.ReadToEndAsync();
        }

        string updatedContent = pattern.Replace(originalContent, replacement);

        zip.RemoveEntry(entry.FileName);
        _ = zip.AddEntry(entry.FileName, updatedContent);
    }
}
