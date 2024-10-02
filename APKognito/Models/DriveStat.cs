using System.IO;

namespace APKognito.Models;

public record DriveFolderStat
{
    public static DriveFolderStat Empty = new(string.Empty, "No files to cleanup!", 0, false);

    public string FolderPath { get; }

    public string FolderName { get; }

    public long FolderSizeBytes { get; }

    public int FolderSizeMegabytes => (int)(FolderSizeBytes / 1024 / 1024);

    public bool IsFile { get; }

    public DateTime CreationDate {get;}

    public string FormattedCreationDate { get; }

#if DEBUG
    public DriveFolderStat(string folderPath, long folderByteSize, bool isFile = false)
    {
        FolderPath = folderPath;

        FolderName = isFile
            ? Path.GetFileName(folderPath)
            : Path.GetDirectoryName(folderPath) ?? "[Unknown]";

        FolderSizeBytes = folderByteSize;

        CreationDate = DateTimeOffset.FromUnixTimeSeconds(Random.Shared.NextInt64(-62135596800, 253402300799)).DateTime;
        FormattedCreationDate = CreationDate.ToString();

        IsFile = isFile;
    }
#endif

    public DriveFolderStat(DirectoryInfo directory, long folderByteSize)
    {
        FolderPath = directory.FullName;
        FolderName = directory.Name;

        FolderSizeBytes = folderByteSize;
        
        CreationDate = directory.CreationTime;
        FormattedCreationDate = CreationDate.ToString();

        IsFile = false;
    }

    public DriveFolderStat(FileInfo file)
    {
        FolderPath = file.FullName;
        FolderName = file.Name;

        FolderSizeBytes = file.Length;

        CreationDate = file.CreationTime;
        FormattedCreationDate = CreationDate.ToString();

        IsFile = true;
    }

    private DriveFolderStat(string folderPath, string folderName, long folderByteSize, bool isFile)
    {
        FolderPath = folderPath;
        FolderName = folderName;
        FolderSizeBytes = folderByteSize;
        IsFile = isFile;
    }
}
