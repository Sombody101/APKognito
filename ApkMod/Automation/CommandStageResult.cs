namespace APKognito.ApkMod.Automation;

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

    private CommandStageResult(IEnumerable<string> exclusions, IEnumerable<string> inclusions)
    {
        Exclusions = [.. exclusions];
        Inclusions = [.. inclusions];
    }

    public static CommandStageResult operator +(CommandStageResult a, CommandStageResult b)
    {
        return new(
            a.Exclusions.Concat(b.Exclusions),
            a.Inclusions.Concat(b.Inclusions)
        );
    }
}
