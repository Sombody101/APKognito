namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adb-history.bin", ConfigType.Bson, ConfigModifiers.Compressed)]
internal sealed record AdbHistory : IKognitoConfig
{
    public List<string> CommandHistory { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = GetDefaultVariables();

    public AdbHistory()
    {
    }

    public AdbHistory(List<string> commandHistory, Dictionary<string, string> variables)
    {
        CommandHistory = commandHistory;
        Variables = variables ?? GetDefaultVariables();
    }

    public string GetVariable(string variableName)
    {
        return Variables.TryGetValue(variableName, out string? value) 
            ? value 
            : string.Empty;
    }

    public void SetVariable(string variableName, string? variableValue)
    {
        variableValue ??= string.Empty; // Just added security

        Variables[variableName] = variableValue;
    }

    private static Dictionary<string, string> GetDefaultVariables()
    {
        return new()
        {
            { "PS1", "> " }
        };
    }
}
