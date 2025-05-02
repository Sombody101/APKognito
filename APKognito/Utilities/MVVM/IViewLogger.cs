using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using Wpf.Ui.Controls;
using static APKognito.Models.LogBoxEntry;

namespace APKognito.Utilities.MVVM;

public interface IViewLogger
{
    public void WriteGenericLog(string text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None, bool newline = true);

    public void WriteGenericLog(StringBuilder text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None, bool newline = false);

    public void WriteGenericLogLine(string text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None);

    public void WriteGenericLogLine(StringBuilder text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None);

    public void Log(string log);

    public void LogSuccess(string log);

    public void LogWarning(string log);

    public void LogError(string log);

    public void LogError(Exception ex);

    public void LogDebug(string log);

    public void LogDebug(Exception ex);

    public void AddIndent(char indentor = '\t');

    public void AddIndentString(string indentor = "\t");

    public void RemoveIndent();

    public void ResetIndent();

    public void ClearLogs();

    public void DisplaySnack(string header, string body, ControlAppearance appearance, int displayTimeMs = 10_000);

    public void SnackInfo(string header, string body);

    public void SnackSuccess(string header, string body);

    public void SnackWarning(string header, string body);

    public void SnackError(string header, string body);

    public void SnackError(string body);
}
