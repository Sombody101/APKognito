using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Utilities;

internal sealed class BinaryReplace(string filePath, IProgress<ProgressInfo>? progressReporter, ILogger _logger)
{
    private const int ELF_MAGIC = 0x464c457f;
    private const int BUFFER_SIZE = 1024 * 1024;

    public async Task ModifyElfStringsAsync(Regex pattern, string replacement, CancellationToken token)
    {
        string elfReadPath = $"{filePath}.apkspare";

        IELF elfFile = null!;

        try
        {
            // TODO: Change the buffer to a config value at some point...
            if (!VerifyElf(filePath, BUFFER_SIZE, out FileStream? stream))
            {
                _logger?.LogWarning("Object file '{Name}' is not an ELF.", Path.GetFileName(filePath));
                return;
            }

            using FileStream writeStream = stream;

            File.Copy(filePath, elfReadPath, true);

            if (!ELFReader.TryLoad(elfReadPath, out elfFile))
            {
                _logger?.LogWarning("Failed to load ELF file '{Name}'", Path.GetFileName(filePath));
                return;
            }

            IEnumerable<StringTable<ulong>> stringTableSections = elfFile.GetSections<StringTable<ulong>>()
                .Where(t => t.Name is not ".shstrtab");

            foreach (StringTable<ulong> section in stringTableSections)
            {
                await ReplaceStringsInSectionAsync(writeStream, (long)section.Offset, (long)section.Size, pattern, replacement, token);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing ELF file '{BinaryPath}'", filePath);
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
            if (token.IsCancellationRequested)
            {
                return;
            }

            int stringStart = currentOffsetInSection;
            while (currentOffsetInSection < sectionData.Length && sectionData[currentOffsetInSection] != 0)
            {
                currentOffsetInSection++;
            }

            if (currentOffsetInSection > stringStart)
            {
                await ReplaceBinarySubstringAsync(searchPattern, replacement, sectionData, Encoding.UTF8, currentOffsetInSection, stringStart, token);
            }

            currentOffsetInSection++;
        }

        _ = elfStream.Seek(sectionOffset, SeekOrigin.Begin);
        await elfStream.WriteAsync(sectionData.AsMemory(0, (int)sectionSize), token);

        _ = elfStream.Seek(originalPosition, SeekOrigin.Begin);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async ValueTask ReplaceBinarySubstringAsync(
        Regex searchPattern,
        string replacement,
        byte[] sectionData,
        Encoding encoding,
        int currentOffsetInSection,
        int stringStart,
        CancellationToken token
    )
    {
        int stringLength = currentOffsetInSection - stringStart;
        string originalString = encoding.GetString(sectionData, stringStart, stringLength);

        string newString = searchPattern.Replace(originalString, replacement);

        progressReporter.ReportProgressMessage(newString);

        if (newString.Length != originalString.Length)
        {
            if (_logger is not null)
            {
                _logger.LogWarning("Replacement string '{NewString}' is longer than the original '{OriginalString}'. Skipping.", newString, originalString);

                // It's dumb, but allows the UI thread to catch up.
                // Very specific to APKognito. Will be removed if this lib is used by other people for whatever reason...
                await Task.Delay(1, token);
            }

            return;
        }

        byte[] newStringBytes = encoding.GetBytes(newString);
        Array.Copy(newStringBytes, 0, sectionData, stringStart, newStringBytes.Length);
    }

    private static bool VerifyElf(string path, int bufferSize, [NotNullWhen(true)] out FileStream? stream)
    {
        stream = null;
        FileStream? fileStream = null;

        try
        {
            fileStream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, bufferSize);
            using BinaryReader reader = new(fileStream, Encoding.UTF8, true);

            int readMagic = reader.ReadInt32();

            if (readMagic != ELF_MAGIC)
            {
                fileStream.Dispose();
                return false;
            }

            fileStream.Seek(0, SeekOrigin.Begin);
            stream = fileStream;
            return true;
        }
        catch
        {
            fileStream?.Dispose();
            return false;
        }
    }
}
