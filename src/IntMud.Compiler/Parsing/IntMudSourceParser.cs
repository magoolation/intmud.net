using System.Text;
using Antlr4.Runtime;
using IntMud.Compiler.Ast;

namespace IntMud.Compiler.Parsing;

/// <summary>
/// Parser for IntMUD source files.
/// </summary>
public class IntMudSourceParser
{
    // Mapping of accented keywords to their normalized forms
    private static readonly Dictionary<string, string> KeywordNormalization = new()
    {
        { "senão", "senao" },
        { "então", "entao" },
        { "função", "funcao" },
        { "não", "nao" },
    };

    /// <summary>
    /// Normalizes source code from legacy Latin1 encoding to UTF-8.
    /// Also normalizes accented keywords to their ASCII equivalents.
    /// </summary>
    public static string NormalizeEncoding(string sourceCode)
    {
        // Replace accented keywords with their normalized versions
        var normalized = sourceCode;
        foreach (var (accented, plain) in KeywordNormalization)
        {
            normalized = normalized.Replace(accented, plain);
        }

        // Remove line continuations outside of strings
        normalized = RemoveLineContinuations(normalized);

        return normalized;
    }

    /// <summary>
    /// Removes line continuations (backslash followed by newline) outside of strings.
    /// Strings handle their own line continuations via the lexer.
    /// </summary>
    private static string RemoveLineContinuations(string source)
    {
        var result = new StringBuilder(source.Length);
        bool inString = false;
        int i = 0;

        while (i < source.Length)
        {
            char c = source[i];

            if (inString)
            {
                // Inside string - copy everything, including backslash-newline
                result.Append(c);
                if (c == '\\' && i + 1 < source.Length)
                {
                    // Escape sequence - copy next char too to avoid treating \" as string end
                    i++;
                    result.Append(source[i]);
                }
                else if (c == '"')
                {
                    inString = false;
                }
            }
            else if (c == '"')
            {
                // Start of string
                inString = true;
                result.Append(c);
            }
            else if (c == '#')
            {
                // Comment - copy until end of line, skipping line continuations
                while (i < source.Length && source[i] != '\n')
                {
                    if (source[i] == '\\')
                    {
                        // Check for line continuation in comment
                        int j = i + 1;
                        while (j < source.Length && (source[j] == ' ' || source[j] == '\t'))
                            j++;
                        if (j < source.Length && (source[j] == '\n' || (source[j] == '\r' && j + 1 < source.Length && source[j + 1] == '\n')))
                        {
                            // Skip the line continuation in comment
                            i = source[j] == '\r' ? j + 2 : j + 1;
                            continue;
                        }
                    }
                    result.Append(source[i]);
                    i++;
                }
                if (i < source.Length)
                {
                    result.Append(source[i]); // newline
                }
            }
            else if (c == '\\')
            {
                // Potential line continuation outside string
                int j = i + 1;
                // Skip optional whitespace
                while (j < source.Length && (source[j] == ' ' || source[j] == '\t'))
                    j++;
                // Check for newline
                if (j < source.Length && source[j] == '\r' && j + 1 < source.Length && source[j + 1] == '\n')
                {
                    // Skip \<whitespace>\r\n
                    i = j + 1;
                }
                else if (j < source.Length && source[j] == '\n')
                {
                    // Skip \<whitespace>\n
                    i = j;
                }
                else
                {
                    // Not a line continuation
                    result.Append(c);
                }
            }
            else
            {
                result.Append(c);
            }
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Reads a source file with automatic encoding detection.
    /// Tries UTF-8 first, falls back to Latin1 for legacy files.
    /// </summary>
    public static string ReadSourceFile(string filePath)
    {
        // Read as bytes first
        var bytes = File.ReadAllBytes(filePath);

        // Try to detect if it's valid UTF-8
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var content = utf8.GetString(bytes);
            return NormalizeEncoding(content);
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8, assume Latin1 (ISO-8859-1)
            var latin1 = Encoding.GetEncoding("ISO-8859-1");
            var content = latin1.GetString(bytes);
            return NormalizeEncoding(content);
        }
    }

    /// <summary>
    /// Parse a source file and return the AST.
    /// </summary>
    public CompilationUnitNode Parse(string sourceCode, string? fileName = null)
    {
        // Normalize encoding in case source was read with wrong encoding
        var normalizedSource = NormalizeEncoding(sourceCode);

        var inputStream = new AntlrInputStream(normalizedSource);
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
