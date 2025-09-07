using Antlr4.Runtime;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Automation.Parser;

// internal class ErrorListener<T> : ConsoleErrorListener<T>
// {
//     private readonly ILogger? _logger;
// 
//     public ErrorListener(ILogger? logger)
//     {
//         _logger = logger;
//     }
// 
//     public bool had_error;
// 
//     public override void SyntaxError(TextWriter output, IRecognizer recognizer, T offendingSymbol, int line,
//         int col, string msg, RecognitionException e)
//     {
//         had_error = true;
//         base.SyntaxError(output, recognizer, offendingSymbol, line, col, msg, e);
//     }
// }

public class ErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    public bool Errored { get; private set; } = false;

    private readonly ILogger? _logger;

    public ErrorListener(ILogger? logger)
    {
        _logger = logger;
    }

    public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Errored = true;
        _logger?.LogError(e, "line {Line}:{Position}: {Message}", line, charPositionInLine, msg);
    }

    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        SyntaxError(output, recognizer, 0, line, charPositionInLine, msg, e);
    }
}
