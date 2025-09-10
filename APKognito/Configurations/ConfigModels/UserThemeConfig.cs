using Newtonsoft.Json;
using Wpf.Ui.Appearance;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("usertheme.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
public sealed record UserThemeConfig : IKognitoConfig
{
    [JsonProperty("theme")]
    public ApplicationTheme AppTheme { get; set; } = ApplicationThemeManager.GetAppTheme();

    [JsonProperty("use_system_accent")]
    public bool UseSystemAccent { get; set; } = true;

    public void ApplyUserTheme()
    {
        ApplicationThemeManager.Apply(AppTheme);

        if (UseSystemAccent)
        {
            ApplicationAccentColorManager.ApplySystemAccent();
        }
    }
}
