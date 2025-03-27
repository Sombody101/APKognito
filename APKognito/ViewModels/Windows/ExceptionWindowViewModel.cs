using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using System.Runtime.InteropServices;

namespace APKognito.ViewModels.Windows;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public partial class ExceptionWindowViewModel : LoggableObservableObject
{
    private string exceptionDetails;

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

    #region Commands

    [RelayCommand]
    private static void OnCreateLogpack()
    {
        _ = SettingsViewModel.CreateLogPack();
    }

    [RelayCommand]
    private void OnOpenGithubIssue()
    {
        App.OpenHyperlink(this, new(new Uri("https://github.com/Sombody101/APKognito/issues/new/choose"), string.Empty));
    }

    [RelayCommand]
    private void OnJoinSupportDiscord()
    {
        App.OpenHyperlink(this, new(new Uri("https://discord.gg/rNR2VHySgF"), string.Empty));
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
            : $"{exception.GetType().Name}, caught by {exceptionSource}, from {exception.Source ?? "[NULL]"}";

        IsFailure = (exception.HResult & 0x80000000) is not 0;
        Facility = (exception.HResult & 0x7FFF0000) >> 16;
        ExceptionCode = exception.HResult & 0xFFFF;

        ExceptionStackTrace = exception.StackTrace ?? "[No stack trace]";

        try
        {
            FileLogger.LogFatal("Fatal exception details:");
            FileLogger.LogFatal(exception);
        }
        catch (Exception ex)
        {
            exceptionDetails = $"[Failed to log exception to %APPDATA%\\applog.log]: File Log Exception Details:\n\tType:\t{ex.GetType().Name}\n\tReason:\t{ex.Message}\n\n";
        }

        exceptionDetails = $"{exceptionDetails}[Main Exception Details]\n\tFailure: \t{IsFailure}\n\tFacility: \t0x{Facility:x0} ({Facility})\n\tCode: \t0x{ExceptionCode:x00} ({ExceptionCode})\n" +
            $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";

        WriteGenericLogLine(exceptionDetails);
    }
}