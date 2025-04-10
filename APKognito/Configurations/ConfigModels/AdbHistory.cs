using MemoryPack;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("adb-history.bin", ConfigType.MemoryPacked, ConfigModifiers.MemoryPacked | ConfigModifiers.Compressed)]
[MemoryPackable]
internal partial class AdbHistory : IKognitoConfig
{
    public List<string> CommandHistory { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = GetDefaultVariables();

    public AdbHistory()
    {
    }

    [MemoryPackConstructor]
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