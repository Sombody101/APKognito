using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;

namespace APKognito.Utilities;

/// <summary>
/// Gives a horrible interface for logging to a <see cref="RichTextBox"/> while still adhering to <see cref="ObservableObject"/> rules.
/// </summary>
public class LoggableObservableObject : PageSizeTracker, IAntiMvvmRtb
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

    private List<(string, Brush?, LogType?)>? logBuffer = [];
    public void WriteGenericLog(string text, [Optional] Brush color, LogType? logType = LogType.None)
    {
        if (richTextBox is null)
        {
            logBuffer?.Add(new(text, color, logType));
            return;
        }

        if (logBuffer is not null)
        {
            foreach (var logPair in logBuffer)
            {
                AppendRunToLogbox(logPair.Item1, logPair.Item2, logType);
            }

            logBuffer.Clear();
            logBuffer = null;
        }

        AppendRunToLogbox(text, color, logType);
    }

    public void WriteGenericLogLine(string text, [Optional] Brush color)
    {
        WriteGenericLog($"{text}\n", color);
    }

    public void Log(string log)
    {
        FileLogger.Log(log);
        WriteGenericLog($"{log}\n", logType: LogType.Info);
    }

    public void LogSuccess(string log)
    {
        FileLogger.Log(log);
        WriteGenericLog($"{log}\n", Brushes.Green, logType: LogType.Success);
    }

    public void LogWarning(string log)
    {
        FileLogger.LogWarning(log);
        WriteGenericLog($"{log}\n", Brushes.Yellow, logType: LogType.Warning);
    }

    public void LogError(string log)
    {
        FileLogger.LogError(log);
        WriteGenericLog($"{log}\n", Brushes.Red, logType: LogType.Error);
    }

    [Conditional("DEBUG")]
    public void LogDebug(string log)
    {
        FileLogger.LogDebug(log);
        WriteGenericLog($"{log}\n", Brushes.Cyan, logType: LogType.Debug);
    }

    public void ClearLogs()
    {
        richTextBox.Dispatcher.Invoke(() =>
        {
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph());
        });
    }

    public virtual void AntiMvvm_SetRichTextbox(RichTextBox rtb)
    {
        richTextBox = rtb;
    }

    protected void SetSnackbarProvider(ISnackbarService _snackbarService)
    {
        snackbarService = _snackbarService;
    }

    public void DisplaySnack(string header, string body, ControlAppearance appearance, int time_ms = 10_000)
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

        snackbarService.Show(header, body, appearance, icon, TimeSpan.FromMilliseconds(time_ms));
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

    public enum LogType
    {
        None,
        Info,
        Success,
        Warning,
        Error,
        Debug,
    }

    private static readonly SymbolIcon SymbolInfo = new()
    {
        Symbol = SymbolRegular.Info16,
        FontSize = 14,
        Margin = new(1, 0, 0, 0)
    };

    private static readonly SymbolIcon SymbolSuccess = new()
    {
        Symbol = SymbolRegular.CheckmarkCircle16,
    };

    private static readonly SymbolIcon SymbolWarning = new()
    {
        Symbol = SymbolRegular.Warning16,
    };

    private static readonly SymbolIcon SymbolError = new()
    {
        Symbol = SymbolRegular.ErrorCircle16,
        FontSize = 15.5,
    };

    private static readonly SymbolIcon SymbolDebug = new()
    {
        Symbol = SymbolRegular.Bug16,
    };
}
