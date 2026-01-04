using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Utilities.MVVM;
using CommunityToolkit.Mvvm.Messaging;
using Wpf.Ui.Controls;

namespace APKognito.ViewModels.Windows;

public partial class KognitoWindowViewModel : LoggableObservableObject
{
    [ObservableProperty]
    public partial WindowBackdropType WindowStyle { get; set; }

    public KognitoWindowViewModel()
    {
        // For designer
    }

    public KognitoWindowViewModel(ConfigurationFactory configFactory)
        : base(configFactory)
    {
        UserThemeConfig userThemeConfig = configFactory.GetConfig<UserThemeConfig>();

        WindowStyle = userThemeConfig.WindowStyle;

        WeakReferenceMessenger.Default.Register<UserThemeConfig>(this, (sender, config) =>
        {
            WindowStyle = config.WindowStyle;
        });
    }
}
