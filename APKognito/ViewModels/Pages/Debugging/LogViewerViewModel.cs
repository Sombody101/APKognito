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
    #region Properties

    [ObservableProperty]
    private string _logpackCreatorVersion = string.Empty;

    [ObservableProperty]
    private string _logpackPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<LogViewerLine> _logLines = [
#if DEBUG
        new("[10:12:55.999 PM: INFO] \t[App.OnStartup] App start. v1.8.9150.29065, Release"),
        new("[10:12:55.999 AM: INFO] \t[AutoUpdaterService.LogNextUpdate] Next update check will be at x/xx/xxxx x:xx:xx FM"),
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
        }
        catch (Exception ex)
        {
            SnackError("Failed to open logpack", ex.Message);
            FileLogger.LogException(ex);
        }
    }

    #endregion Commands

    private async Task OpenAndDeployLogpack(string packPath)
    {
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
                    // Not needed (yet?)
                    break;

                default: LogpackCreatorVersion = entry.Name; break;
            }
        }

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

        bool exceptionLogsAvailable = exLogs is not null;
        Stream? exLogStream = null;
        StreamReader? exLogReader = null;

        if (exceptionLogsAvailable)
        {
            exLogStream = exLogs!.Open();
            exLogReader = new(exLogStream);
        }

        StringBuilder logBuilder = new();
        string? line = string.Empty;

        while ((line = await logStreamReader.ReadLineAsync()) is not null)
        {
            logBuilder.AppendLine(line);

            // The start of a log line.
            if (logStreamReader.Peek() is '[')
            {
                LogViewerLine newLine = new(logBuilder.ToString().Trim());
                LogLines.Add(newLine);

                logBuilder.Clear();
            }
            else if (exceptionLogsAvailable && line.StartsWith("Exception: "))
            {

            }
        }
    }
}
