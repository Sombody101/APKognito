using APKognito.Utilities.MVVM;
using System.IO;

namespace APKognito.ApkMod.Automation.CommandBindings;

internal static partial class CommandBindings
{
    [Command("exclude", CommandAttribute.ANY, FileAccess.Read)]
    public static CommandStageResult ExcludeFile(string[] targets, IViewLogger logger)
    {
        CommandStageResult result = new();

        if (targets.Length is 0)
        {
            return result;
        }

        foreach (string target in targets)
        {
            logger.Log($"Excluding regex target: {target}");
            result.Exclusions.Add(target);
        }

        return result;
    }

    [Command("include", CommandAttribute.ANY, FileAccess.Read)]
    public static CommandStageResult IncludeFile(string[] targets, IViewLogger logger)
    {
        CommandStageResult result = new();

        if (targets.Length is 0)
        {
            return result;
        }

        foreach (string target in targets)
        {
            logger.Log($"Including regex target: {target}");
            result.Inclusions.Add(target);
        }

        return result;
    }
}
