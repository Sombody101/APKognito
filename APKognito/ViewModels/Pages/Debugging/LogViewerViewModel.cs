using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Helpers;
using APKognito.Models;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using Wpf.Ui;

namespace APKognito.ViewModels.Pages.Debugging;

public partial class LogViewerViewModel : LoggableObservableObject
{
    private readonly ConfigurationFactory _configFactory;
    private readonly LogViewerConfig viewerConfig;
    private readonly IContentDialogService _dialogService;

    // These aren't really being "cached", but it sounds weirder to say "realLogs"
    private readonly List<LogViewerLine> _cachedLogs = [];

    #region Properties

    [ObservableProperty]
    public partial ObservableCollection<string> RecentPacks { get; set; } = [];

    [ObservableProperty]
    public partial string LogpackCreatorVersion { get; set; } = "Unkown";

    [ObservableProperty]
    public partial string SearchFilterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CaseSensitiveSearch { get; set; } = false;

    [ObservableProperty]
    public partial string LogpackPath { get; set; } = "None selected.";

    [ObservableProperty]
    public partial ObservableCollection<LogViewerLine> LogLines { get; set; } = [];

    [ObservableProperty]
    public partial LogLevel[] LogLevelCombo { get; set; } = [.. Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>()];

    [ObservableProperty]
    public partial LogLevel SelectedLogFilter { get; set; } = LogLevel.ANY;

    #endregion Properties

    public LogViewerViewModel()
    {
        DisableFileLogging = true;
        LogLines = [
#if DEBUG
        LogViewerLine.FromLog("[10:12:55.999 PM: INFO] \t[App.OnStartup] App start. v1.8.9150.29065, Release", false),
        LogViewerLine.FromLog("[10:12:55.999 AM: INFO] \t[AutoUpdaterService.LogNextUpdate] Next update check will be at x/xx/xxxx x:xx:xx FM", false),
        LogViewerLine.FromLog("[04:01:36.955 PM: INFO] \t[JavaVersionLocator.CheckLtsDirectory] Checking Java latest directory...\r\n" +
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

        viewerConfig = null!;
        _configFactory = null!;
        _dialogService = null!;

        RefreshRecents();
    }

    public LogViewerViewModel(
        ISnackbarService snackbarService,
        IContentDialogService dialogService,
        ConfigurationFactory configFactory
    ) : base(configFactory)
    {
        DisableFileLogging = true;
        SetSnackbarProvider(snackbarService);

        viewerConfig = configFactory.GetConfig<LogViewerConfig>();
        _dialogService = dialogService;
        _configFactory = configFactory;

        Log($"Loading recent logs ({viewerConfig.RecentPacks.Count})");
        RefreshRecents();

        Log("Ready to load logpack.");
    }

    #region Commands

    [RelayCommand]
    private async Task OnLoadLogpackAsync()
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
            await LoadLogpackAsync(LogpackPath);
            AddOrMoveRecent(LogpackPath);

            RefreshRecents();
            _configFactory.SaveConfig(viewerConfig);
        }
        catch (Exception ex)
        {
            SnackError("Failed to open logpack", ex.Message);
            FileLogger.LogException(ex);
        }
    }

    [RelayCommand]
    private async Task OnReloadPackAsync()
    {
        if (LogpackPath is null)
        {
            LogError("No logpack selected.");
            return;
        }

        await OpenLogpackAsync(LogpackPath);
    }

    [RelayCommand]
    private async Task OnCreateLogpackAsync()
    {
        await SettingsViewModel.CreateLogPackAsync(_dialogService);
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

    public async Task OpenLogpackAsync(string packPath)
    {
        try
        {
            await LoadLogpackAsync(packPath);
            AddOrMoveRecent(packPath);
        }
        catch (Exception ex)
        {
            FileLogger.LogException(ex);
            SnackError("Failed to open logpack", ex.Message);
            LogError(ex);
        }
    }

    private async Task LoadLogpackAsync(string packPath)
    {
        LogpackInfo packInfo = await LogpackInfo.FromFileAsync(packPath, this);

        _cachedLogs.Clear();
        _cachedLogs.AddRange(packInfo.LogLines);
        MoveCacheLogsToView(SearchFilterText);
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

        foreach (LogViewerLine? log in _cachedLogs.Where(l => (noLevelFilter || l.LogLevel == SelectedLogFilter)
            && (noTextFilter || l.Contains(filter, comparer))))
        {
            LogLines.Add(log);
        }
    }

    private void RefreshRecents()
    {
        List<string> recents = [.. viewerConfig.RecentPacks];
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

        viewerConfig.RecentPacks = [.. viewerConfig.RecentPacks.Distinct()];
    }
}

public sealed record LogpackInfo : LogpackInfo.ILogpackInfo
{
    public string Name => Path.GetFileName(FullName);

