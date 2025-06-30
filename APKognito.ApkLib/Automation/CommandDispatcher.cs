using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Automation;

public partial class CommandDispatcher
{
    private const string BINDING_TARGET_NAMESPACE = "APKognito.ApkLib.Automation.CommandBindings";

    private static Dictionary<string, CommandInfo>? s_commands;

    private readonly RenameStage _renameStage;

    private readonly ILogger _logger;

    private readonly string _basePath;

    private readonly Dictionary<string, string> _variables;

    private readonly CommandStageResult _stageResult;

    public CommandDispatcher(
        RenameStage stage,
        string packageBasePath,
        Dictionary<string, string> variables,
        ILogger logger
    )
    {
        if (IsElevated())
        {
            // I can't ensure there will never be some sort of exploit, so
            // it's best to try and minimize possible damage.
            throw new LaunchedAsAdminException();
        }

        _renameStage = stage;
        _logger = logger;
        _basePath = packageBasePath;
        _variables = variables;

        _stageResult = new();

        CacheInternalCommands(_logger);
    }

    public async Task<CommandStageResult> DispatchCommandsAsync()
    {
        CacheInternalCommands(_logger);

        foreach (Command command in _renameStage.Commands)
        {
            if (!s_commands!.TryGetValue(command.Name, out CommandInfo? commandInfo))
            {
                throw new UnknownCommandException(command.Name);
            }

            CommandStageResult? result = await ExecuteCommandAsync(commandInfo, [.. command.Arguments]);

            if (result is not null)
            {
                _stageResult.Append(result);
            }
        }

        return _stageResult;
    }

