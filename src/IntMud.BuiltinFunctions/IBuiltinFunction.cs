using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;
using RuntimeExecutionContext = IntMud.Runtime.Execution.ExecutionContext;

namespace IntMud.BuiltinFunctions;

/// <summary>
/// Interface for built-in functions.
/// </summary>
public interface IBuiltinFunction
{
    /// <summary>
    /// The function name(s) this handler responds to.
    /// </summary>
    IEnumerable<string> Names { get; }

    /// <summary>
    /// Execute the function with the given arguments.
    /// </summary>
    RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args);
}

/// <summary>
/// Context for built-in function execution.
/// </summary>
public class BuiltinFunctionContext
{
    /// <summary>
    /// The execution context.
    /// </summary>
    public required RuntimeExecutionContext ExecutionContext { get; init; }

    /// <summary>
    /// The class registry.
    /// </summary>
    public required RuntimeClassRegistry ClassRegistry { get; init; }

    /// <summary>
    /// The current object (este).
    /// </summary>
    public RuntimeObject? CurrentObject => ExecutionContext.CurrentObject as RuntimeObject;

    /// <summary>
    /// Write output text.
    /// </summary>
    public Action<string>? WriteOutput { get; set; }

    /// <summary>
    /// Write error text.
    /// </summary>
    public Action<string>? WriteError { get; set; }
}

/// <summary>
/// Attribute to mark a method as a built-in function.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class BuiltinFunctionAttribute : Attribute
{
    public string Name { get; }

    public BuiltinFunctionAttribute(string name)
    {
        Name = name;
    }
}
