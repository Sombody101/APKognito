using APKognito.ViewModels.Windows;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.Views.Windows;

/// <summary>
/// Interaction logic for ExceptionWindow.xaml
/// </summary>
public partial class ExceptionWindow : FluentWindow
{
    public ExceptionWindowViewModel ViewModel { get; set; }

    public ExceptionWindow(ExceptionWindowViewModel exceptionViewModel)
    {
        DataContext = this;
        ViewModel = exceptionViewModel;
        
        InitializeComponent();

        exceptionViewModel.AntiMvvm_SetRichTextbox(ExceptionDetailsBox);

        SystemThemeWatcher.Watch(this);
        ApplicationAccentColorManager.ApplySystemAccent();

    }

    public static bool? CreateNewExceptionWindow(Exception exception, IHost host, [Optional] string exceptionSource)
    {
        ExceptionWindowViewModel evm = (ExceptionWindowViewModel)host.Services.GetService(typeof(ExceptionWindowViewModel))!;
        ExceptionWindow exceptionWindow = new(evm);
        evm.SetException(exception.InnerException ?? exception, exceptionSource);
        
        return exceptionWindow.ShowDialog();
    }
}
