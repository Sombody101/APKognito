using System.Globalization;
using System.IO;
using Wpf.Ui.Controls;

namespace APKognito.Models;

public class AdbFolderInfo
{
    public const string FormatSeparator = "||";
    public const string FormatString = $"%.10y{FormatSeparator}%F{FormatSeparator}%s{FormatSeparator}%N{FormatSeparator}%U";

    public static AdbFolderInfo EmptyLoading => new($"{FormatSeparator}{FormatSeparator}{FormatSeparator}/Loading...{FormatSeparator}");
    public static AdbFolderInfo EmptyDirectory => new($"{FormatSeparator}E{FormatSeparator}{FormatSeparator}/Empty Directory{FormatSeparator}");

#if DEBUG
    public static AdbFolderInfo DebugFiller => new($"2022-12-20{FormatSeparator}regular file{FormatSeparator}69420{FormatSeparator}/Random File{FormatSeparator}root");
#endif

    public static AdbFolderInfo RootFolder => new($"{FormatSeparator}directory{FormatSeparator}0{FormatSeparator}/{FormatSeparator}root");

    public AdbFolderInfo? ParentDirectory { get; }

    /// <summary>
    /// Specifically for the view to help locate the corresponding TreeViewItem.
    /// </summary>
    public string TreeViewItemTag { get; }

    public string RawCreationDate { get; }

    public string CreationDate => string.IsNullOrWhiteSpace(RawCreationDate)
                ? RawCreationDate
                : DateTime.ParseExact(RawCreationDate, "yyyy-M-d", CultureInfo.CurrentCulture).ToString();

    public string FileOwner { get; }

    public string FileName { get; }

    public string FullPath { get; }

    public AdbFolderType ItemType { get; }

    public long FileSizeInBytes { get; }

    // Size info is not rendered for directories, so the item type is needed for the converter
    public KeyValuePair<long, AdbFolderType> ConverterPair => new(FileSizeInBytes, ItemType);

    public string FormattedItemType => ItemType switch
    {
        AdbFolderType.None => string.Empty,
        AdbFolderType.File => "File",
        AdbFolderType.Directory => "Directory",
        AdbFolderType.SymbolicLink => "Directory (Link)", // Resolved virtually
        _ => string.Empty
    };

    public SymbolIcon ItemIcon => new()
    {
        Symbol = GetSymbol()
    };

    public SymbolRegular Symbol => GetSymbol();

    public AdbFolderInfo()
    {
    }

    public AdbFolderInfo(string statInfo, AdbFolderInfo? parentItem = null)
    {
        /*
         * 'stat' format: %.10y %F %s %N %U
         *      0: Date (0000-00-0)
         *      1: File type
         *      2: Size in bytes (gives block size for directories)
         *      3: Full path (or: path -> '/link/path')
         *      4: Owner name
         */

        string[] parts = statInfo.Split(FormatSeparator);

        RawCreationDate = parts[0];

        ItemType = ResolveType(parts[1]);

        _ = long.TryParse(parts[2], out long result);
        FileSizeInBytes = result;

        // Assume it's a directory, they're more common
        if (ItemType is AdbFolderType.SymbolicLink)
        {
            string[] split = parts[3].Split(" -> ");
            FileName = GetFileName(FixSegment(split[0]));
            FullPath = FixSegment(split[0]); // 'path' -> path
        }
        else
        {
            // Only given a file
            FullPath = FixSegment(parts[3]);
            FileName = GetFileName(FullPath);
        }

        FileOwner = parts[4];

        ParentDirectory = parentItem;
        TreeViewItemTag = Random.Shared.Next().ToString("x");
    }

    private SymbolRegular GetSymbol()
    {
        return ItemType switch
        {
            AdbFolderType.File => SymbolRegular.Document48,
            AdbFolderType.Directory => SymbolRegular.Folder48,
            AdbFolderType.SymbolicLink => SymbolRegular.FolderLink48,
            AdbFolderType.EmptyDirectory => SymbolRegular.DocumentProhibited24,
            AdbFolderType.LoadingChildren => SymbolRegular.DocumentSearch32,
            AdbFolderType.None => SymbolRegular.Empty,
            _ => SymbolRegular.Question48
        };
    }

    private static AdbFolderType ResolveType(string type)
    {
        return type switch
        {
            "directory" => AdbFolderType.Directory,
            "regular file" => AdbFolderType.File,
            "symbolic link" => AdbFolderType.SymbolicLink,
            "E" => AdbFolderType.EmptyDirectory,
            "L" => AdbFolderType.LoadingChildren,
            _ => AdbFolderType.None,
        };
    }

    private static string FixSegment(string segment)
    {
        if (segment.Length > 3
           && segment[0] is '`'
           && segment[^1] is '\'')
        {
            segment = segment[1..^1];
        }

        if (segment.StartsWith("//"))
        {
            segment = segment[1..];
        }

        return segment;
    }

    private static string GetFileName(string path)
    {
        string fpath = path.TrimEnd('/');
        int sliceStart = fpath.LastIndexOf('/');

        if (sliceStart == -1)
        {
            return path;
        }

        return fpath[(sliceStart + 1)..];
    }

    public override string ToString()
    {
        return FullPath;
    }
}

public enum AdbFolderType
{
    None,
    File,
    Directory,
    SymbolicLink,
    EmptyDirectory,
    LoadingChildren,
}