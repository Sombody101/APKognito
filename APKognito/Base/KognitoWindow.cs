using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.Base;

public class KognitoWindow : FluentWindow
{
    protected KognitoWindow()
    {
        // For designer
    }

    public KognitoWindow(ConfigurationFactory configFactory)
    {
        UserThemeConfig themeManager = configFactory.GetConfig<UserThemeConfig>();
        ApplicationThemeManager.Apply(themeManager.AppTheme, WindowBackdropType.None, false);

        if (themeManager.UseSystemAccent)
        {
            ApplicationAccentColorManager.ApplySystemAccent();
            SystemThemeWatcher.Watch(this);
        }
    }
}
