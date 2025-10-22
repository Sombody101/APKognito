﻿using System.Runtime.InteropServices;
using System.Text;
using APKognito.Utilities;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui;

namespace APKognito.ViewModels.Windows;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public partial class ExceptionWindowViewModel : LoggableObservableObject
{
    private readonly IContentDialogService dialogService;

    #region Properties

    [ObservableProperty]
    public partial Exception ThrownException { get; set; }

    [ObservableProperty]
    public partial string ExceptionTypeName { get; set; }

    [ObservableProperty]
    public partial string ExceptionStackTrace { get; set; }

    [ObservableProperty]
    public partial bool IsFailure { get; set; }

    [ObservableProperty]
    public partial int Facility { get; set; }

    [ObservableProperty]
    public partial int ExceptionCode { get; set; }

    #endregion Properties

    public ExceptionWindowViewModel(IContentDialogService _dialogService)
    {
        dialogService = _dialogService;
    }

    #region Commands

    [RelayCommand]
    private async Task OnCreateLogpackAsync()
    {
        _ = await SettingsViewModel.CreateLogPackAsync(dialogService);
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

        StringBuilder exceptionBuffer = new();

        try
        {
            FileLogger.LogFatal("Fatal exception details:", exception);
        }
        catch (Exception ex)
        {
            exceptionBuffer.AppendLine($"[Failed to log exception to %APPDATA%\\applog.log]: File Log Exception Details:\n\tType:\t{ex.GetType().Name}\n\tReason:\t{ex.Message}\n");
        }

        exceptionBuffer.Append("[Main Exception Details]\n\tFailure: \t").AppendLine(IsFailure.ToString())
            .Append("\tFacility: \t0x").Append(Facility.ToString("x0")).Append(" (").Append(Facility).AppendLine(")")
            .Append("\tCode: \t0x").Append(ExceptionCode.ToString("x00")).Append(" (").Append(ExceptionCode).AppendLine(")")
            .Append(exception.GetType().Name).Append(": ").AppendLine(exception.Message)
            .AppendLine(exception.StackTrace);

        WriteGenericLogLine(exceptionBuffer.ToString());
    }
}
