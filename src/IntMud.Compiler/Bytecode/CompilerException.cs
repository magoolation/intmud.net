namespace IntMud.Compiler.Bytecode;

/// <summary>
/// Exception thrown during bytecode compilation.
/// </summary>
public sealed class CompilerException : Exception
{
    /// <summary>
    /// Line number where the error occurred.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Column number where the error occurred.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Source file where the error occurred.
    /// </summary>
    public string? SourceFile { get; }

    public CompilerException(string message)
        : base(message)
    {
    }

    public CompilerException(string message, int line)
        : base($"Line {line}: {message}")
    {
        Line = line;
    }

    public CompilerException(string message, int line, int column)
        : base($"Line {line}, Column {column}: {message}")
    {
        Line = line;
        Column = column;
    }

    public CompilerException(string message, string sourceFile, int line)
        : base($"{sourceFile}:{line}: {message}")
    {
        SourceFile = sourceFile;
        Line = line;
    }

    public CompilerException(string message, string sourceFile, int line, int column)
        : base($"{sourceFile}:{line}:{column}: {message}")
    {
        SourceFile = sourceFile;
        Line = line;
        Column = column;
    }

    public CompilerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
