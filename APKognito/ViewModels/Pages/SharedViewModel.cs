using APKognito.Utilities.MVVM;

namespace APKognito.ViewModels.Pages;

public sealed partial class SharedViewModel : ViewModel, IViewable
{
    [ObservableProperty]
    public partial bool ConfigurationControlsEnabled { get; set; } = true;
}
