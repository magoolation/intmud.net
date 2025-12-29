using IntMud.Runtime.Values;

namespace IntMud.BuiltinFunctions;

/// <summary>
/// Registry for built-in functions.
/// </summary>
public class BuiltinFunctionRegistry
{
    private readonly Dictionary<string, IBuiltinFunction> _functions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Register a built-in function.
    /// </summary>
    public void Register(IBuiltinFunction function)
    {
        foreach (var name in function.Names)
        {
            _functions[name] = function;
        }
    }

    /// <summary>
    /// Check if a function is registered.
    /// </summary>
    public bool HasFunction(string name)
    {
        return _functions.ContainsKey(name);
    }

    /// <summary>
    /// Get a function by name.
    /// </summary>
    public IBuiltinFunction? GetFunction(string name)
    {
        return _functions.GetValueOrDefault(name);
    }

    /// <summary>
    /// Execute a function by name.
    /// </summary>
    public RuntimeValue Execute(string name, BuiltinFunctionContext context, RuntimeValue[] args)
    {
        var func = GetFunction(name);
        if (func == null)
            throw new InvalidOperationException($"Built-in function not found: {name}");

        return func.Execute(context, args);
    }

    /// <summary>
    /// Get all registered function names.
    /// </summary>
    public IEnumerable<string> GetFunctionNames()
    {
        return _functions.Keys;
    }

    /// <summary>
    /// Create a registry with all standard built-in functions.
    /// </summary>
    public static BuiltinFunctionRegistry CreateDefault()
    {
        var registry = new BuiltinFunctionRegistry();

        // Register all function handlers
        registry.Register(new Functions.ObjectFunctions());
        registry.Register(new Functions.ArgumentFunctions());
        registry.Register(new Functions.TextFunctions());
        registry.Register(new Functions.MathFunctions());
        registry.Register(new Functions.ConversionFunctions());
        registry.Register(new Functions.ControlFunctions());

        return registry;
    }
}
