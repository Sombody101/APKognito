using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Wpf.Ui.Controls;

namespace APKognito.Models;

public class AdbFolderInfo : ObservableObject
{
    public const string FormatSeparator = "///";
    public const string FormatString = $"%.10y{FormatSeparator}%F{FormatSeparator}%s{FormatSeparator}%N{FormatSeparator}%U";

    public static readonly AdbFolderInfo EmptyLoading = new($"{FormatSeparator}{FormatSeparator}{FormatSeparator}/Loading...{FormatSeparator}");
    public static readonly AdbFolderInfo EmptyDirectory = new($"{FormatSeparator}E{FormatSeparator}{FormatSeparator}/Empty Directory{FormatSeparator}");

#if DEBUG
    public static readonly AdbFolderInfo DebugFiller = new($"{FormatSeparator}{FormatSeparator}{FormatSeparator}/Random File{FormatSeparator}");
#endif

    public TreeViewItem? ParentTreeViewItem { get; }

    /// <summary>
    /// Specifically for the view to help locate the corresponding TreeViewItem.
    /// </summary>
    public string TreeViewItemTag { get; }

    public string RawCreationDate { get; }

    public string CreationDate => string.IsNullOrWhiteSpace(RawCreationDate)
                ? string.Empty
                : DateTime.ParseExact(RawCreationDate, "yyyy-M-d", CultureInfo.CurrentCulture).ToString();

    public string FileOwner { get; }

    public string FileName { get; }

    public string FullPath { get; }

    public AdbFolderType ItemType { get; }

    public KeyValuePair<long, AdbFolderType> ConverterPair => new(FileSizeInBytes, ItemType);

    public string FormattedItemType => ItemType switch
    {
        AdbFolderType.None => string.Empty,
        AdbFolderType.File => "File",
        AdbFolderType.Directory => "Directory",
        AdbFolderType.SymbolicLink => "Directory (Link)", // Resolved virtually
        _ => string.Empty
    };

    public long FileSizeInBytes { get; }

    public ObservableCollection<AdbFolderInfo>? Children { get; set; }

    public SymbolIcon ItemIcon => new()
    {
        Symbol = ItemType switch
        {
            AdbFolderType.File => SymbolRegular.Document48,
            AdbFolderType.Directory => SymbolRegular.Folder48,
            AdbFolderType.SymbolicLink => SymbolRegular.FolderLink48,
            AdbFolderType.EmptyDirectory => SymbolRegular.DocumentProhibited24,
            AdbFolderType.LoadingChildren => SymbolRegular.DocumentSearch32,
            AdbFolderType.None => SymbolRegular.Empty,
            _ => SymbolRegular.Question48
        }
    };

    public AdbFolderInfo()
    {
    }

    public AdbFolderInfo(string statInfo, TreeViewItem? parentItem = null)
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
            FileName = Path.GetFileName(FixSegment(split[0]));
            FullPath = FixSegment(split[1]); // 'path' -> path
        }
        else
        {
            // Only given a file
            FullPath = FixSegment(parts[3]);
            FileName = Path.GetFileName(FullPath);
        }

        FileOwner = parts[4];

        if (ItemType is AdbFolderType.Directory or AdbFolderType.SymbolicLink)
        {
            // Adds a dropdown button on the tree view
            Children = [EmptyLoading];
        }

        ParentTreeViewItem = parentItem;
        TreeViewItemTag = Random.Shared.Next().ToString("x");
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
            return segment[1..^1];
        }

        return segment;
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