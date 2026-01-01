using System.Collections.ObjectModel;
using System.Reflection;
using APKognito.Utilities.MVVM;
using static APKognito.ViewModels.Pages.AdbConsoleViewModel;

namespace APKognito.ViewModels.ConsoleCommands;

public static class CommandRegistrar
{
    private const string COMMAND_NAMESPACE = "APKognito.ViewModels.ConsoleCommands";

    private static List<CommandInfo>? s_commands;

    internal static ReadOnlyCollection<CommandInfo> GetCommands()
    {
        if (s_commands is null)
        {
            RegisterCommands();
        }

        return s_commands!.AsReadOnly();
    }

    public static CommandHost CreateHost()
    {
        return new CommandHost(GetCommands());
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "It's okay here.")]
    private static void RegisterCommands()
    {
        IEnumerable<Type> types = Assembly.GetExecutingAssembly().GetTypes().Where(t => string.Equals(t.Namespace, COMMAND_NAMESPACE, StringComparison.Ordinal));
        IEnumerable<MethodInfo> methods = types.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        s_commands = [];

        foreach (MethodInfo method in methods)
        {
            CommandAttribute? commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            if (commandAttribute != null)
            {
                var commandInfo = new CommandInfo(commandAttribute, method);
                s_commands.Add(commandInfo);
            }
        }
    }
}

public class CommandHost
{
    private readonly CommandParameterProvider _commandParameterService = new();

    internal ReadOnlyCollection<CommandInfo> Commands { get; init; }

    public CommandParameterProvider ParameterProvider => _commandParameterService;

    internal CommandHost(ReadOnlyCollection<CommandInfo> commands)
    {
        Commands = commands;
    }

    public CommandHost()
    {
        Commands = CommandRegistrar.GetCommands();
    }

    public async ValueTask<bool> RunCommandAsync(string command, IViewLogger logger, CancellationToken token = default)
    {
        command = command.TrimStart(':');

        ParsedCommand parsedCommand = new(command);

        CommandInfo? commandInfo = Commands.FirstOrDefault(c => c.CommandName == parsedCommand.Command);
        if (commandInfo is null)
        {
            return false;
        }

        await InvokeCommandAsync(commandInfo, parsedCommand, logger, token);

        return true;
    }

    internal async Task InvokeCommandDirectAsync(CommandInfo commandInfo, ParsedCommand parsedCommand, IViewLogger logger, CancellationToken token = default)
    {
        await InvokeCommandAsync(commandInfo, parsedCommand, logger, token);
    }

    private async Task InvokeCommandAsync(CommandInfo commandInfo, ParsedCommand parsedCommand, IViewLogger logger, CancellationToken token)
    {
        ParameterInfo[] parameters = commandInfo.CommandMethod.GetParameters();
        var arguments = new List<object?>();

        var callLocals = new Dictionary<Type, object>
        {
            [typeof(ParsedCommand)] = parsedCommand,
            [typeof(IViewLogger)] = logger,
            [typeof(CancellationToken)] = token,
            [typeof(List<CommandInfo>)] = Commands.ToList(),
        };

        foreach (ParameterInfo param in parameters)
        {
            Type paramType = param.ParameterType;

            object? service = paramType != typeof(ParsedCommand)
                ? callLocals.GetValueOrDefault(paramType) ?? _commandParameterService.GetService(paramType)
                : parsedCommand;

            if (service is null)
            {
                if (!param.HasDefaultValue)
                {
                    throw new InvalidOperationException($"No service registered for type {paramType.Name}");
                }

                service = param.DefaultValue;
            }

            arguments.Add(service);
        }

        try
        {
            object? target = null;

            /// I don't think any of the new commands would be in instance classes..
            // if (!commandInfo.CommandMethod.IsStatic)
            // {
            //     if (targetObject is null)
            //     {
            //         throw new InvalidOperationException("Unable to invoke command with null target object.");
            //     }
            // 
            //     target = targetObject;
            // }

            object? result = commandInfo.CommandMethod.Invoke(target, [.. arguments]);

            if (commandInfo.IsAsync && result is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException ex)
        {
            logger.Log($"Command execution error: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            logger.Log($"An unexpected error occurred during command invocation: {ex.Message}");
        }
    }
}

public class CommandParameterProvider
{
    private readonly Dictionary<Type, object> _parameters = [];

    public int ParamCount => _parameters.Count;

    public ReadOnlyDictionary<Type, object> GetParams => _parameters.AsReadOnly();

    public void Register<T>(T instance) where T : class
    {
        _parameters[typeof(T)] = instance;
    }

    public object? GetService(Type type)
    {
        return _parameters.TryGetValue(type, out object? service)
            ? service
            : null;
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class CommandAttribute : Attribute
{
    public const string NO_USAGE = "";

    public string CommandName { get; }

    public string HelpInfo { get; }

    public string CommandUsage { get; }

    public bool IsVisible { get; }

    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "It's literally used in this class, how does Sonar not see that?")]
    [SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression",
        Justification = "Without the suppression, there's a warning for the suppression that should suppress another warning. Am I having a stroke?")]
    public CommandAttribute(string commandName, string helpInfo, string usage = NO_USAGE, bool visible = true)
    {
        CommandName = commandName;
        HelpInfo = helpInfo;
        CommandUsage = usage;
        IsVisible = visible;
    }

    /// <summary>
    /// Creates an invisible command. It will not be listed when using the ':help' command.
    /// </summary>
    /// <param name="commandName"></param>
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "")]
    public CommandAttribute(string commandName)
    {
        CommandName = commandName;
        HelpInfo = CommandUsage = string.Empty;
        IsVisible = false;
    }

    public override string ToString()
    {
        return CommandName;
    }
}

internal sealed record CommandInfo
{
    public string CommandName { get; }
    public string HelpInfo { get; }
    public string CommandUsage { get; }
    public bool IsVisible { get; }

    internal MethodInfo CommandMethod { get; }

    public bool IsAsync => CommandMethod.ReturnType == typeof(Task) || CommandMethod.ReturnType.IsSubclassOf(typeof(Task));

    internal CommandInfo(CommandAttribute commandAttribute, MethodInfo commandMethod)
    {
        CommandName = commandAttribute.CommandName;
        HelpInfo = commandAttribute.HelpInfo;
        CommandUsage = commandAttribute.CommandUsage;
        IsVisible = commandAttribute.IsVisible;
        CommandMethod = commandMethod;
    }
}
