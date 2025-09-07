using Antlr4.Runtime;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Automation.Parser;

public sealed class AutoConfigParser
{
    private readonly ILogger _logger;

    private readonly ErrorListener _parserErrorListener;
    private readonly ErrorListener _lexerErrorListener;

    public AutoConfigParser(ILogger logger)
    {
        _logger = logger;

        _lexerErrorListener = new(logger);
        _parserErrorListener = new(logger);
    }

    public AutoConfig? ParseDocument(string script)
    {
        var visitor = new AutoConfigVisitor(_logger);

        AutoConfig_Lexer lexer = CreateLexer(script);
        AutoConfig_Parser parser = CreateParser(lexer);

        _ = visitor.Visit(parser.document());

        if (_lexerErrorListener.Errored)
        {
            throw new LexingFailedException();
        }

        if (_parserErrorListener.Errored)
        {
            throw new ParsingFailedException();
        }

        return visitor.ConfigBuilder.Build();
    }

    private AutoConfig_Lexer CreateLexer(string data)
    {
        AutoConfig_Lexer lexer = new(new AntlrInputStream(data));

        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(_lexerErrorListener);

        return _lexerErrorListener.Errored
            ? throw new LexingFailedException()
            : lexer;
    }

    private AutoConfig_Parser CreateParser(AutoConfig_Lexer lexer)
    {
        AutoConfig_Parser parser = new(new CommonTokenStream(lexer));

        parser.RemoveErrorListeners();
        parser.AddErrorListener(_parserErrorListener);

        return _lexerErrorListener.Errored
            ? throw new ParsingFailedException()
            : parser;
    }
}
