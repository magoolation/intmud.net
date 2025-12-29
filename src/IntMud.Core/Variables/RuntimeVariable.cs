using System.Runtime.InteropServices;
using IntMud.Core.Types;

namespace IntMud.Core.Variables;

/// <summary>
/// Represents a variable access context during execution.
/// Equivalent to TVariavel from variavel.h in the original implementation.
/// This is a stack-allocated struct for performance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RuntimeVariable
{
    /// <summary>
    /// Pointer to variable definition bytecode.
    /// </summary>
    public nint DefinitionPtr;

    /// <summary>
    /// Pointer to variable name definition.
    /// </summary>
    public nint NamePtr;

    /// <summary>
    /// Pointer to variable data in memory (or direct value for small types).
    /// </summary>
    public nint DataPtr;

    /// <summary>
    /// Integer value (used for immediate values).
    /// </summary>
    public int IntValue;

    /// <summary>
    /// Double value (used for immediate values).
    /// </summary>
    public double DoubleValue;

    /// <summary>
    /// Variable data size.
    /// </summary>
    public int Size;

    /// <summary>
    /// Array index (0xFF = whole array).
    /// </summary>
    public byte Index;

    /// <summary>
    /// Bit number for int1 types.
    /// </summary>
    public byte BitNumber;

    /// <summary>
    /// Function number for member functions.
    /// </summary>
    public ushort FunctionNumber;

    /// <summary>
    /// Runtime type of this variable.
    /// </summary>
    public VariableType Type;

    /// <summary>
    /// Flags for special handling.
    /// </summary>
    public RuntimeVariableFlags Flags;

    /// <summary>
    /// Get value as boolean.
    /// </summary>
    public readonly bool GetBool()
    {
        return Type switch
        {
            VariableType.Int => IntValue != 0,
            VariableType.Double => DoubleValue != 0.0,
            VariableType.Object => DataPtr != 0,
            _ => false
        };
    }

    /// <summary>
    /// Get value as integer.
    /// </summary>
    public readonly int GetInt()
    {
        return Type switch
        {
            VariableType.Int => IntValue,
            VariableType.Double => (int)DoubleValue,
            _ => 0
        };
    }

    /// <summary>
    /// Get value as double.
    /// </summary>
    public readonly double GetDouble()
    {
        return Type switch
        {
            VariableType.Int => IntValue,
            VariableType.Double => DoubleValue,
            _ => 0.0
        };
    }

    /// <summary>
    /// Check if this is a null/invalid variable.
    /// </summary>
    public readonly bool IsNull => Type == VariableType.Unknown && DataPtr == 0;

    /// <summary>
    /// Check if this is an object reference.
    /// </summary>
    public readonly bool IsObject => Type == VariableType.Object;

    /// <summary>
    /// Check if this is a numeric type.
    /// </summary>
    public readonly bool IsNumeric => Type is VariableType.Int or VariableType.Double;

    /// <summary>
    /// Create a null/empty variable.
    /// </summary>
    public static RuntimeVariable Null => default;

    /// <summary>
    /// Create an integer variable.
    /// </summary>
    public static RuntimeVariable FromInt(int value) => new()
    {
        Type = VariableType.Int,
        IntValue = value
    };

    /// <summary>
    /// Create a double variable.
    /// </summary>
    public static RuntimeVariable FromDouble(double value) => new()
    {
        Type = VariableType.Double,
        DoubleValue = value
    };

    /// <summary>
    /// Create a boolean variable (as int).
    /// </summary>
    public static RuntimeVariable FromBool(bool value) => new()
    {
        Type = VariableType.Int,
        IntValue = value ? 1 : 0
    };
}

/// <summary>
/// Flags for RuntimeVariable.
/// </summary>
[Flags]
public enum RuntimeVariableFlags : byte
{
    /// <summary>No special flags</summary>
    None = 0,

    /// <summary>Value is stored inline (not pointer)</summary>
    Inline = 1,

    /// <summary>Read-only variable</summary>
    ReadOnly = 2,

    /// <summary>Temporary variable (on stack)</summary>
    Temporary = 4,

    /// <summary>Array access in progress</summary>
    ArrayAccess = 8
}
