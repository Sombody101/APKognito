using System.Runtime.InteropServices;
using APKognito.Utilities;
using APKognito.ViewModels.Windows;

namespace APKognito.Views.Windows;

/// <summary>
/// Interaction logic for ExceptionWindow.xaml
/// </summary>
public partial class ExceptionWindow
{
    public ExceptionWindowViewModel ViewModel { get; set; }

#if DEBUG
    public ExceptionWindow()
        : this(new())
    {
        // For designer
        ViewModel.SetException(new DebugOnlyException());
    }
#endif

    public ExceptionWindow(ExceptionWindowViewModel exceptionViewModel)
    {
        ViewModel = exceptionViewModel;
        DataContext = this;

        InitializeComponent();
    }

    public static bool? CreateNewExceptionWindow(Exception exception, ExceptionWindowViewModel evm, [Optional] string exceptionSource)
    {
        ExceptionWindow exceptionWindow = new(evm);
        evm.SetException(exception.InnerException ?? exception, exceptionSource);

        return exceptionWindow.ShowDialog();
    }
}
