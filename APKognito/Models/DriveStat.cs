using System.IO;

namespace APKognito.Models;

public record DriveFolderStat
{
    public string FolderPath { get; init; }

    public string FolderName { get; init; }

    public long FolderSizeBytes { get; init; }

    public int FolderSizeMegabytes => (int)(FolderSizeBytes / 1024 / 1024);

    public bool IsFile { get; init; }

    public DriveFolderStat(string folderPath, long folderByteSize, bool isFile = false)
    {
        FolderPath = folderPath;

        FolderName = isFile
            ? Path.GetFileName(folderPath)
            : Path.GetDirectoryName(folderPath) ?? "[Unknown]";

        FolderSizeBytes = folderByteSize;

        IsFile = isFile;
    }

    public DriveFolderStat(DirectoryInfo directory, long folderByteSize)
    {
        FolderPath = directory.FullName;
        FolderName = directory.Name;

        FolderSizeBytes = folderByteSize;

        IsFile = false;
    }

    public DriveFolderStat(FileInfo file)
    {
        FolderPath = file.FullName;
        FolderName = file.Name;

        FolderSizeBytes = file.Length;

        IsFile = true;
    }

    private DriveFolderStat(string folderPath, string folderName, long folderByteSize, bool isFile)
    {
        FolderPath = folderPath;
        FolderName = folderName;
        FolderSizeBytes = folderByteSize;
        IsFile = isFile;
    }

    public static DriveFolderStat Empty = new(string.Empty, "No files to cleanup!", 0, false);
}
