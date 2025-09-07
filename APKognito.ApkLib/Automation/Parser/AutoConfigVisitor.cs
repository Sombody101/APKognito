using System.Diagnostics.CodeAnalysis;
using Antlr4.Runtime;
using Microsoft.Extensions.Logging;
using NotNullAttribute = Antlr4.Runtime.Misc.NotNullAttribute;

namespace APKognito.ApkLib.Automation.Parser;

public class AutoConfigVisitor : AutoConfig_ParserBaseVisitor<string>
{
    private readonly ILogger? _logger;

    internal AutoConfigBuilder ConfigBuilder { get; }

    [SuppressMessage("Minor Code Smell",
        "S2325:Methods and properties that don't access instance data should be static",
        Justification = "That would break simultaneous instances of the visitor.")]
    private RenameStage CurrentStage
    {
        get => field ?? throw new InvalidStageScopeException();
        set => field = value;
    }

    public AutoConfigVisitor(ILogger? logger)
    {
        _logger = logger;
        ConfigBuilder = new();
    }

    public override string VisitMetaSetter([NotNull] AutoConfig_Parser.MetaSetterContext context)
    {
        string key = context.Identifier().GetText();
        string value = Visit(context.argument());

        ConfigBuilder.MetadataTable.Add(key, value);
        return null!;
    }

    public override string VisitSectionDeclaration([NotNull] AutoConfig_Parser.SectionDeclarationContext context)
    {
        string rawStageText = context.Identifier().GetText();

        if (!Enum.TryParse(rawStageText, true, out CommandStage stage))
        {
            throw new UnknownStageException(rawStageText, context.Start.Line);
        }

        RenameStage? currentStage = ConfigBuilder.GetStage(stage);

        if (currentStage is null)
        {
            currentStage = new(stage);
            ConfigBuilder.Stages.Add(currentStage);
        }

        CurrentStage = currentStage;
        return null!;
    }

    public override string VisitLine([NotNull] AutoConfig_Parser.LineContext context)
    {
        string commandName = context.Identifier().GetText();
        IEnumerable<string> arguments = context.argument().Select(Visit);

        Command command = new(commandName, arguments);
        CurrentStage.Commands.Add(command);

        return null!;
    }

    public override string VisitArgument([NotNull] AutoConfig_Parser.ArgumentContext context)
    {
        if (context.StringConstant() is { } stringConstant)
        {
            string rawStringConstant = stringConstant.GetText();
            return rawStringConstant.Trim(rawStringConstant[0]);
        }
        else if (context.Number() is { } numberConstant)
        {
            return numberConstant.GetText();
        }
        else if (context.Word() is { } word)
        {
            return word.GetText();
        }

        throw new UnknownArgumentTypeException(context);
    }
}

public sealed class InvalidStageScopeException() : Exception("Attempted to get a stage before any were defined.")
{
}

public sealed class UnknownArgumentTypeException(ParserRuleContext context) : Exception($"Unknown argument type at {context.Start.Line}:{context.Start.Column}")
{
}

public sealed class LexingFailedException() : Exception("Failed to lex script.")
{
}

public sealed class ParsingFailedException() : Exception("Failed to parse script.")
{
}
