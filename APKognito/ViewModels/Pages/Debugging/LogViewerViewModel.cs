﻿using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Helpers;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages.Debugging;

public partial class LogViewerViewModel : LoggableObservableObject
{
    private readonly LogViewerConfig viewerConfig = ConfigurationFactory.Instance.GetConfig<LogViewerConfig>();

    // These aren't really being "cached", but it sounds weirder to say "realLogs"
    private List<LogViewerLine> _cachedLogs = [];

    #region Properties

    [ObservableProperty]
    private ObservableCollection<string> _recentPacks = [];

    [ObservableProperty]
    private string _logpackCreatorVersion = "Unkown";

    [ObservableProperty]
    private string _searchFilterText = string.Empty;

    [ObservableProperty]
    private bool _caseSensitiveSearch = false;

    [ObservableProperty]
    private string _logpackPath = "None selected.";

    [ObservableProperty]
    private ObservableCollection<LogViewerLine> _logLines = [];

    [ObservableProperty]
    private LogLevel[] _logLevelCombo = [.. Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>()];

    [ObservableProperty]
    private LogLevel _selectedLogFilter = LogLevel.ANY;

    #endregion Properties

    public LogViewerViewModel()
    {
        DisableFileLogging = true;

        // For designer
        _logLines = [
#if DEBUG
        new("[10:12:55.999 PM: INFO] \t[App.OnStartup] App start. v1.8.9150.29065, Release", false),
        new("[10:12:55.999 AM: INFO] \t[AutoUpdaterService.LogNextUpdate] Next update check will be at x/xx/xxxx x:xx:xx FM", false),
        new("[04:01:36.955 PM: INFO] \t[JavaVersionLocator.CheckLtsDirectory] Checking Java latest directory...\r\n" +
        "[04:01:37.041 PM: ]: EXCEPTION[No log]: DirectoryNotFoundException: Could not find a part of the path 'C:\\Program Files\\Java\\latest'.\r\n" +
        "   at System.IO.Enumeration.FileSystemEnumerator`1.CreateDirectoryHandle(String path, Boolean ignoreNotFound)\r\n" +
        "   at System.IO.Enumeration.FileSystemEnumerator`1.Init()\r\n" +
        "   at System.IO.Enumeration.FileSystemEnumerable`1..ctor(String directory, FindTransform transform, EnumerationOptions options, Boolean isNormalized)\r\n" +
        "   at System.IO.Enumeration.FileSystemEnumerableFactory.UserDirectories(String directory, String expression, EnumerationOptions options)\r\n" +
        "   at System.IO.Directory.InternalEnumeratePaths(String path, String searchPattern, SearchTarget searchTarget, EnumerationOptions options)\r\n" +
        "   at System.IO.Directory.GetDirectories(String path, String searchPattern, EnumerationOptions enumerationOptions)\r\n" +
        "   at APKognito.Utilities.JavaVersionLocator.CheckLtsDirectory(String& javaPath) in C:\\Users\\[:USER:]\\repo\\APKognito\\APKognito\\Utilities\\JavaVersionLocator.cs:line 106", true),
#endif
        ];

        RefreshRecents();
    }

    public LogViewerViewModel(ISnackbarService _snackbarService)
    {
        DisableFileLogging = true;
        SetSnackbarProvider(_snackbarService);

        Log($"Loading recent logs ({viewerConfig.RecentPacks.Count})");
        RefreshRecents();

        Log("Ready to load logpack.");
    }

    #region Commands

    [RelayCommand]
    private async Task OnLoadLogpack()
    {
        OpenFileDialog openFileDialog = new()
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = "ZIP Archive (*.zip)|*.zip",
        };

        if (openFileDialog.ShowDialog() is not true)
        {
            return;
        }

        if (!File.Exists(openFileDialog.FileName))
        {
            SnackError("The selected logpack does not exist.");
            return;
        }

        LogpackPath = openFileDialog.FileName;

