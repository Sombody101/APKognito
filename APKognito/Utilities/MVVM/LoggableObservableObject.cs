using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Documents;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;

namespace APKognito.Utilities.MVVM;

/// <summary>
/// Gives a horrible interface for logging to a <see cref="RichTextBox"/> while still adhering to <see cref="ObservableObject"/> rules.
/// </summary>
public class LoggableObservableObject : ViewModel, IAntiMvvmRtb, IViewable
{
    /// <summary>
    /// The current <see cref="LoggableObservableObject"/>.
    /// </summary>
    public static LoggableObservableObject CurrentLoggableObject { get; private set; } = null!;
    public void SetCurrentLogger()
    {
        CurrentLoggableObject = this;
    }

    /* Log box */
    private RichTextBox richTextBox = null!;

    public RichTextBox? RichTextBoxInUse => richTextBox;
    public Block RichTextLastBlock => richTextBox.Document.Blocks.LastBlock;

    /* Snackbar */
    private ISnackbarService snackbarService = null!;
    public ISnackbarService? SnackbarService => snackbarService;

    /* Configs */
    protected bool LogIconPrefixes = true;
    protected bool DisableFileLogging = true;

    private string indent = string.Empty;

    private List<(string log, Brush? color, LogType? type)>? logBuffer = [];
    public void WriteGenericLog(string text, [Optional] Brush color, LogType? logType = LogType.None, bool newline = true)
    {
        WriteGenericLog(new StringBuilder(text), color, logType, newline);
    }

    public void WriteGenericLog(StringBuilder text, [Optional] Brush color, LogType? logType = LogType.None, bool newline = false)
    {
        if (newline)
        {
            text.AppendLine();
        }

        if (indent != string.Empty)
        {
            text.Insert(0, indent);
        }

        if (richTextBox is null)
        {
            logBuffer?.Add(new(text.ToString(), color, logType));
            return;
        }

        ClearBuffedLogs();
        AppendRunToLogbox(text.ToString(), color, logType);
    }

    public void WriteGenericLogLine(string text, [Optional] Brush color, LogType? logType = LogType.None)
    {
        WriteGenericLog(text, color, logType, newline: true);
    }

    public void WriteGenericLogLine(StringBuilder text, [Optional] Brush color, LogType? logType = LogType.None)
    {
        WriteGenericLog(text, color, logType, newline: true);
    }

    public void Log(string log)
    {
        FileLogger.Log(log, DisableFileLogging);
        WriteGenericLogLine(log, logType: LogType.Info);
    }

    public void LogSuccess(string log)
    {
        FileLogger.Log(log, DisableFileLogging);
        WriteGenericLogLine(log, Brushes.Green, logType: LogType.Success);
    }

    public void LogWarning(string log)
    {
        FileLogger.LogWarning(log, DisableFileLogging);
        WriteGenericLogLine(log, Brushes.Yellow, logType: LogType.Warning);
    }

    public void LogError(string log)
    {
        FileLogger.LogError(log, DisableFileLogging);
        WriteGenericLogLine(log, Brushes.Red, logType: LogType.Error);
    }

    public void LogError(Exception ex)
    {
        FileLogger.LogException(ex, DisableFileLogging);
        WriteGenericLog(ex.ToString(), Brushes.Red, logType: LogType.Error);
    }

    public void LogDebug(string log)
    {
        FileLogger.LogDebug(log);

#if DEBUG
        WriteGenericLogLine(log, Brushes.Cyan, logType: LogType.Debug);
#endif
    }

    public void LogDebug(Exception ex)
    {
        FileLogger.LogDebug(ex);

#if DEBUG
        WriteGenericLog($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", Brushes.Cyan, logType: LogType.Debug);
#endif
    }

    public void AddIndent()
    {
        indent = $"{indent}\t";
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
        richTextBox.Dispatcher.Invoke(() =>
        {
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph());
        });
    }

    public async Task ClearLogsAsync()
    {
        await richTextBox.Dispatcher.InvokeAsync(() =>
        {
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph());
        });
    }

    public virtual void AntiMvvm_SetRichTextbox(RichTextBox rtb)
    {
        richTextBox = rtb;
        ClearBuffedLogs();
    }

    protected void SetSnackbarProvider(ISnackbarService _snackbarService)
    {
        snackbarService = _snackbarService;
    }

    public void DisplaySnack(string header, string body, ControlAppearance appearance, int displayTimeMs = 10_000)
    {
        if (snackbarService is null)
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

        snackbarService.Show(header, body, appearance, icon, TimeSpan.FromMilliseconds(displayTimeMs));
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

    private void AppendRunToLogbox(string text, [Optional] Brush? color, LogType? logType)
    {
        richTextBox.Dispatcher.BeginInvoke(() =>
        {
            Run log = new(text);

            if (color is not null)
            {
                log.Foreground = color;
            }

            Paragraph p = (Paragraph)RichTextLastBlock;

            if (LogIconPrefixes && logType is not (null or LogType.None))
            {
                // Everything in this block is just to fix the inconsistent bum-fuckery sizing in the SymbolIcons

                SymbolRegular symbol = SymbolRegular.Empty;
                double height = 16;
                Thickness margin = new(0, 0, 5, 0);

                switch (logType)
                {
                    case LogType.Info:
                        symbol = SymbolRegular.Info16;
                        height = 14;
                        margin.Left = 1;
                        break;

                    case LogType.Success:
                        symbol = SymbolRegular.CheckmarkCircle16;
                        break;

                    case LogType.Warning:
                        symbol = SymbolRegular.Warning16;
                        break;

                    case LogType.Error:
                        symbol = SymbolRegular.ErrorCircle16;
                        height = 15.5;
                        break;

                    case LogType.Debug:
                        symbol = SymbolRegular.Bug16;
                        break;
                }

                var icon = new SymbolIcon()
                {
                    Symbol = symbol,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    FontSize = height,
                    Margin = margin
                };

                if (color is not null)
                {
                    icon.Foreground = color;
                }

                p.Inlines.Add(icon);
            }

            p.Inlines.Add(log);
            richTextBox.ScrollToEnd();
        });
    }

    private void ClearBuffedLogs()
    {
        if (logBuffer is null)
        {
            return;
        }

        foreach ((string bLog, Brush? bColor, LogType? bType) in logBuffer)
        {
            AppendRunToLogbox(bLog, bColor, bType);
        }

        logBuffer.Clear();
        logBuffer = null;
    }

    public enum LogType
    {
        None,
        Info,
        Success,
        Warning,
        Error,
        Debug,
    }
}
