using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using static APKognito.Models.LogBoxEntry;

namespace APKognito.Utilities.MVVM;

public interface IViewLogger : ILogger
{
    /// <summary>
    /// Writes a log.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="color">The color to be displayed</param>
    /// <param name="logType">The type of log. This determines the icon to be presented. Use <see cref="LogEntryType.None"/> for no icons.</param>
    /// <param name="newline"></param>
    public void WriteGenericLog(string text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None, bool newline = true);

    /// <summary>
    /// Writes a log.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="color">The color to be displayed</param>
    /// <param name="logType">The type of log. This determines the icon to be presented. Use <see cref="LogEntryType.None"/> for no icons.</param>
    /// <param name="newline"></param>
    public void WriteGenericLog(StringBuilder text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None, bool newline = false);

    /// <summary>
    /// Writes a log,
    /// </summary>
    /// <param name="text"></param>
    /// <param name="color">The color to be displayed</param>
    /// <param name="logType">The type of log. This determines the icon to be presented. Use <see cref="LogEntryType.None"/> for no icons.</param>
    public void WriteGenericLogLine(string text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None);

    /// <summary>
    /// Writes a log,
    /// </summary>
    /// <param name="text"></param>
    /// <param name="color">The color to be displayed</param>
    /// <param name="logType">The type of log. This determines the icon to be presented. Use <see cref="LogEntryType.None"/> for no icons.</param>
    public void WriteGenericLogLine(StringBuilder text, [Optional] Brush color, LogEntryType? logType = LogEntryType.None);

    /// <summary>
    /// Writes an informational log. (Default text color)
    /// </summary>
    /// <param name="log"></param>
    public void Log(string log);

    /// <summary>
    /// Logs a successful log. (Green)
    /// </summary>
    /// <param name="log"></param>
    public void LogSuccess(string log);

    /// <summary>
    /// Logs a warning. (Yellow)
    /// </summary>
    /// <param name="log"></param>
    public void LogWarning(string log);

    /// <summary>
    /// Logs an error. (Red)
    /// </summary>
    /// <param name="log"></param>
    public void LogError(string log);

    /// <summary>
    /// Logs an error via an exception message. (Red)
    /// </summary>
    /// <param name="ex"></param>
    public void LogError(Exception ex);

    /// <summary>
    /// Logs information via the debug icon. (Cyan)
    /// </summary>
    /// <param name="log"></param>
    public void LogDebug(string log);

    /// <summary>
    /// Logs an exception (stack trace and message) via the debug icon. (Cyan)
    /// </summary>
    /// <param name="ex"></param>
    public void LogDebug(Exception ex);

    /// <summary>
    /// Clears all logs.
    /// </summary>
    public void ClearLogs();

    /// <summary>
    /// Displays a WPF-UI snack.
    /// <para>
    /// This will only work if the implementor sets a valid <see cref="Wpf.Ui.ISnackbarService"/>.
    /// </para>
    /// </summary>
    /// <param name="header"></param>
    /// <param name="body"></param>
    /// <param name="appearance"></param>
    /// <param name="displayTimeMs"></param>
    public void DisplaySnack(string header, string body, ControlAppearance appearance, int displayTimeMs = 10_000);

    /// <summary>
    /// Displays a WPF-UI informational snack. (Blue)
    /// </summary>
    /// <para>
    /// This will only work if the implementor sets a valid <see cref="Wpf.Ui.ISnackbarService"/>.
    /// </para>
    /// <param name="header"></param>
    /// <param name="body"></param>
    public void SnackInfo(string header, string body);

    /// <summary>
    /// Displays a successful snack. (Green)
    /// </summary>
    /// <para>
    /// This will only work if the implementor sets a valid <see cref="Wpf.Ui.ISnackbarService"/>.
    /// </para>
    /// <param name="header"></param>
    /// <param name="body"></param>
    public void SnackSuccess(string header, string body);

    /// <summary>
    /// Displays a warning snack. (Yellow)
    /// </summary>
    /// <para>
    /// This will only work if the implementor sets a valid <see cref="Wpf.Ui.ISnackbarService"/>.
    /// </para>
    /// <param name="header"></param>
    /// <param name="body"></param>
    public void SnackWarning(string header, string body);

    /// <summary>
    /// Displays an error snack. (Red)
    /// </summary>
    /// <para>
    /// This will only work if the implementor sets a valid <see cref="Wpf.Ui.ISnackbarService"/>.
    /// </para>
    /// <param name="header"></param>
    /// <param name="body"></param>
    public void SnackError(string header, string body);

    /// <summary>
    /// Displays an error snack. (Red)
    /// <para>
    /// This sets the header to "Error", the body is still customizable.
    /// </para>
    /// <para>
    /// This will only work if the implementor sets a valid <see cref="Wpf.Ui.ISnackbarService"/>.
    /// </para>
    /// </summary>
    /// <param name="body"></param>
    public void SnackError(string body);

    public void WriteImage(WPFUI.Controls.Image image);

    public void WriteImage(System.Windows.Controls.Image image);
}
