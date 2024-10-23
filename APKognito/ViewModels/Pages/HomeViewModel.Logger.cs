using APKognito.Utilities;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Pages;

public partial class HomeViewModel
{
    private static readonly List<Run> _runLogBuffer = [];

    public static void WriteGenericLog(string text, [Optional] Brush color)
    {
        logBox.Dispatcher.Invoke(() =>
        {
            Run log = new(text)
            {
                FontFamily = firaRegular
            };

            if (color is not null)
            {
                log.Foreground = color;
            }

            if (logBox is null)
            {
                _runLogBuffer.Add(log);
                return;
            }

            ((Paragraph)logBox.Document.Blocks.LastBlock).Inlines.Add(log);
            logBox.ScrollToEnd();
        });
    }

    public static void Log(string log)
    {
        FileLogger.Log(log);
        WriteGenericLog($"[INFO]    ~ {log}\n");
    }

    public static void LogWarning(string log)
    {
        FileLogger.LogWarning(log);
        WriteGenericLog($"[WARNING] # {log}\n", Brushes.Yellow);
    }

    public static void LogError(string log)
    {
        FileLogger.LogError(log);
        WriteGenericLog($"[ERROR]   ! {log}\n", Brushes.Red);
    }

    public static void ClearLogs()
    {
        logBox.Dispatcher.Invoke(() =>
        {
            logBox.Document.Blocks.Clear();
        });
    }

    public void AntiMvvm_SetRichTextbox(RichTextBox rtb)
    {
        logBox = rtb;
        logBox.Document.FontFamily = firaRegular;

        // Dump all logs
        foreach (var run in _runLogBuffer)
        {
            ((Paragraph)logBox.Document.Blocks.LastBlock).Inlines.AddRange(_runLogBuffer);
            _runLogBuffer.Clear();
        }
    }
}
