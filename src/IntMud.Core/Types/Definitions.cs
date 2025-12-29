using IntMud.Core.Instructions;
using IntMud.Core.Variables;

namespace IntMud.Core.Types;

/// <summary>
/// Variable definition within a class.
/// Maps to InstrVar/IndiceVar from the original implementation.
/// </summary>
public readonly struct VariableDefinition
{
    /// <summary>Variable name</summary>
    public required string Name { get; init; }

    /// <summary>Variable type opcode</summary>
    public required OpCode TypeCode { get; init; }

    /// <summary>Memory offset within object/class data</summary>
    public required int MemoryOffset { get; init; }

    /// <summary>Size in bytes</summary>
    public required int Size { get; init; }

    /// <summary>Whether this is a class-level (static) variable</summary>
    public required bool IsClassVariable { get; init; }

    /// <summary>Whether this variable is defined in this class (not inherited)</summary>
    public required bool IsDefinedInClass { get; init; }

    /// <summary>Array size (0 if not an array)</summary>
    public required int ArraySize { get; init; }

    /// <summary>Bit number for int1 types (0-7)</summary>
    public required byte BitNumber { get; init; }

    /// <summary>Text size for txt1/txt2 types</summary>
    public required int TextSize { get; init; }

    /// <summary>Variable modifiers</summary>
    public required VariableModifiers Modifiers { get; init; }

    /// <summary>Bytecode offset for initial value expression (if any)</summary>
    public int? InitializerOffset { get; init; }
}

/// <summary>
/// Function definition within a class.
/// </summary>
public readonly struct FunctionDefinition
{
    /// <summary>Function name</summary>
    public required string Name { get; init; }

    /// <summary>Whether this is a varfunc (variable function)</summary>
    public required bool IsVarFunc { get; init; }

    /// <summary>Bytecode offset where function body starts</summary>
    public required int BytecodeOffset { get; init; }

    /// <summary>Bytecode length of function body</summary>
    public required int BytecodeLength { get; init; }

    /// <summary>Local variable count</summary>
    public required int LocalCount { get; init; }

    /// <summary>Class where this function is defined</summary>
    public required IIntClass? DefiningClass { get; init; }
}

/// <summary>
/// Constant definition within a class.
/// </summary>
public readonly struct ConstantDefinition
{
    /// <summary>Constant name</summary>
    public required string Name { get; init; }

    /// <summary>Type of constant value</summary>
    public required ConstantType Type { get; init; }

    /// <summary>Value for integer constants</summary>
    public int? IntValue { get; init; }

    /// <summary>Value for real constants</summary>
    public double? RealValue { get; init; }

    /// <summary>Value for text constants</summary>
    public string? TextValue { get; init; }

    /// <summary>Bytecode offset for expression constants (varconst)</summary>
    public int? ExpressionOffset { get; init; }
}

/// <summary>
/// Type of constant.
/// </summary>
public enum ConstantType : byte
{
    /// <summary>Null constant</summary>
    Null = 0,

    /// <summary>Text constant</summary>
    Text = 1,

    /// <summary>Numeric constant (integer)</summary>
    Integer = 2,

    /// <summary>Numeric constant (real)</summary>
    Real = 3,

    /// <summary>Expression constant (evaluated at runtime)</summary>
    Expression = 4
}

/// <summary>
/// Source location for error reporting and debugging.
/// </summary>
public readonly record struct SourceLocation(
    string FilePath,
    int Line,
    int Column,
    int Length = 0
);
