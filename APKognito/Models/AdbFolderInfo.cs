using APKognito.Utilities;
using System.Globalization;
using Wpf.Ui.Controls;

namespace APKognito.Models;

public class AdbFolderInfo
{
    private const string STAT_TIME_FORMAT_STRING = "yyyy-MM-dd HH:mm:ss.fffffff zzz";

    public const string STAT_FORMAT_SEPARATOR = "||";
    public const string STAT_FORMAT_STRING = $"%y{STAT_FORMAT_SEPARATOR}%F{STAT_FORMAT_SEPARATOR}%s{STAT_FORMAT_SEPARATOR}%N{STAT_FORMAT_SEPARATOR}%U";

    #region Static Instances

    public static AdbFolderInfo EmptyLoading => new($"{STAT_FORMAT_SEPARATOR}{STAT_FORMAT_SEPARATOR}{STAT_FORMAT_SEPARATOR}/Loading...{STAT_FORMAT_SEPARATOR}");
    public static AdbFolderInfo EmptyDirectory => new($"{STAT_FORMAT_SEPARATOR}E{STAT_FORMAT_SEPARATOR}{STAT_FORMAT_SEPARATOR}/Empty Directory{STAT_FORMAT_SEPARATOR}");

#if DEBUG
    public static AdbFolderInfo DebugFiller => new($"2008-12-31 17:00:00.000000000 -0700{STAT_FORMAT_SEPARATOR}regular file{STAT_FORMAT_SEPARATOR}69420{STAT_FORMAT_SEPARATOR}/Random File{STAT_FORMAT_SEPARATOR}root");
#endif

    public static AdbFolderInfo RootFolder => new($"{STAT_FORMAT_SEPARATOR}directory{STAT_FORMAT_SEPARATOR}0{STAT_FORMAT_SEPARATOR}/{STAT_FORMAT_SEPARATOR}root");

    #endregion Static Instances

    public string? ParentDirectory { get; } = null;

    /// <summary>
    /// Specifically for the view to help locate the corresponding TreeViewItem.
    /// </summary>
    public string TreeViewItemTag { get; } = string.Empty;

    public DateTime CreationDate { get; }

    public string FileOwner { get; } = string.Empty;

    public string FileName { get; } = string.Empty;

    public string FullPath { get; } = string.Empty;

    public AdbFolderType ItemType { get; } = AdbFolderType.None;

    public long FileSizeInBytes { get; } = 0;

    // Size info is not rendered for directories, so the item type is needed for the converter
    public KeyValuePair<long, AdbFolderType> ConverterPair { get; }

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

    public AdbFolderInfo(string statInfo, string? parentItem = null)
    {
        /*
         * 'stat' format: %y %F %s %N %U (STAT_FORMAT_STRING)
         *      0: Date (0000-00-0)
         *      1: File type
         *      2: Size in bytes (gives block size for directories)
         *      3: Full path (or: path -> '/link/path')
         *      4: Owner name
         */

        string[] parts = statInfo.Split(STAT_FORMAT_SEPARATOR);

        string rawTime = parts[0];

        if (!string.IsNullOrEmpty(rawTime))
        {
            if (rawTime.Length > 26 && rawTime[19] == '.')
            {
                rawTime = string.Concat(rawTime.AsSpan(0, 27), rawTime.AsSpan(29));
            }

            DebugOnlyException.Assert(rawTime != parts[0], "stat time format didn't work");

            if (DateTime.TryParseExact(rawTime, STAT_TIME_FORMAT_STRING, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDateTime))
            {
                CreationDate = parsedDateTime;
            }
        }

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

        ConverterPair = new(FileSizeInBytes, ItemType);
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

        return sliceStart == -1 ? path : fpath[(sliceStart + 1)..];
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