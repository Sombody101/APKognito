using System.Runtime.InteropServices;
using System.Windows.Documents;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;

namespace APKognito.Utilities;

/// <summary>
/// Gives a horrible interface for logging to a <see cref="RichTextBox"/> while still adhering to <see cref="ObservableObject"/> rules.
/// </summary>
public class LoggableObservableObject : ObservableObject, IAntiMvvmRtb
{
    /* Log box */
    private RichTextBox richTextBox = null!;
    public RichTextBox? RichTextBoxInUse => richTextBox;
    public Block RichTextLastBlock => richTextBox.Document.Blocks.LastBlock;

    /* Snackbar */
    private ISnackbarService snackbarService = null!;
    public ISnackbarService? SnackbarService => snackbarService;

    private List<(string, Brush?)>? logBuffer = [];
    public void WriteGenericLog(string text, [Optional] Brush color)
    {
        if (richTextBox is null)
        {
            logBuffer?.Add(new(text, color));
            return;
        }

        if (logBuffer is not null)
        {
            foreach (var logPair in logBuffer)
            {
                AppendRunToLogbox(logPair.Item1, logPair.Item2);
            }

            logBuffer.Clear();
            logBuffer = null;
        }

        AppendRunToLogbox(text, color);
    }

    public void WriteGenericLogLine(string text, [Optional] Brush color)
    {
        WriteGenericLog($"{text}\n", color);
    }

    protected string LogPrefix { get; set; } = "[INFO]    ~ ";
    public void Log(string log)
    {
        FileLogger.Log(log);
        WriteGenericLog($"{LogPrefix}{log}\n");
    }

    protected string LogSuccessPrefix { get; set; } = "[SUCCESS] @ ";
    public void LogSuccess(string log)
    {
        FileLogger.Log(log);
        WriteGenericLog($"{LogSuccessPrefix}{log}\n", Brushes.Green);
    }

    protected string LogWarningPrefix { get; set; } = "[WARNING] # ";
    public void LogWarning(string log)
    {
        FileLogger.LogWarning(log);
        WriteGenericLog($"{LogWarningPrefix}{log}\n", Brushes.Yellow);
    }

    protected string LogErrorPrefix { get; set; } = "[ERROR]   ! ";
    public void LogError(string log)
    {
        FileLogger.LogError(log);
        WriteGenericLog($"{LogErrorPrefix}{log}\n", Brushes.Red);
    }

#if DEBUG

    public void LogDebug(string log)
    {
        FileLogger.LogDebug(log);
        WriteGenericLog($"[DEBUG]   & {log}\n", Brushes.Cyan);
    }

#endif

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

    private void AppendRunToLogbox(string text, [Optional] Brush? color)
    {
        richTextBox.Dispatcher.Invoke(() =>
        {
            Run log = new(text);

            if (color is not null)
            {
                log.Foreground = color;
            }

            ((Paragraph)RichTextLastBlock).Inlines.Add(log);
            richTextBox.ScrollToEnd();
        });
    }
}
