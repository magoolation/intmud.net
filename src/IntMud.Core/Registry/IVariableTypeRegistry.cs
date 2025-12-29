using IntMud.Core.Instructions;
using IntMud.Core.Variables;

namespace IntMud.Core.Registry;

/// <summary>
/// Registry for variable type handlers.
/// Each variable type (int8, txt1, socket, etc.) has a handler that knows
/// how to create, destroy, read, and write values of that type.
/// </summary>
public interface IVariableTypeRegistry
{
    /// <summary>
    /// Register a variable type handler.
    /// </summary>
    void Register(IVariableTypeHandler handler);

    /// <summary>
    /// Get handler for a specific opcode.
    /// </summary>
    IVariableTypeHandler? GetHandler(OpCode opCode);

    /// <summary>
    /// Get all registered handlers.
    /// </summary>
    IEnumerable<IVariableTypeHandler> GetAllHandlers();

    /// <summary>
    /// Check if a handler exists for the given opcode.
    /// </summary>
    bool HasHandler(OpCode opCode);
}

/// <summary>
/// Handler for a specific variable type.
/// Maps to TVarInfo from variavel.h in the original implementation.
/// </summary>
public interface IVariableTypeHandler
{
    /// <summary>
    /// OpCode this handler manages.
    /// </summary>
    OpCode OpCode { get; }

    /// <summary>
    /// Runtime type for this variable.
    /// </summary>
    VariableType RuntimeType { get; }

    /// <summary>
    /// Name of this variable type (e.g., "int8", "txt1", "socket").
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Calculate the size in bytes for this variable type.
    /// </summary>
    /// <param name="instruction">Variable definition bytecode</param>
    /// <returns>Size in bytes</returns>
    int GetSize(ReadOnlySpan<byte> instruction);

    /// <summary>
    /// Calculate array element size.
    /// </summary>
    int GetArrayElementSize(ReadOnlySpan<byte> instruction);

    /// <summary>
    /// Initialize variable data in memory.
    /// </summary>
    void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction);

    /// <summary>
    /// Clean up variable data (release resources).
    /// </summary>
    void Destroy(Span<byte> memory);

    /// <summary>
    /// Get value as boolean.
    /// </summary>
    bool GetBool(ReadOnlySpan<byte> memory);

    /// <summary>
    /// Get value as integer.
    /// </summary>
    int GetInt(ReadOnlySpan<byte> memory);

    /// <summary>
    /// Get value as double.
    /// </summary>
    double GetDouble(ReadOnlySpan<byte> memory);

    /// <summary>
    /// Get value as text.
    /// </summary>
    string GetText(ReadOnlySpan<byte> memory);

    /// <summary>
    /// Set value from integer.
    /// </summary>
    void SetInt(Span<byte> memory, int value);

    /// <summary>
    /// Set value from double.
    /// </summary>
    void SetDouble(Span<byte> memory, double value);

    /// <summary>
    /// Set value from text.
    /// </summary>
    void SetText(Span<byte> memory, string value);

    /// <summary>
    /// Assign from another variable.
    /// </summary>
    void Assign(Span<byte> dest, ReadOnlySpan<byte> source);

    /// <summary>
    /// Add to this variable.
    /// </summary>
    bool Add(Span<byte> memory, ReadOnlySpan<byte> value);

    /// <summary>
    /// Compare two values.
    /// </summary>
    int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);

    /// <summary>
    /// Check equality.
    /// </summary>
    bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);

    /// <summary>
    /// Execute a member function on this variable type.
    /// </summary>
    /// <param name="memory">Variable memory</param>
    /// <param name="functionName">Name of the function</param>
    /// <param name="context">Execution context</param>
    /// <returns>True if function was handled</returns>
    bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context);
}

/// <summary>
/// Execution context passed to variable type handlers.
/// </summary>
public interface IExecutionContext
{
    /// <summary>Get argument count</summary>
    int ArgumentCount { get; }

    /// <summary>Get argument as integer</summary>
    int GetIntArgument(int index);

    /// <summary>Get argument as double</summary>
    double GetDoubleArgument(int index);

    /// <summary>Get argument as string</summary>
    string GetStringArgument(int index);

    /// <summary>Get argument as object</summary>
    object? GetObjectArgument(int index);

    /// <summary>Set return value as integer</summary>
    void SetReturnInt(int value);

    /// <summary>Set return value as double</summary>
    void SetReturnDouble(double value);

    /// <summary>Set return value as string</summary>
    void SetReturnString(string value);

    /// <summary>Set return value as boolean</summary>
    void SetReturnBool(bool value);

    /// <summary>Set return value as object</summary>
    void SetReturnObject(object? value);

    /// <summary>Set return value as null</summary>
    void SetReturnNull();
}
