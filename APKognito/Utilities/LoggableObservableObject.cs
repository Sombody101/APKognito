using System.Runtime.InteropServices;
using System.Windows.Documents;

using Brush = System.Windows.Media.Brush;

namespace APKognito.Utilities;

/// <summary>
/// Gives a horrible interface for logging to a <see cref="RichTextBox"/> while still adhering to <see cref="ObservableObject"/> rules.
/// </summary>
public class LoggableObservableObject : ObservableObject, IAntiMvvmRtb
{
    private RichTextBox richTextBox = null!;

    public RichTextBox? RichTextBoxInUse => richTextBox;

    public Block RichTextLastBlock => richTextBox.Document.Blocks.LastBlock;

    public void WriteGenericLog(string text, [Optional] Brush color)
    {
        if (richTextBox is null)
        {
            return;
        }

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

    public void WriteGenericLogLine(string text, [Optional] Brush color)
    {
        WriteGenericLog($"{text}\n", color);
    }

    public void Log(string log)
    {
        FileLogger.Log(log);
        WriteGenericLog($"[INFO]    ~ {log}\n");
    }

    public void LogWarning(string log)
    {
        FileLogger.LogWarning(log);
        WriteGenericLog($"[WARNING] # {log}\n", Brushes.Yellow);
    }

    public void LogError(string log)
    {
        FileLogger.LogError(log);
        WriteGenericLog($"[ERROR]   ! {log}\n", Brushes.Red);
    }

    public void ClearLogs()
    {
        richTextBox.Dispatcher.Invoke(richTextBox.Document.Blocks.Clear);
    }

    public virtual void AntiMvvm_SetRichTextbox(RichTextBox rtb)
    {
        richTextBox = rtb;
    }
}
