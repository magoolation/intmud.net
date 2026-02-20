using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Types;

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
                // Common (static) variables: stored at class level, shared between all objects
                if (variable.IsCommon)
                {
                    // Initialize only once per class - subsequent objects share the same value
                    if (variable.ArraySize > 0)
                    {
                        var array = new List<RuntimeValue>(variable.ArraySize);
                        for (int j = 0; j < variable.ArraySize; j++)
                        {
                            array.Add(CreateInitialValue(variable.TypeName, $"{variable.Name}[{j}]"));
                        }
                        ClassVariableStorage.InitializeIfNew(unit, variable.Name, RuntimeValue.FromArray(array));
                    }
                    else
                    {
                        var initialValue = CreateInitialValue(variable.TypeName, variable.Name);
                        ClassVariableStorage.InitializeIfNew(unit, variable.Name, initialValue);
                    }
                    continue;
                }

                if (!_fields.ContainsKey(variable.Name))
                {
                    // Check if this is an array variable (e.g., textotxt linhas.10)
                    if (variable.ArraySize > 0)
                    {
                        // Create array of values as a List<RuntimeValue>
                        var array = new List<RuntimeValue>(variable.ArraySize);
                        for (int j = 0; j < variable.ArraySize; j++)
                        {
                            array.Add(CreateInitialValue(variable.TypeName, $"{variable.Name}[{j}]"));
                        }
                        _fields[variable.Name] = RuntimeValue.FromArray(array);
                    }
                    else
                    {
                        // Check for special types
                        var initialValue = CreateInitialValue(variable.TypeName, variable.Name);
                        _fields[variable.Name] = initialValue;
                    }
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
        // Check if this is a common (static) variable - stored at class level
        foreach (var unit in _classHierarchy)
        {
            var variable = unit.Variables.FirstOrDefault(
                v => v.IsCommon && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (variable != null)
                return ClassVariableStorage.Get(unit, name);
        }

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
        // Check if this is a common (static) variable - stored at class level
        foreach (var unit in _classHierarchy)
        {
            var variable = unit.Variables.FirstOrDefault(
                v => v.IsCommon && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (variable != null)
            {
                ClassVariableStorage.Set(unit, name, value);
                return;
            }
        }

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
        // (includes common variables stored at class level)
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
    /// Get a constant by name from the class hierarchy.
    /// Used for expression constants like: const msg = _tela.msg(arg0)
    /// </summary>
    public CompiledConstant? GetConstant(string name)
    {
        // Search the class hierarchy from most derived to base
        foreach (var unit in _classHierarchy)
        {
            if (unit.Constants.TryGetValue(name, out var constant))
            {
                return constant;
            }
        }
        return null;
    }

    /// <summary>
    /// Get a constant by name, returning which class unit defines it.
    /// Useful for determining which class's string pool to use for expression constants.
    /// </summary>
    public (CompiledConstant? Constant, CompiledUnit? DefiningUnit) GetConstantWithUnit(string name)
    {
        // Search the class hierarchy from most derived to base
        foreach (var unit in _classHierarchy)
        {
            if (unit.Constants.TryGetValue(name, out var constant))
            {
                return (constant, unit);
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

    /// <summary>
    /// Previous object in the per-class doubly linked list (like C++ TObjeto::Antes).
    /// </summary>
    public BytecodeRuntimeObject? PreviousObject { get; set; }

    /// <summary>
    /// Next object in the per-class doubly linked list (like C++ TObjeto::Depois).
    /// </summary>
    public BytecodeRuntimeObject? NextObject { get; set; }

    public override string ToString() => $"[{ClassName}]";

    /// <summary>
    /// Create an initial value for a variable based on its type.
    /// Handles special types like telatxt, inttempo, etc.
    /// </summary>
    private RuntimeValue CreateInitialValue(string typeName, string variableName)
    {
        var lowerType = typeName.ToLowerInvariant();

        switch (lowerType)
        {
            // telatxt - console text type
            case "telatxt":
                return RuntimeValue.FromObject(new TelaTxtInstance
                {
                    Owner = this,
                    VariableName = variableName,
                    IsActive = true
                });

            // textotxt - multi-line text container
            case "textotxt":
                return RuntimeValue.FromObject(new TextoTxtInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // textopos - text position cursor
            case "textopos":
                return RuntimeValue.FromObject(new TextoPosInstance(new TextoTxtInstance(), 0));

            // listaobj - object list
            case "listaobj":
                return RuntimeValue.FromObject(new ListaObjInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // listaitem - list item cursor
            case "listaitem":
                return RuntimeValue.Null; // Created dynamically

            // indiceobj - indexed object reference
            case "indiceobj":
                return RuntimeValue.FromObject(new IndiceObjInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // indiceitem - index item cursor
            case "indiceitem":
                return RuntimeValue.FromObject(new IndiceItemInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // inttempo - timer
            case "inttempo":
                return RuntimeValue.FromObject(new IntTempoInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // intexec - execution trigger
            case "intexec":
                return RuntimeValue.FromObject(new IntExecInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // intinc - incrementing counter
            case "intinc":
                return RuntimeValue.FromObject(new IntIncInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // datahora - date/time
            case "datahora":
                return RuntimeValue.FromObject(new DataHoraInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // debug
            case "debug":
                return RuntimeValue.FromObject(new DebugInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // arqtxt - text file
            case "arqtxt":
                return RuntimeValue.FromObject(new ArqTxtInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // arqsav - save file
            case "arqsav":
                return RuntimeValue.FromObject(new ArqSavInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // serv - server socket
            case "serv":
                return RuntimeValue.FromObject(new ServInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // socket - TCP socket connection
            case "socket":
                return RuntimeValue.FromObject(new SocketInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // textovar - text with variable references
            case "textovar":
                return RuntimeValue.FromObject(new TextoVarInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // textoobj - text with object references
            case "textoobj":
                return RuntimeValue.FromObject(new TextoObjInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // nomeobj - object name index
            case "nomeobj":
                return RuntimeValue.FromObject(new NomeObjInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // arqdir - directory operations
            case "arqdir":
                return RuntimeValue.FromObject(new ArqDirInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // arqlog - log file
            case "arqlog":
                return RuntimeValue.FromObject(new ArqLogInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // arqprog - program source reader
            case "arqprog":
                return RuntimeValue.FromObject(new ArqProgInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // arqexec - external process execution
            case "arqexec":
                return RuntimeValue.FromObject(new ArqExecInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // arqmem - memory buffer
            case "arqmem":
                return RuntimeValue.FromObject(new ArqMemInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // prog - program introspection
            case "prog":
                return RuntimeValue.FromObject(new ProgInstance
                {
                    Owner = this,
                    VariableName = variableName
                });

            // ref - object reference (initialized to null)
            case "ref":
                return RuntimeValue.Null;

            // Default to null for unknown types
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Get the type name of a variable.
    /// </summary>
    public string? GetFieldTypeName(string name)
    {
        foreach (var unit in _classHierarchy)
        {
            var variable = unit.Variables.FirstOrDefault(
                v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (variable != null)
                return variable.TypeName;
        }
        return null;
    }
}
