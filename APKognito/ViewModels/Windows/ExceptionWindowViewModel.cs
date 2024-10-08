using APKognito.Utilities;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Documents;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Windows;

public partial class ExceptionWindowViewModel : ObservableObject, IAntiMvvmRTB
{
    private RichTextBox _exceptionBox;

    #region Properties

    [ObservableProperty]
    private Exception _thrownException;

    [ObservableProperty]
    private string _exceptionTypeName;

    [ObservableProperty]
    private string _exceptionStackTrace;

    [ObservableProperty]
    private bool _isFailure;

    [ObservableProperty]
    private int _facility;

    [ObservableProperty]
    private int _exceptionCode;

    #endregion Properties

    private string exceptionDetails;

    #region Commands

    [RelayCommand]
    private void OnCopyExceptionDetails()
    {
        try
        {
            Clipboard.Clear();
            Clipboard.SetText(exceptionDetails);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    [RelayCommand]
    private void OnOpenGithubIssue()
    {
        App.OpenHyperlink(this, new(new Uri("https://github.com/Sombody101/APKognito/issues"), string.Empty));
    }

    [RelayCommand]
    private void OnExitApkognito()
    {
        Environment.Exit(ExceptionCode);
    }

    #endregion Commands

    public void SetException(Exception exception, [Optional] string exceptionSource)
    {
        ThrownException = exception;
        ExceptionTypeName = string.IsNullOrWhiteSpace(exceptionSource)
            ? exception.GetType().Name
            : $"{exception.GetType().Name}, from {exceptionSource} ({exception.Source})";

        IsFailure = (exception.HResult & 0x80000000) is not 0;
        Facility = (exception.HResult & 0x7FFF0000) >> 16;
        ExceptionCode = exception.HResult & 0xFFFF;

        ExceptionStackTrace = exception.StackTrace ?? "[No stack trace]";

        exceptionDetails = $"Failure: \t{IsFailure}\nFacility: \t0x{Facility:x0} ({Facility})\nCode: \t0x{ExceptionCode:x00} ({ExceptionCode})\n{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
        ((Paragraph)_exceptionBox.Document.Blocks.LastBlock).Inlines.Add(exceptionDetails);
        _exceptionBox.ScrollToEnd();
    }

    public void AntiMvvm_SetRichTextbox(RichTextBox rtb)
    {
        _exceptionBox = rtb;
    }
}
