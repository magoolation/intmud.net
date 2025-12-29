namespace IntMud.Compiler.Bytecode;

/// <summary>
/// Represents a compiled class with all its functions and variables.
/// </summary>
public sealed class CompiledUnit
{
    /// <summary>
    /// Name of the class.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Source file where the class was defined.
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// List of base class names (inheritance).
    /// </summary>
    public List<string> BaseClasses { get; } = new();

    /// <summary>
    /// Compiled functions indexed by name.
    /// </summary>
    public Dictionary<string, CompiledFunction> Functions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Variable declarations.
    /// </summary>
    public List<CompiledVariable> Variables { get; } = new();

    /// <summary>
    /// Constant definitions.
    /// </summary>
    public Dictionary<string, CompiledConstant> Constants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// String pool for constant strings used in bytecode.
    /// </summary>
    public List<string> StringPool { get; } = new();

    /// <summary>
    /// Total size of variable data in bytes.
    /// </summary>
    public int VariableDataSize { get; set; }
}

/// <summary>
/// Represents a compiled function with its bytecode.
/// </summary>
public sealed class CompiledFunction
{
    /// <summary>
    /// Name of the function.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Source file where the function was defined.
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// Starting line number.
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// The bytecode instructions.
    /// </summary>
    public byte[] Bytecode { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Local variables declared in this function.
    /// </summary>
    public List<CompiledVariable> LocalVariables { get; } = new();

    /// <summary>
    /// Maximum stack depth required.
    /// </summary>
    public int MaxStackDepth { get; set; }

    /// <summary>
    /// Line number information for debugging.
    /// </summary>
    public List<LineInfo> LineInfo { get; } = new();

    /// <summary>
    /// Whether this function is a varfunc (virtual).
    /// </summary>
    public bool IsVirtual { get; set; }
}

/// <summary>
/// Represents a compiled variable declaration.
/// </summary>
public sealed class CompiledVariable
{
    /// <summary>
    /// Variable name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Variable type name (e.g., "int32", "txt1").
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Offset in the data segment.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Array size (0 = not an array).
    /// </summary>
    public int ArraySize { get; set; }

    /// <summary>
    /// Whether this is a common (static) variable.
    /// </summary>
    public bool IsCommon { get; set; }

    /// <summary>
    /// Whether this variable should be saved.
    /// </summary>
    public bool IsSaved { get; set; }
}

/// <summary>
/// Represents a compiled constant.
/// </summary>
public sealed class CompiledConstant
{
    /// <summary>
    /// Constant name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Constant value type.
    /// </summary>
    public ConstantType Type { get; set; }

    /// <summary>
    /// Integer value (if Type is Int).
    /// </summary>
    public int IntValue { get; set; }

    /// <summary>
    /// Double value (if Type is Double).
    /// </summary>
    public double DoubleValue { get; set; }

    /// <summary>
    /// String value (if Type is String).
    /// </summary>
    public string? StringValue { get; set; }
}

/// <summary>
/// Type of constant value.
/// </summary>
public enum ConstantType
{
    Null,
    Int,
    Double,
    String
}

/// <summary>
/// Line number information for debugging.
/// </summary>
public struct LineInfo
{
    /// <summary>
    /// Bytecode offset.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Source line number.
    /// </summary>
    public int Line { get; set; }
}
