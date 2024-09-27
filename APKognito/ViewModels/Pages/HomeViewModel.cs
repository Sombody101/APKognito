using APKognito.Models.Settings;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace APKognito.ViewModels.Pages;

public partial class HomeViewModel : ObservableObject, IViewable
{
    private const string defaultPropertyMessage = "No APK loaded";

    // By the time this is used anywhere, it will not be null
    public static HomeViewModel Instance { get; private set; }

    private readonly KognitoConfig config;

    public HomeViewModel()
    {
        Instance = this;
        config = KognitoSettings.GetSettings();
    }

    /*
     * Properties
     */
    #region Properties

    private readonly StringBuilder _logs = new();
    public string Logs
    {
        get => _logs.ToString();
        set
        {
            _ = _logs.AppendLine(value);
            OnPropertyChanged(nameof(Logs));
        }
    }

    public void Log(string log)
    {
        Logs = $"[APKognito] ~ {log}";
    }

    public void LogWarning(string log)
    {
        Logs = $"[APKognito] # {log}";
    }

    public void LogError(string log)
    {
        Logs = $"[APKognito] ! {log}";
    }

    public void ClearLogs()
    {
        _ = _logs.Clear();
    }

    private string _apkName = defaultPropertyMessage;
    public string ApkName
    {
        get => _apkName;
        set
        {
            _apkName = value;
            OnPropertyChanged(nameof(ApkName));
        }
    }

    private string _originalPackageName = defaultPropertyMessage;
    public string OriginalPackageName
    {
        get => _originalPackageName;
        set
        {
            _originalPackageName = value;
            OnPropertyChanged(nameof(OriginalPackageName));
        }
    }

    public string FilePath
    {
        get
        {
            string? sourcePath = config.ApkSourcePath;

            if (sourcePath is null)
            {
                return defaultPropertyMessage;
            }

            // Return the number of paths
            if (sourcePath.Contains(':'))
            {
                return sourcePath.Split(':').Length.ToString("0 APKs (View the logs to see paths)");
            }

            // Just a single file has been selected
            return sourcePath;
        }
        set
        {
            config.ApkSourcePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }

    public string[]? GetFilePaths()
    {
        return config.ApkSourcePath?.Split(':');
    }

    public string OutputPath
    {
        get => config.ApkOutputDirectory ?? defaultPropertyMessage;
        set
        {
            config.ApkOutputDirectory = value;
            OnPropertyChanged();
        }
    }

    private string finalName = defaultPropertyMessage;
    public string FinalName
    {
        get => finalName;
        set => SetProperty(ref finalName, value);
    }

    #endregion Properties
}
