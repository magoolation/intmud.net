namespace IntMud.Core.Variables;

/// <summary>
/// Runtime variable type enumeration - maps to TVarTipo from variavel.h.
/// Used for quick type checking during expression evaluation.
/// </summary>
public enum VariableType : byte
{
    /// <summary>Unknown or complex type</summary>
    Unknown = 0,

    /// <summary>Integer value (32-bit)</summary>
    Int = 1,

    /// <summary>Double precision floating point</summary>
    Double = 2,

    /// <summary>Text string (const char* equivalent)</summary>
    Text = 3,

    /// <summary>Object reference or null (NULO)</summary>
    Object = 4
}

/// <summary>
/// Variable modifiers for storage and behavior.
/// </summary>
[Flags]
public enum VariableModifiers : byte
{
    /// <summary>No special modifiers</summary>
    None = 0,

    /// <summary>Shared across all instances (comum)</summary>
    Common = 1,

    /// <summary>Saved to file (sav)</summary>
    Save = 2,

    /// <summary>Array/vector type</summary>
    Array = 4
}
