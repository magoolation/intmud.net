using IntMud.Compiler.Bytecode;

namespace IntMud.Runtime.Values;

/// <summary>
/// Represents an object instance at runtime for bytecode execution.
/// </summary>
public sealed class BytecodeRuntimeObject
{
    private readonly Dictionary<string, RuntimeValue> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CompiledUnit> _classHierarchy;

    /// <summary>
    /// The compiled unit that defines this object's class (most derived).
    /// </summary>
    public CompiledUnit ClassUnit { get; }

    /// <summary>
    /// The class hierarchy from most derived to base classes.
    /// </summary>
    public IReadOnlyList<CompiledUnit> ClassHierarchy => _classHierarchy;

    /// <summary>
    /// The class name of this object.
    /// </summary>
    public string ClassName => ClassUnit.ClassName;

    /// <summary>
    /// Creates a new runtime object instance.
    /// </summary>
    public BytecodeRuntimeObject(CompiledUnit classUnit)
        : this(classUnit, Array.Empty<CompiledUnit>())
    {
    }

    /// <summary>
    /// Creates a new runtime object instance with base class units.
    /// </summary>
    public BytecodeRuntimeObject(CompiledUnit classUnit, IEnumerable<CompiledUnit> baseClassUnits)
    {
        ClassUnit = classUnit ?? throw new ArgumentNullException(nameof(classUnit));

        // Build class hierarchy (most derived first)
        _classHierarchy = new List<CompiledUnit> { classUnit };
        _classHierarchy.AddRange(baseClassUnits);

        // Initialize fields from all classes in hierarchy (base classes first so derived can override)
        for (int i = _classHierarchy.Count - 1; i >= 0; i--)
        {
            var unit = _classHierarchy[i];

            // Initialize variables with default values
            foreach (var variable in unit.Variables)
            {
                if (!_fields.ContainsKey(variable.Name))
                {
                    _fields[variable.Name] = RuntimeValue.Null;
                }
            }

            // Initialize literal constants only (Expression constants are evaluated at runtime)
            foreach (var constant in unit.Constants)
            {
                // Skip Expression constants - they need runtime context (args, this)
                if (constant.Value.Type == ConstantType.Expression)
                    continue;

                _fields[constant.Key] = constant.Value.Type switch
                {
                    ConstantType.Int => RuntimeValue.FromInt(constant.Value.IntValue),
                    ConstantType.Double => RuntimeValue.FromDouble(constant.Value.DoubleValue),
                    ConstantType.String => RuntimeValue.FromString(constant.Value.StringValue),
                    _ => RuntimeValue.Null
                };
            }
        }
    }

    /// <summary>
    /// Get a field value by name.
    /// </summary>
    public RuntimeValue GetField(string name)
    {
        if (_fields.TryGetValue(name, out var value))
            return value;

        // Check if it's a constant
        if (ClassUnit.Constants.TryGetValue(name, out var constant))
        {
            // Note: Expression constants cannot be evaluated here because we don't have
            // the runtime context (args, interpreter state). They are handled by the
            // BytecodeInterpreter through LoadClassMember opcode.
            return constant.Type switch
            {
                ConstantType.Int => RuntimeValue.FromInt(constant.IntValue),
                ConstantType.Double => RuntimeValue.FromDouble(constant.DoubleValue),
                ConstantType.String => RuntimeValue.FromString(constant.StringValue),
                ConstantType.Expression => RuntimeValue.Null, // Handled by interpreter
                _ => RuntimeValue.Null
            };
        }

        return RuntimeValue.Null;
    }

    /// <summary>
    /// Set a field value by name.
    /// </summary>
    public void SetField(string name, RuntimeValue value)
    {
        _fields[name] = value;
    }

    /// <summary>
    /// Check if the object has a field with the given name.
    /// </summary>
    public bool HasField(string name)
    {
        if (_fields.ContainsKey(name))
            return true;

        // Check if it's defined as a variable in any class in the hierarchy
        foreach (var unit in _classHierarchy)
        {
            if (unit.Variables.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Check if the object has a method with the given name.
    /// </summary>
    public bool HasMethod(string name)
    {
        return GetMethod(name) != null;
    }

    /// <summary>
    /// Get a method by name using virtual method dispatch.
    /// Searches from most derived class to base classes.
    /// </summary>
    public CompiledFunction? GetMethod(string name)
    {
        // Search the class hierarchy from most derived to base
        foreach (var unit in _classHierarchy)
        {
            if (unit.Functions.TryGetValue(name, out var function))
            {
                return function;
            }
        }
        return null;
    }

    /// <summary>
    /// Get a method by name, returning which class unit defines it.
    /// Useful for determining which class's string pool to use.
    /// </summary>
    public (CompiledFunction? Function, CompiledUnit? DefiningUnit) GetMethodWithUnit(string name)
    {
        // Search the class hierarchy from most derived to base
        foreach (var unit in _classHierarchy)
        {
            if (unit.Functions.TryGetValue(name, out var function))
            {
                return (function, unit);
            }
        }
        return (null, null);
    }

    /// <summary>
    /// Check if this object is an instance of the given class name.
    /// </summary>
    public bool IsInstanceOf(string className)
    {
        // Check all classes in hierarchy
        foreach (var unit in _classHierarchy)
        {
            if (string.Equals(unit.ClassName, className, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public override string ToString() => $"[{ClassName}]";
}
