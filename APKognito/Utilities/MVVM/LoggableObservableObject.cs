using APKognito.Models;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;

using LogEntryType = APKognito.Models.LogBoxEntry.LogEntryType;

namespace APKognito.Utilities.MVVM;

/// <summary>
/// Gives a horrible interface for logging to a <see cref="RichTextBox"/> while still adhering to <see cref="ObservableObject"/> rules.
/// </summary>
public partial class LoggableObservableObject : ViewModel, IViewable, IViewLogger
{
    [ObservableProperty]
    public partial ObservableCollection<LogBoxEntry> LogBoxEntries { get; set; } = [];

    /// <summary>
    /// The current <see cref="LoggableObservableObject"/>.
    /// </summary>
    public static LoggableObservableObject CurrentLoggableObject { get; private set; } = null!;

    public ISnackbarService? SnackbarService { get; private set; } = null!;

    /* Configs */
    protected bool LogIconPrefixes = true;
    protected bool DisableFileLogging = true;

    private string indent = string.Empty;

    public void SetCurrentLogger()
    {
        CurrentLoggableObject = this;
    }

    public void WriteGenericLog(string text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None, bool newline = true)
    {
        WriteGenericLog(new StringBuilder(text), color, logType, newline);
    }

    public void WriteGenericLog(StringBuilder text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None, bool newline = false)
    {
        if (newline)
        {
            _ = text.AppendLine();
        }

        if (!string.IsNullOrEmpty(indent))
        {
            _ = text.Insert(0, indent);
        }

        LogBoxEntry newEntry = new()
        {
            Text = text.ToString(),
            Color = color,
            LogType = logType,
        };

        LogBoxEntries.Add(newEntry);
    }

    public void WriteGenericLogLine(string text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None)
    {
        WriteGenericLog(text, color, logType, newline: true);
    }

    public void WriteGenericLogLine(StringBuilder text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None)
    {
        WriteGenericLog(text, color, logType, newline: true);
    }

    public void Log(string log)
    {
        FileLogger.Log(log, DisableFileLogging);
        WriteGenericLogLine(log, logType: LogEntryType.Info);
    }

    public void LogSuccess(string log)
    {
        FileLogger.Log(log, DisableFileLogging);
        WriteGenericLogLine(log, Brushes.Lime, logType: LogEntryType.Success);
    }

    public void LogWarning(string log)
    {
        FileLogger.LogWarning(log, DisableFileLogging);
        WriteGenericLogLine(log, Brushes.Yellow, logType: LogEntryType.Warning);
    }

    public void LogError(string log)
    {
        FileLogger.LogError(log, DisableFileLogging);
        WriteGenericLogLine(log, Brushes.Red, logType: LogEntryType.Error);
    }

    public void LogError(Exception ex)
    {
        FileLogger.LogException(ex, DisableFileLogging);
        WriteGenericLog(ex.ToString(), Brushes.Red, logType: LogEntryType.Error);
    }

    public void LogDebug(string log)
    {
        FileLogger.LogDebug(log);

#if DEBUG
        WriteGenericLogLine(log, Brushes.Cyan, logType: LogEntryType.Debug);
#endif
    }

    public void LogDebug(Exception ex)
    {
        FileLogger.LogDebug(ex);

#if DEBUG
        WriteGenericLog($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", Brushes.Cyan, logType: LogEntryType.Debug);
#endif
    }

    public void AddIndent(char indenter = '\t')
    {
        indent = $"{indent}{indenter}";
    }

    public void AddIndentString(string indenter = "\t")
    {
        indent = $"{indent}{indenter}";
    }

    public void RemoveIndent()
    {
        indent = indent[..^1];
    }

    public void ResetIndent()
    {
        indent = string.Empty;
    }

    public void ClearLogs()
    {
        LogBoxEntries.Clear();
    }

    public void DisplaySnack(string header, string body, ControlAppearance appearance, int displayTimeMs = 10_000)
    {
        if (SnackbarService is null)
        {
            throw new InvalidOperationException("No Snackpresenter was set.");
        }

        if (body.Length is 0)
        {
            body = header;
        }

        SymbolIcon icon = new()
        {
            Symbol = appearance switch
            {
                ControlAppearance.Info => SymbolRegular.Info24,
                ControlAppearance.Success => SymbolRegular.CheckmarkCircle24,
                ControlAppearance.Caution => SymbolRegular.Warning24,
                ControlAppearance.Danger => SymbolRegular.ErrorCircle24,
                _ => SymbolRegular.Empty
            },
        };

        SnackbarService.Show(header, body, appearance, icon, TimeSpan.FromMilliseconds(displayTimeMs));
    }

    public void SnackInfo(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Info);
    }

    public void SnackSuccess(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Success);
    }

    public void SnackWarning(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Caution);
    }

    public void SnackError(string header, string body)
    {
        DisplaySnack(header, body, ControlAppearance.Danger);
    }

    public void SnackError(string body)
    {
        DisplaySnack("Error", body, ControlAppearance.Danger);
    }

    protected void SetSnackbarProvider(ISnackbarService _snackbarService)
    {
        SnackbarService = _snackbarService;
    }
}
