namespace APKognito.ApkLib.Automation;

public class CommandStageResult
{
    /// <summary>
    /// Files explicitly marked to not be regexed.
    /// </summary>
    public List<string> Exclusions { get; } = [];

    /// <summary>
    /// Files explicitly marked to be regexed.
    /// </summary>
    public List<string> Inclusions { get; } = [];

    public CommandStageResult()
    {
    }

    public void Append(CommandStageResult other)
    {
        Exclusions.AddRange(other.Exclusions);
        Inclusions.AddRange(other.Inclusions);
    }
}
