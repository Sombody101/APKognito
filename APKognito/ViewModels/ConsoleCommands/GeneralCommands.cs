using System.Diagnostics;
using System.Text;
using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Helpers;
using APKognito.Utilities.MVVM;
using static APKognito.ViewModels.Pages.AdbConsoleViewModel;

namespace APKognito.ViewModels.ConsoleCommands;

internal static class GeneralCommands
{
    [Command("help", "Prints this help information.")]
    public static void GetHelpInfoCommand(IViewLogger logger, List<CommandInfo> commands)
    {
        StringBuilder output = new();

        int longestName = commands.Max(c => c.CommandName.Length);

        foreach (CommandInfo? command in commands.Where(c => c.IsVisible))
        {
            _ = output.Append(':')
                .Append(command.CommandName.PadRight(longestName))
                .Append(' ');

            _ = command.CommandUsage.Length is not 0
                ? output.AppendLine(command.CommandUsage)
                    .Append('\t')
                : output.AppendLine("(no parameters)\t");

            _ = output.AppendLine(command.HelpInfo).AppendLine();
        }

        logger.WriteGenericLogLine(output.ToString());
    }

    [Command("clear", "Clears the log buffer.")]
    public static void ClearLogsCommand(IViewLogger logger)
    {
        logger.ClearLogs();
    }

    [Command("set",
    "Sets and saves a custom cmdlet. Use '--list' to see all currently set cmdlets.",
    "[--list] || <command name> <command body>...")]
    public static void SetCommandletCommand(ParsedCommand ctx, IViewLogger logger, ConfigurationFactory configFactory)
    {
        AdbConfig adbConfig = configFactory.GetConfig<AdbConfig>();

        if (ctx.ArgCount is 0 || ctx[0] == "--list")
        {
            if (adbConfig.UserCmdlets.Count is 0)
            {
                logger.Log("No cmdlets set.");
                return;
            }

            foreach (KeyValuePair<string, string> command in adbConfig.UserCmdlets)
            {
                logger.Log($"{command.Key}: {command.Value}");
            }

            return;
        }

        if (ctx.ArgCount is 0)
        {
            logger.LogError("No cmdlet name or body supplied.");
            return;
        }

        if (ctx.ArgCount is 1)
        {
            logger.LogError("No cmdlet body supplied.");
            return;
        }

        string cmdlet = ctx[0]!;
        adbConfig.UserCmdlets[cmdlet] = string.Join(' ', ctx[1..]);
        logger.Log($"Cmdlet '::{cmdlet}' created.");
        configFactory.SaveConfig(adbConfig);
    }

    [Command("unset", "Removes a custom cmdlet.", "<cmdlet name>")]
    public static void RemoveCommandletCommand(ParsedCommand ctx, IViewLogger logger, ConfigurationFactory configFactory)
    {
        if (ctx.ArgCount > 1)
        {
            logger.LogError("Too many arguments.");
            return;
        }

        if (ctx.ArgCount is not 1)
        {
            logger.LogError("No cmdlet name supplied.");
            return;
        }

        string cmdlet = ctx[0] ?? "[NO CMDLET]";
        AdbConfig adbConfig = configFactory.GetConfig<AdbConfig>();

        if (adbConfig.UserCmdlets.Remove(cmdlet))
        {
            logger.Log($"Removed cmdlet '{cmdlet}'");
            configFactory.SaveConfig(adbConfig);
        }
        else
        {
            logger.LogError($"Commandlet '{cmdlet}' not defined, no cmdlet removed.");
        }
    }

    [Command("echo", "Prints all arguments to the console.", "[text ...]")]
    public static void EchoCommand(ParsedCommand ctx, IViewLogger logger)
    {
        logger.Log(string.Join(' ', ctx.Args));
    }

    [Command("vars", "Prints all set variables.", NO_USAGE)]
    public static void PrintAllVariablesCommand(IViewLogger logger, ConfigurationFactory configFactory)
    {
        var adbHistory = configFactory.GetConfig<AdbHistory>();

        if (adbHistory.Variables.Count is 0)
        {
            logger.Log("There are no set variables.");
            return;
        }

        StringBuilder builder = new();
        foreach (KeyValuePair<string, string> pair in adbHistory.Variables)
        {
            _ = builder.Append(pair.Key).Append("=\'").Append(pair.Value).AppendLine("'");
        }

        logger.Log(builder.ToString());
    }

    [Command(VARIABLE_SETTER)]
    public static void SetVariableCommand(ParsedCommand ctx, ConfigurationFactory configFactory)
    {
        // Args 0: Variable name
        // Args 1: Variable value
        configFactory.GetConfig<AdbHistory>().SetVariable(ctx.Args[0], ctx.Args[1]);
    }

    [Command("sys")]
    public static void SysInternalCommand(ParsedCommand ctx, IViewLogger logger)
    {
        if (ctx.ArgCount is not 1)
        {
            logger.LogError("Invalid argument count.");
            return;
        }

        string option = ctx.Args[0];
        switch (option)
        {
            case "get_heap":
                logger.Log($"GC: {GBConverter.FormatSizeFromBytes(GC.GetTotalMemory(false))}");
                logger.Log($"Private: {GBConverter.FormatSizeFromBytes(Process.GetCurrentProcess().PrivateMemorySize64)}");
                return;

            default:
                logger.LogError($"Unknown sys option '{option}'");
                return;
        }
    }
}
