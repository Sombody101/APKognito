using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
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
    private readonly LogViewerConfig viewerConfig = ConfigurationFactory.GetConfig<LogViewerConfig>();

    #region Properties

    [ObservableProperty]
    private ObservableCollection<string> _recentPacks = [];

    [ObservableProperty]
    private string _logpackCreatorVersion = string.Empty;

    [ObservableProperty]
    private string _filterSearch = string.Empty;

    [ObservableProperty]
    private string _logpackPath = "None selected.";

    [ObservableProperty]
    private ObservableCollection<LogViewerLine> _logLines = [
#if DEBUG
        new("[10:12:55.999 PM: INFO] \t[App.OnStartup] App start. v1.8.9150.29065, Release", false),
        new("[10:12:55.999 AM: INFO] \t[AutoUpdaterService.LogNextUpdate] Next update check will be at x/xx/xxxx x:xx:xx FM", false),
#endif
    ];

    #endregion Properties

    public LogViewerViewModel()
    {
        // For designer
    }

    public LogViewerViewModel(ISnackbarService _snackbarService)
    {
        SetSnackbarProvider(_snackbarService);
        RefreshRecents();
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
            ConfigurationFactory.SaveConfig(viewerConfig);
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

    #endregion Commands

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

    private async Task OpenAndDeployLogpack(string packPath)
    {
        if (packPath is null)
        {
            LogError("Logpack path is null.");
            return;
        }

        Log($"Opening pack {packPath}");

        using ZipArchive zip = new(File.OpenRead(packPath));

        ZipArchiveEntry? appLogs = null, exLogs = null;

        foreach (var entry in zip.Entries)
        {
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
        LogLines.Clear();

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

            logBuilder.AppendLine(line.Trim());

            // The start of a log line.
            if (logStreamReader.Peek() is '[')
            {
                LogViewerLine newLine = new(logBuilder.ToString().TrimEnd(), isException);
                LogLines.Add(newLine);

                logBuilder.Clear();
                isException = false;
            }
            else if (exceptionLogsAvailable && line.StartsWith("Exception: "))
            {
                logBuilder.AppendLine(await GetNextException(exLogReader));
                isException = true;
            }
        }
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
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private void RefreshRecents()
    {
        var recents = viewerConfig.RecentPacks.ToList();
        recents.Reverse();

        foreach (string pack in recents.Where(File.Exists))
        {
            RecentPacks.Add(pack);
        }
    }

    private void AddOrMoveRecent(string path)
    {
        int index = RecentPacks.Select((value, idx) => new { value, idx })
            .FirstOrDefault(x => x.value == path)?.idx ?? -1;

        if (index is not -1)
        {
            RecentPacks.Move(index, RecentPacks.Count - 1);
        }
        else
        {
            viewerConfig.RecentPacks.Add(path);
        }
    }
}
