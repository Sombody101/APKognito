namespace APKognito.Configurations.ConfigModels;

[ConfigFile("rename-settings.json", ConfigType.Json, ConfigModifiers.JsonIndented | ConfigModifiers.JsonIgnoreMissing)]
public sealed record class _RenameConfiguration : IKognitoConfig
{

}
