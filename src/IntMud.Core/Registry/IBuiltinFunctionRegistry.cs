namespace IntMud.Core.Registry;

/// <summary>
/// Registry for built-in functions.
/// IntMUD has 145+ built-in functions (arg0-9, criar, apagar, int, txt, etc.).
/// </summary>
public interface IBuiltinFunctionRegistry
{
    /// <summary>
    /// Register a built-in function.
    /// </summary>
    /// <param name="name">Function name</param>
    /// <param name="function">Function implementation</param>
    void Register(string name, BuiltinFunction function);

    /// <summary>
    /// Register a built-in function with metadata.
    /// </summary>
    void Register(BuiltinFunctionInfo info);

    /// <summary>
    /// Get function by name.
    /// </summary>
    BuiltinFunction? GetFunction(string name);

    /// <summary>
    /// Get function by index (for optimized calls).
    /// </summary>
    BuiltinFunction? GetFunction(int index);

    /// <summary>
    /// Get function index by name.
    /// </summary>
    int GetFunctionIndex(string name);

    /// <summary>
    /// Get all function names.
    /// </summary>
    IEnumerable<string> GetAllFunctionNames();

    /// <summary>
    /// Get function info by name.
    /// </summary>
    BuiltinFunctionInfo? GetFunctionInfo(string name);

    /// <summary>
    /// Check if function exists.
    /// </summary>
    bool HasFunction(string name);

    /// <summary>
    /// Number of registered functions.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// Delegate for built-in function implementation.
/// </summary>
/// <param name="context">Execution context with arguments and return value</param>
/// <returns>True if function executed successfully</returns>
public delegate bool BuiltinFunction(IExecutionContext context);

/// <summary>
/// Built-in function metadata.
/// </summary>
public sealed class BuiltinFunctionInfo
{
    /// <summary>Function name</summary>
    public required string Name { get; init; }

    /// <summary>Function implementation</summary>
    public required BuiltinFunction Function { get; init; }

    /// <summary>Minimum argument count</summary>
    public int MinArguments { get; init; }

    /// <summary>Maximum argument count (-1 for variadic)</summary>
    public int MaxArguments { get; init; } = -1;

    /// <summary>Return type hint</summary>
    public BuiltinReturnType ReturnType { get; init; } = BuiltinReturnType.Any;

    /// <summary>Description for documentation</summary>
    public string? Description { get; init; }

    /// <summary>Category for organization</summary>
    public BuiltinCategory Category { get; init; } = BuiltinCategory.Other;
}

/// <summary>
/// Return type hint for built-in functions.
/// </summary>
public enum BuiltinReturnType
{
    /// <summary>Any type</summary>
    Any,

    /// <summary>No return value</summary>
    Void,

    /// <summary>Integer return</summary>
    Int,

    /// <summary>Double return</summary>
    Double,

    /// <summary>String return</summary>
    Text,

    /// <summary>Object reference return</summary>
    Object,

    /// <summary>Boolean return</summary>
    Bool
}

/// <summary>
/// Built-in function category.
/// </summary>
public enum BuiltinCategory
{
    /// <summary>Other/uncategorized</summary>
    Other,

    /// <summary>Function arguments (arg0-9, args)</summary>
    Arguments,

    /// <summary>Object management (criar, apagar, este)</summary>
    Objects,

    /// <summary>Math functions (intabs, matsin, etc.)</summary>
    Math,

    /// <summary>Text functions (txt, txtmai, txtsub, etc.)</summary>
    Text,

    /// <summary>Type conversion</summary>
    Conversion,

    /// <summary>Date/time functions</summary>
    DateTime,

    /// <summary>Random number generation</summary>
    Random,

    /// <summary>Debugging/introspection</summary>
    Debug
}
