using System.Collections.ObjectModel;
using APKognito.Utilities.MVVM;

namespace APKognito.ViewModels.Windows;

public sealed partial class SetupWizardViewModel : ViewModel, IViewable
{
    [ObservableProperty]
    public partial ObservableCollection<object> MenuItems { get; set; } = [];
}
