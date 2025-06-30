using System.Text;
using System.Text.RegularExpressions;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Utilities;

internal sealed class BinaryReplace(string filePath, IProgress<ProgressInfo>? progressReporter, ILogger _logger)
{
    private const int BUFFER_SIZE = 1024 * 1024;
    private readonly Encoding _encoding = Encoding.UTF8;

    public async Task ModifyElfStringsAsync(Regex pattern, string replacement, CancellationToken token)
    {
        string elfReadPath = $"{filePath}.apkspare";

        IELF elfFile = null!;

        try
        {
            File.Copy(filePath, elfReadPath, true);

            try
            {
                elfFile = ELFReader.Load(elfReadPath);
            }
            catch (ArgumentException aex)
            {
                _logger?.LogWarning(aex, "Object file '{GetFileName}' is not an ELF.", Path.GetFileName(filePath));
                return;
            }

            IEnumerable<StringTable<ulong>> stringTableSections = elfFile.GetSections<StringTable<ulong>>()
                .Where(t => t.Name is not ".shstrtab");

            using FileStream binaryStream = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, BUFFER_SIZE);

            foreach (StringTable<ulong> section in stringTableSections)
            {
                await ReplaceStringsInSectionAsync(binaryStream, (long)section.Offset, (long)section.Size, pattern, replacement, token);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing ELF file '{BinaryFilePath}'", filePath);
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
            _logger?.LogWarning("Could only read {BytesRead} bytes from section at offset {SectionOffset} (expected {SectionSize}).", bytesRead, sectionOffset, sectionSize);
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
                ReplaceBinarySubstring(searchPattern, replacement, sectionData, _encoding, currentOffsetInSection, stringStart);
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

        progressReporter.ReportProgressMessage(newString);

        if (newString.Length != originalString.Length)
        {
            _logger?.LogWarning("Replacement string '{NewString}' is longer than the original '{OriginalString}'. Skipping.", newString, originalString);
            return;
        }

        byte[] newStringBytes = encoding.GetBytes(newString);
        Array.Copy(newStringBytes, 0, sectionData, stringStart, newStringBytes.Length);
    }
}
