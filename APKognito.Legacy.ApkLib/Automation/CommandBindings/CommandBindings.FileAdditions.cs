using Microsoft.Extensions.Logging;

namespace APKognito.Legacy.ApkLib.Automation.CommandBindings;

internal static partial class CommandBindings
{
    [Command("exclude", CommandAttribute.ANY, FileAccess.Read)]
    public static CommandStageResult ExcludeFile(string[] targets, ILogger logger)
    {
        CommandStageResult result = new();

        if (targets.Length is 0)
        {
            return result;
        }

        foreach (string target in targets)
        {
            logger.LogInformation("Excluding regex target: {Target}", target);
            result.Exclusions.Add(target);
        }

        return result;
    }

    [Command("include", CommandAttribute.ANY, FileAccess.Read)]
    public static CommandStageResult IncludeFile(string[] targets, ILogger logger)
    {
        CommandStageResult result = new();

        if (targets.Length is 0)
        {
            return result;
        }

        foreach (string target in targets)
        {
            logger.LogInformation("Including regex target: {Target}", target);
            result.Inclusions.Add(target);
        }

        return result;
    }
}
