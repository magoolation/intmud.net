using IntMud.Compiler.Bytecode;

namespace IntMud.Runtime.Values;

/// <summary>
/// Class-level storage for "comum" (common/static) variables.
/// In the C++ original, common variables are stored in TClasse::Vars, shared between
/// all objects of that class. This registry provides the same semantics for the .NET port.
/// Each CompiledUnit (class) has its own dictionary of shared variable values.
/// </summary>
public static class ClassVariableStorage
{
    // Keyed by CompiledUnit identity (reference equality), then by variable name
    private static readonly Dictionary<CompiledUnit, Dictionary<string, RuntimeValue>> _storage = new();

    /// <summary>
    /// Get a common variable value for a class.
    /// </summary>
    public static RuntimeValue Get(CompiledUnit unit, string name)
    {
        if (_storage.TryGetValue(unit, out var vars) && vars.TryGetValue(name, out var value))
            return value;
        return RuntimeValue.Null;
    }

    /// <summary>
    /// Set a common variable value for a class.
    /// </summary>
    public static void Set(CompiledUnit unit, string name, RuntimeValue value)
    {
        if (!_storage.TryGetValue(unit, out var vars))
        {
            vars = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
            _storage[unit] = vars;
        }
        vars[name] = value;
    }

    /// <summary>
    /// Check if a common variable exists for a class.
    /// </summary>
    public static bool Has(CompiledUnit unit, string name)
    {
        return _storage.TryGetValue(unit, out var vars) && vars.ContainsKey(name);
    }

    /// <summary>
    /// Initialize a common variable with a default value if not already set.
    /// Returns true if the variable was initialized (first time), false if already existed.
    /// </summary>
    public static bool InitializeIfNew(CompiledUnit unit, string name, RuntimeValue defaultValue)
    {
        if (!_storage.TryGetValue(unit, out var vars))
        {
            vars = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
            _storage[unit] = vars;
        }
        if (!vars.ContainsKey(name))
        {
            vars[name] = defaultValue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clear all class variable storage (for tests/reset).
    /// </summary>
    public static void Clear()
    {
        _storage.Clear();
    }
}