        try
        {
            await OpenAndDeployLogpack(LogpackPath);

            AddOrMoveRecent(LogpackPath);

            RefreshRecents();
            ConfigurationFactory.Instance.SaveConfig(viewerConfig);
        }
        catch (Exception ex)
        {
            SnackError("Failed to open logpack", ex.Message);
            FileLogger.LogException(ex);
        }
    }

    [RelayCommand]
    private async Task OnReloadPack()
    {
        if (LogpackPath is null)
        {
            LogError("No logpack selected.");
            return;
        }

        await OpenLogpack(LogpackPath);
    }

    [RelayCommand]
    private static void OnCreateLogpack()
    {
        SettingsViewModel.CreateLogPack();
    }

    [RelayCommand]
    private static void OnOpenAppData()
    {
        App.OpenDirectory(App.AppDataDirectory!.FullName);
    }

    #endregion Commands

    partial void OnLogpackPathChanged(string value)
    {
        AddOrMoveRecent(value);
    }

    partial void OnSearchFilterTextChanged(string value)
    {
        MoveCacheLogsToView(value);
    }

    partial void OnCaseSensitiveSearchChanged(bool value)
    {
        MoveCacheLogsToView(SearchFilterText);
    }

    partial void OnSelectedLogFilterChanged(LogLevel value)
    {
        OnSearchFilterTextChanged(SearchFilterText);
    }

    public async Task OpenLogpack(string packPath)
    {
        try
        {
            await OpenAndDeployLogpack(packPath);
            AddOrMoveRecent(packPath);
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Failed to open logpack", ex.Message);
            LogError(ex);
        }
    }

    private void MoveCacheLogsToView(string filter)
    {
        if (_cachedLogs.Count is 0)
        {
            return;
        }

        bool noTextFilter = string.IsNullOrEmpty(filter);
        bool noLevelFilter = SelectedLogFilter is LogLevel.ANY;
        StringComparison comparer = CaseSensitiveSearch
            ? StringComparison.CurrentCulture
            : StringComparison.OrdinalIgnoreCase;

        LogLines.Clear();

        foreach (var log in _cachedLogs.Where(l => (noLevelFilter || l.LogLevel == SelectedLogFilter)
            && (noTextFilter || l.Contains(filter, comparer))))
        {
            LogLines.Add(log);
        }
    }

    private async Task OpenAndDeployLogpack(string packPath)
    {
        if (packPath is null)
        {
            LogError("Logpack path is null.");
            return;
        }

        Log($"Opening pack {packPath}");

        using var zipStream = File.OpenRead(packPath);
        using ZipArchive zip = new(zipStream);

        ZipArchiveEntry? appLogs = null, exLogs = null;

        WriteGenericLogLine($"Processing archive files ({GBConverter.FormatSizeFromBytes(zipStream.Length)}):");
        foreach (var entry in zip.Entries)
        {
            WriteGenericLogLine($"\t{entry.Name} ({GBConverter.FormatSizeFromBytes(entry.Length)})");

            switch (entry.Name)
            {
                case "applog.log": appLogs = entry; break;
                case "exlog.log": exLogs = entry; break;

                case "history.bin":
                case "logbox.txt":
                case "unpacked.txt":
                    // Not needed (yet?)
                    break;

                default: LogpackCreatorVersion = entry.Name; break;
            }
        }

        Log($"Logpack created by APKognito {LogpackCreatorVersion}");

        if (appLogs is null)
        {
            throw new FormatException("Failed to find application logs file!");
        }

        if (exLogs is null)
        {
            Log("No exceptions log file found in logpack. Proceeding with app logs.");
        }

        await ParseOutLogFiles(appLogs, exLogs);
    }

    private async Task ParseOutLogFiles(ZipArchiveEntry logs, ZipArchiveEntry? exLogs)
    {
        _cachedLogs.Clear();

        if (logs.Length is 0)
        {
            return;
        }

        using Stream logStream = logs.Open();
        using StreamReader logStreamReader = new(logStream);

        bool exceptionLogsAvailable = exLogs is not null && exLogs.Length > 0;
        Stream? exLogStream = null;
        StreamReader? exLogReader = null;

        if (exceptionLogsAvailable)
        {
            exLogStream = exLogs!.Open();
            exLogReader = new(exLogStream);
        }

        StringBuilder logBuilder = new();
        string? line = string.Empty;
        bool isException = false;

        while ((line = await logStreamReader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (exceptionLogsAvailable && line.StartsWith("Exception: "))
            {
                logBuilder.AppendLine(await GetNextException(exLogReader));
                isException = true;
                continue;
            }

            if (!isException)
            {
                logBuilder.AppendLine(line.Trim());
            }

            // The start of a log line.
            if (logStreamReader.Peek() is '[')
            {
                FinalizeLog();
            }
        }

        FinalizeLog();

        void FinalizeLog()
        {
            string log = logBuilder.ToString().TrimEnd();
            LogViewerLine newLine = new(log, isException);
            _cachedLogs.Add(newLine);

            logBuilder.Clear();
            isException = false;
        }

        MoveCacheLogsToView(SearchFilterText);
    }

    private static async Task<string?> GetNextException(StreamReader? exReader)
    {
        if (exReader is null)
        {
            return null;
        }

        StringBuilder sb = new();

        string? line;
        while ((line = await exReader.ReadLineAsync()) is not (null or "-- END LOG --"))
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            sb.AppendLine(line.TrimEnd());
        }

        return sb.ToString();
    }

    private void RefreshRecents()
    {
        var recents = viewerConfig.RecentPacks.ToList();
        recents.Reverse();

        RecentPacks.Clear();
        foreach (string pack in recents.Where(File.Exists))
        {
            RecentPacks.Add(pack);
        }
    }

    private void AddOrMoveRecent(string path)
    {
        int index = RecentPacks.Select((value, idx) => new { value, idx })
            .FirstOrDefault(x => x.value == path)?.idx ?? -1;

        if (index is 0)
        {
            return;
        }

        if (index is not -1)
        {
            RecentPacks.Move(index, 0);
        }
        else
        {
            viewerConfig.RecentPacks.Insert(0, path);
        }
    }
}
