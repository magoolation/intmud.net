using Antlr4.Runtime;
using IntMud.Compiler.Ast;

namespace IntMud.Compiler.Parsing;

/// <summary>
/// Parser for IntMUD source files.
/// </summary>
public class IntMudSourceParser
{
    /// <summary>
    /// Parse a source file and return the AST.
    /// </summary>
    public CompilationUnitNode Parse(string sourceCode, string? fileName = null)
    {
        var inputStream = new AntlrInputStream(sourceCode);
        var lexer = new IntMudLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new IntMudParser(tokenStream);

        // Add error handling
        var errorListener = new ParserErrorListener(fileName);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(errorListener);

        var parseTree = parser.compilationUnit();

        if (errorListener.Errors.Count > 0)
        {
            throw new ParseException(errorListener.Errors);
        }

        var visitor = new ParseTreeToAstVisitor(fileName);
        return visitor.Visit(parseTree) as CompilationUnitNode
            ?? throw new InvalidOperationException("Failed to parse compilation unit");
    }

    /// <summary>
    /// Parse a single expression.
    /// </summary>
    public ExpressionNode ParseExpression(string expression)
    {
        var inputStream = new AntlrInputStream(expression);
        var lexer = new IntMudLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new IntMudParser(tokenStream);

        var errorListener = new ParserErrorListener(null);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        var parseTree = parser.expression();

        if (errorListener.Errors.Count > 0)
        {
            throw new ParseException(errorListener.Errors);
        }

        var visitor = new ParseTreeToAstVisitor(null);
        return visitor.VisitExpression(parseTree);
    }
}

/// <summary>
/// Parser error information.
/// </summary>
public class ParseError
{
    public string? FileName { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public required string Message { get; init; }

    public override string ToString()
    {
        if (FileName != null)
            return $"{FileName}({Line},{Column}): {Message}";
        return $"({Line},{Column}): {Message}";
    }
}

/// <summary>
/// Exception thrown when parsing fails.
/// </summary>
public class ParseException : Exception
{
    public IReadOnlyList<ParseError> Errors { get; }

    public ParseException(IReadOnlyList<ParseError> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    private static string FormatMessage(IReadOnlyList<ParseError> errors)
    {
        if (errors.Count == 1)
            return errors[0].ToString();
        return $"{errors.Count} parse errors:\n" + string.Join("\n", errors);
    }
}

/// <summary>
/// Error listener that collects parse errors.
/// </summary>
internal class ParserErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    private readonly string? _fileName;
    public List<ParseError> Errors { get; } = new();

    public ParserErrorListener(string? fileName)
    {
        _fileName = fileName;
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        Errors.Add(new ParseError
        {
            FileName = _fileName,
            Line = line,
            Column = charPositionInLine + 1,
            Message = msg
        });
    }

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        Errors.Add(new ParseError
        {
            FileName = _fileName,
            Line = line,
            Column = charPositionInLine + 1,
            Message = msg
        });
    }
}