    private async Task<CommandStageResult?> ExecuteCommandAsync(CommandInfo commandInfo, string[] args)
    {
        // Resolve all variables. They're CMD style %VAR%. That might change later, though.
        for (int i = 0; i < args.Length; ++i)
        {
            args[i] = ReplaceVariables(args[i]);
        }

        CheckArgumentAccess(commandInfo.ArgumentCount, args, commandInfo.Accessors);

        switch (commandInfo.ArgumentCount)
        {
            case CommandAttribute.ANY:
            {
                // Do nothing
            }
            break;

            case CommandAttribute.NONE:
            {
                if (args.Length is not 0)
                {
                    throw new InvalidArgumentException($"Command '{commandInfo.Name}' expects no arguments, but {args.Length} were provided.");
                }
            }
            break;

            default:
            {
                if (args.Length != commandInfo.ArgumentCount)
                {
                    throw new InvalidArgumentException($"Command '{commandInfo.Name}' expects {commandInfo.ArgumentCount} arguments, but was given {args.Length}.");
                }
            }
            break;
        }

        int totalMethodParameters = commandInfo.Parameters.Length;

        object?[] invocationArgs = new object?[totalMethodParameters];

        if (commandInfo.ArgumentCount == CommandAttribute.ANY)
        {
            if (totalMethodParameters > 0)
            {
                invocationArgs[0] = args;
            }
        }
        else
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i < totalMethodParameters)
                {
                    invocationArgs[i] = args[i];
                }
            }
        }

        if (commandInfo.RequestsLogger)
        {
            invocationArgs[totalMethodParameters - 1] = _logger;
        }

        try
        {
            object? result = commandInfo.Method.Invoke(null, invocationArgs);

            if (commandInfo.IsAsync)
            {
                if (result is Task task)
                {
                    await task;
                }
                else if (result is Task<CommandStageResult> resultTask)
                {
                    return await resultTask;
                }
            }
            else
            {
                if (result is CommandStageResult commandResult)
                {
                    return commandResult;
                }
            }

            return null;
        }
        catch (TargetParameterCountException ex)
        {
            _logger.LogError(ex, "Internal Invocation Error for command '{Name}': Parameter count mismatch. Method expects {TotalMethodParameters}, built {Length}.",
                commandInfo.Name, totalMethodParameters, invocationArgs.Length);
            throw;
        }
        catch (Exception ex)
        {
#pragma warning disable S6667 // Logging in a catch clause should pass the caught exception as a parameter.
            _logger.LogError(ex.InnerException ?? ex, "Command '{Name}' failed", commandInfo.Name);
#pragma warning restore S6667 // Logging in a catch clause should pass the caught exception as a parameter.
            throw;
        }
    }

    private static void CacheInternalCommands(ILogger logger)
    {
        if (s_commands is not null && s_commands.Count is not 0)
        {
            return;
        }

        s_commands = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<MethodInfo> methods = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.Namespace is BINDING_TARGET_NAMESPACE)
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public));

        if (!methods.Any())
        {
            throw new NoCommandsException();
        }

        foreach (MethodInfo methodInfo in methods)
        {
            CommandAttribute? attribute = methodInfo.GetCustomAttribute<CommandAttribute>();
            if (attribute is null)
            {
                continue;
            }

            ParameterInfo[] parameters = methodInfo.GetParameters();

            bool hasViewLogger = parameters.Length > 0 && parameters[^1].ParameterType == typeof(ILogger);

            int declaredCommandArgsCount = hasViewLogger
                ? parameters.Length - 1
                : parameters.Length;

            bool signatureMatchesAttribute = false;

            switch (attribute.ArgumentCount)
            {
                case CommandAttribute.ANY:
                {
                    signatureMatchesAttribute = declaredCommandArgsCount is 1 && parameters[0].ParameterType == typeof(string[]);

                    if (!signatureMatchesAttribute)
                    {
                        logger.LogError("Command '{AttributeName}' ({Name}) has ArgumentCount.ANY but command-specific signature is not (string[] args).", attribute.Name, methodInfo.Name);
                        continue;
                    }
                }
                break;

                case CommandAttribute.NONE:
                {
                    signatureMatchesAttribute = declaredCommandArgsCount is 0;

                    if (!signatureMatchesAttribute)
                    {
                        logger.LogError("Command '{AttributeName}' ({Name}) has ArgumentCount.NONE but command-specific signature is not ().", attribute.Name, methodInfo.Name);
                        continue;
                    }
                }
                break;

                default:
                {
                    signatureMatchesAttribute = declaredCommandArgsCount == attribute.ArgumentCount
                        && parameters.Take(declaredCommandArgsCount).All(p => p.ParameterType == typeof(string));

                    if (!signatureMatchesAttribute)
                    {
                        logger.LogError("Command '{AttributeName}' ({Name}) expects {ArgumentCount} string arguments but command-specific signature is not (string, string, ...).",
                            attribute.Name, methodInfo.Name, attribute.ArgumentCount);
                        continue;
                    }
                }
                break;
            }

            try
            {
                CommandInfo info = new(attribute, methodInfo, hasViewLogger);

                if (!s_commands.TryAdd(info.Name, info))
                {
                    logger.LogError("Duplicate command name found: '{InfoName}' defined by {Name}. Previous definition will be overwritten.", info.Name, methodInfo.Name);
                    s_commands[info.Name] = info;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize command '{AttributeName}' ({Name}())", attribute.Name, methodInfo.Name);
            }
        }

        logger.LogError("Cached {Count} internal commands.", s_commands.Count);
    }

    private void CheckArgumentAccess(int argCount, string[] args, FileAccess[] accessors)
    {
        if (args.Length is 0)
        {
            return;
        }

        // SafeCombine will throw an exception if the paths are invalid/unsafe, so this doesn't need to return a status.

        if (argCount is CommandAttribute.ANY)
        {
            FileAccess access = accessors[0];

            for (int i = 0; i < args.Length; ++i)
            {
                args[i] = PathSandboxing.SafeCombine(_basePath, args[i], access.HasFlag(FileAccess.Write));
            }
        }
        else
        {
            for (int i = 0; i < args.Length; ++i)
            {
                args[i] = PathSandboxing.SafeCombine(_basePath, args[i], accessors[i].HasFlag(FileAccess.Write));
            }
        }
    }

    private string ReplaceVariables(string line)
    {
        MatchCollection matches = VariableRegex().Matches(line);

        if (matches.Count is 0)
        {
            return line;
        }

        StringBuilder sb = new(line.Length);
        int lastIndex = 0;

        foreach (Match match in matches)
        {
            Debug.Assert(match.Success, $"Failed to parse variable in '{line}':{match.Index}");

            _ = sb.Append(line, lastIndex, match.Index - lastIndex);

            if (_variables.TryGetValue(match.Value.Trim('%'), out string? variable))
            {
                _ = sb.Append(variable);
            }
            else
            {
                _logger.LogWarning("Failed to resolve variable '{Value}'", match.Value);
                _ = sb.Append(string.Empty);
            }

            lastIndex = match.Index + match.Length;
        }

        _ = sb.Append(line, lastIndex, line.Length - lastIndex);

        return sb.ToString();
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();

        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal class CommandInfo(CommandAttribute attribute, MethodInfo method, bool requestsLogger)
    {
        public string Name { get; } = attribute.Name;

        public FileAccess[] Accessors { get; } = attribute.Accessors;

        public int ArgumentCount { get; } = attribute.ArgumentCount;

        public bool RequestsLogger { get; } = requestsLogger;

        public bool IsAsync { get; } = method.ReturnType == typeof(Task);

        public MethodInfo Method { get; } = method;

        public ParameterInfo[] Parameters { get; } = method.GetParameters();
    }

    public class LaunchedAsAdminException() : Exception("Auto config rename files cannot be used while APKognito is launched as admin. " +
        "Either restart APKognito as a normal user, or disable this option in the Advanced Settings Page.")
    {
    }

    public class InvalidArgumentException(string message) : Exception(message)
    {
    }

    public class UnknownCommandException(string name) : Exception($"No command '{name}' exists.")
    {
    }

    public class NoCommandsException() : Exception("No commands were found. This likely means corruption or the namespace target is invalid.")
    {
    }

    [GeneratedRegex(@"%([^%]+)%")]
    private static partial Regex VariableRegex();
}
