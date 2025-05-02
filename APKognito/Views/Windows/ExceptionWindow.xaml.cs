using APKognito.Utilities;
using APKognito.ViewModels.Windows;
using System.Runtime.InteropServices;
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
        ViewModel = exceptionViewModel;
        DataContext = this;

        InitializeComponent();

        SystemThemeWatcher.Watch(this);
        ApplicationAccentColorManager.ApplySystemAccent();
    }

#if DEBUG
    public ExceptionWindow()
        : this(null!)
    {
        // For designer

        ViewModel.SetException(new DebugOnlyException());
    }
#endif

    public static bool? CreateNewExceptionWindow(Exception exception, ExceptionWindowViewModel evm, [Optional] string exceptionSource)
    {
        ExceptionWindow exceptionWindow = new(evm);
        evm.SetException(exception.InnerException ?? exception, exceptionSource);

        return exceptionWindow.ShowDialog();
    }
}