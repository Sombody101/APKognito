using Newtonsoft.Json;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("usertheme.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
public sealed record UserThemeConfig : IKognitoConfig
{
    [JsonProperty("theme")]
    public ApplicationTheme AppTheme { get; set; } = ApplicationThemeManager.GetAppTheme();

    [JsonProperty("use_system_accent")]
    public bool UseSystemAccent { get; set; } = true;

    [JsonProperty("window_style")]
    public WindowBackdropType WindowStyle { get; set; } = WindowBackdropType.Mica;

    public void ApplyUserTheme()
    {
        if (UseSystemAccent)
        {
            ApplicationAccentColorManager.ApplySystemAccent();
        }
        else
        {
            ApplicationAccentColorManager.Apply(ApplicationAccentColorManager.GetColorizationColor());
            ApplicationThemeManager.Apply(AppTheme);
        }
    }
}