    public string FullName { get; init; } = string.Empty;

    public string CreatorVersion { get; init; } = string.Empty;

    public List<LogViewerLine> LogLines { get; init; } = [];

    public static async Task<LogpackInfo> FromFileAsync(string logpackPath, IViewLogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logpackPath);

        if (!File.Exists(logpackPath))
        {
            throw new IOException($"Failed to find '{logpackPath}'");
        }

        logger?.Log($"Opening pack {logpackPath}");

        using FileStream zipStream = File.OpenRead(logpackPath);
        using ZipArchive archive = new(zipStream);

        logger?.Log($"Processing archive files ({GBConverter.FormatSizeFromBytes(zipStream.Length)}):");

        ZipArchiveEntry? appLogs;
        ZipArchiveEntry? exLogs;
        string creatorVersion;

        using (IDisposable? scope = logger?.BeginScope("Unpack"))
        {
            (appLogs, exLogs, creatorVersion) = ExtractFiles(archive, logger);
        }

        LogpackInfoBuilder builder = new()
        {
            FullName = logpackPath,
            CreatorVersion = creatorVersion,
        };

        logger?.Log($"Logpack created by APKognito {creatorVersion}");

        if (appLogs is null)
        {
            throw new FormatException("Failed to find application logs file!");
        }

        if (exLogs is null)
        {
            logger?.Log("No exceptions log file found in logpack. Proceeding with app logs.");
        }

        List<LogViewerLine> logLines = [];
        builder.LogLines = logLines;

        await ParseOutLogFilesAsync(appLogs, exLogs, logLines);

        return builder.Build();
    }

    private static (ZipArchiveEntry? appLogs,
             ZipArchiveEntry? exLogs,
             string creatorVersion) ExtractFiles(ZipArchive archive, IViewLogger? logger)
    {
        ZipArchiveEntry? appLogs = null, exLogs = null;
        string creatorVersion = string.Empty;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            logger?.Log($"\t{entry.Name} ({GBConverter.FormatSizeFromBytes(entry.Length)})");

            switch (entry.Name)
            {
                case "applog.log": appLogs = entry; break;
                case "exlog.log": exLogs = entry; break;

                case "history.bin":
                case "logbox.txt":
                case "unpacked.txt":
                    // Not needed (yet?)
                    break;

                default: creatorVersion = entry.Name; break;
            }
        }

        return (appLogs, exLogs, creatorVersion);
    }

    private static async Task ParseOutLogFilesAsync(ZipArchiveEntry appLogs, ZipArchiveEntry? exLogs, List<LogViewerLine> logList)
    {
        if (appLogs.Length is 0)
        {
            return;
        }

        using Stream logStream = appLogs.Open();
        using StreamReader logStreamReader = new(logStream);

        bool exceptionLogsAvailable = exLogs is not null && exLogs.Length > 0;

        using Stream? exLogStream = exceptionLogsAvailable ? exLogs!.Open() : null;
        using StreamReader? exLogReader = exceptionLogsAvailable ? new(exLogStream!) : null;

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
                _ = logBuilder.AppendLine(await GetNextExceptionAsync(exLogReader));
                isException = true;
                continue;
            }

            if (!isException)
            {
                _ = logBuilder.AppendLine(line.Trim());
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
            LogViewerLine newLine = LogViewerLine.FromLog(log, isException);
            logList.Add(newLine);

            _ = logBuilder.Clear();
            isException = false;
        }
    }

    private static async Task<string?> GetNextExceptionAsync(StreamReader? exReader)
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

            _ = sb.AppendLine(line.TrimEnd());
        }

        return sb.ToString();
    }

    private sealed record LogpackInfoBuilder : ILogpackInfo
    {
        public string Name { get; set; }

        public string FullName { get; set; }

        public string CreatorVersion { get; set; }

        public List<LogViewerLine> LogLines { get; set; }

        public LogpackInfo Build()
        {
            return new()
            {
                FullName = FullName,
                CreatorVersion = CreatorVersion,
                LogLines = LogLines,
            };
        }
    }

    public interface ILogpackInfo
    {
        public string Name { get; }

        public string FullName { get; }

        public string CreatorVersion { get; }

        public List<LogViewerLine> LogLines { get; }
    }
}
