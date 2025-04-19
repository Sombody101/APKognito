using System.IO;

namespace APKognito.Models;

public record FootprintInfo
{
    public static readonly FootprintInfo Empty = new(string.Empty, "No files to cleanup!", 0, FootprintTypes.File);

    public string FolderPath { get; } = string.Empty;

    public string FolderName { get; } = string.Empty;

    public long FolderSizeBytes { get; } = 0;

    public FootprintTypes ItemType { get; } = FootprintTypes.None;

    public DateTime CreationDate { get; }

    public string FormattedCreationDate { get; } = string.Empty;

#if DEBUG
    /// <summary>
    /// Creates an example <see cref="FootprintInfo"/> object for the designer.
    /// </summary>
    /// <param name="folderPath"></param>
    /// <param name="folderByteSize"></param>
    /// <param name="itemType"></param>
    public FootprintInfo(string folderPath, long folderByteSize, FootprintTypes itemType = FootprintTypes.Directory)
    {
        FolderPath = folderPath;

        FolderName = itemType is FootprintTypes.File
            ? Path.GetFileName(folderPath)
            : Path.GetDirectoryName(folderPath) ?? "[Unknown]";

        FolderSizeBytes = folderByteSize;

        CreationDate = DateTimeOffset.FromUnixTimeSeconds(Random.Shared.NextInt64(-62135596800, 253402300799)).DateTime;
        FormattedCreationDate = CreationDate.ToString();

        ItemType = itemType;
    }
#endif

    public FootprintInfo(DirectoryInfo directory, long folderByteSize)
    {
        FolderPath = directory.FullName;
        FolderName = directory.Name;

        FolderSizeBytes = folderByteSize;

        CreationDate = directory.CreationTime;
        FormattedCreationDate = CreationDate.ToString();

        // Check if the directory has an APK file
        if (Directory.GetFiles(directory.FullName, "*.apk").Any())
        {
            ItemType = FootprintTypes.RenamedApk;
        }
        else
        {
            ItemType = directory.FullName.StartsWith(Path.GetTempPath()) ? FootprintTypes.TempDirectory : FootprintTypes.Directory;
        }
    }

    public FootprintInfo(FileInfo file)
    {
        FolderPath = file.FullName;
        FolderName = file.Name;

        FolderSizeBytes = file.Length;

        CreationDate = file.CreationTime;
        FormattedCreationDate = CreationDate.ToString();

        ItemType = FootprintTypes.File;
    }

    private FootprintInfo(string folderPath, string folderName, long folderByteSize, FootprintTypes itemType)
    {
        FolderPath = folderPath;
        FolderName = folderName;
        FolderSizeBytes = folderByteSize;
        ItemType = itemType;
    }
}

[Flags]
public enum FootprintTypes
{
    None = 0,
    Directory = 1,
    TempDirectory = 2,
    File = 4,
    TempFile = 8,
    RenamedApk = 16,
}