using System.IO;

namespace APKognito.Models;

public record FootprintInfo
{
    public static readonly FootprintInfo Empty = new(string.Empty, "No files to cleanup!", 0, FootprintType.File);

    public string FolderPath { get; }

    public string FolderName { get; }

    public long FolderSizeBytes { get; }

    public int FolderSizeMegabytes => (int)(FolderSizeBytes / 1024 / 1024);

    public FootprintType ItemType { get; }

    public DateTime CreationDate { get; }

    public string FormattedCreationDate { get; }

#if DEBUG
    public FootprintInfo(string folderPath, long folderByteSize, FootprintType itemType = FootprintType.Directory)
    {
        FolderPath = folderPath;

        FolderName = itemType is FootprintType.File
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

        ItemType = FootprintType.Directory;
    }

    public FootprintInfo(FileInfo file)
    {
        FolderPath = file.FullName;
        FolderName = file.Name;

        FolderSizeBytes = file.Length;

        CreationDate = file.CreationTime;
        FormattedCreationDate = CreationDate.ToString();

        ItemType = FootprintType.File;
    }

    private FootprintInfo(string folderPath, string folderName, long folderByteSize, FootprintType itemType)
    {
        FolderPath = folderPath;
        FolderName = folderName;
        FolderSizeBytes = folderByteSize;
        ItemType = itemType;
    }
}

public enum FootprintType
{
    Directory,
    File,
    RenamedApk,
}