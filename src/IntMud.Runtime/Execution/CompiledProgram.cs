using IntMud.Compiler.Ast;

namespace IntMud.Runtime.Execution;

/// <summary>
/// A compiled IntMUD program ready for execution.
/// </summary>
public class CompiledProgram
{
    /// <summary>
    /// Program options from file headers.
    /// </summary>
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All classes defined in the program.
    /// </summary>
    public Dictionary<string, CompiledClass> Classes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Source files that make up this program.
    /// </summary>
    public List<string> SourceFiles { get; } = new();

    /// <summary>
    /// Maximum instructions per execution cycle.
    /// </summary>
    public int MaxInstructions { get; set; } = 5000;

    /// <summary>
    /// Whether to show the text window.
    /// </summary>
    public bool ShowTelaTxt { get; set; }

    /// <summary>
    /// Get a class by name.
    /// </summary>
    public CompiledClass? GetClass(string name)
    {
        return Classes.GetValueOrDefault(name);
    }

    /// <summary>
    /// Add a class to the program.
    /// </summary>
    public void AddClass(CompiledClass compiledClass)
    {
        Classes[compiledClass.Name] = compiledClass;
    }
}

/// <summary>
/// A compiled class definition.
/// </summary>
public class CompiledClass
{
    /// <summary>
    /// The class name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Source file where the class was defined.
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// Line number in source file.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Base classes (inheritance).
    /// </summary>
    public List<string> BaseClasses { get; } = new();

    /// <summary>
    /// Resolved base class references (after linking).
    /// </summary>
    public List<CompiledClass> ResolvedBases { get; } = new();

    /// <summary>
    /// Class variables (not in functions).
    /// </summary>
    public Dictionary<string, CompiledVariable> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Functions defined in this class.
    /// </summary>
    public Dictionary<string, CompiledFunction> Functions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Constants defined in this class.
    /// </summary>
    public Dictionary<string, CompiledConstant> Constants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Common (static) variable values shared across all objects.
    /// </summary>
    public Dictionary<string, Values.RuntimeValue> CommonVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cached constant values.
    /// </summary>
    public Dictionary<string, Values.RuntimeValue> CachedConstants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All objects of this class.
    /// </summary>
    public List<RuntimeObject> Objects { get; } = new();

    /// <summary>
    /// Object index for iteration (used by _objind).
    /// </summary>
    public int ObjectIndex { get; set; }

    /// <summary>
    /// Total number of objects created (ever).
    /// </summary>
    public long TotalObjectsCreated { get; set; }

    /// <summary>
    /// Look up a member (variable, function, constant) by name.
    /// Searches this class and all base classes.
    /// </summary>
    public CompiledMember? LookupMember(string name)
    {
        // Check this class first
        if (Variables.TryGetValue(name, out var variable))
            return variable;
        if (Functions.TryGetValue(name, out var function))
            return function;
        if (Constants.TryGetValue(name, out var constant))
            return constant;

        // Check base classes
        foreach (var baseClass in ResolvedBases)
        {
            var member = baseClass.LookupMember(name);
            if (member != null)
                return member;
        }

        return null;
    }

    /// <summary>
    /// Look up a function by name.
    /// </summary>
    public CompiledFunction? LookupFunction(string name)
    {
        if (Functions.TryGetValue(name, out var function))
            return function;

        foreach (var baseClass in ResolvedBases)
        {
            var func = baseClass.LookupFunction(name);
            if (func != null)
                return func;
        }

        return null;
    }

    /// <summary>
    /// Look up a variable by name.
    /// </summary>
    public CompiledVariable? LookupVariable(string name)
    {
        if (Variables.TryGetValue(name, out var variable))
            return variable;

        foreach (var baseClass in ResolvedBases)
        {
            var v = baseClass.LookupVariable(name);
            if (v != null)
                return v;
        }

        return null;
    }

    /// <summary>
    /// Get the first object of this class.
    /// </summary>
    public RuntimeObject? FirstObject => Objects.Count > 0 ? Objects[0] : null;

    /// <summary>
    /// Get the last object of this class.
    /// </summary>
    public RuntimeObject? LastObject => Objects.Count > 0 ? Objects[^1] : null;

    /// <summary>
    /// Get object count.
    /// </summary>
    public int ObjectCount => Objects.Count;

    /// <summary>
    /// Get a common (static) variable value.
    /// </summary>
    public Values.RuntimeValue GetCommonVariable(string name)
    {
        if (CommonVariables.TryGetValue(name, out var value))
            return value;

        // Check base classes
        foreach (var baseClass in ResolvedBases)
        {
            var v = baseClass.GetCommonVariable(name);
            if (!v.IsNull)
                return v;
        }

        return Values.RuntimeValue.Null;
    }

    /// <summary>
    /// Set a common (static) variable value.
    /// </summary>
    public void SetCommonVariable(string name, Values.RuntimeValue value)
    {
        // Check if the variable is defined in this class
        if (Variables.TryGetValue(name, out var varDef) && varDef.IsComum)
        {
            CommonVariables[name] = value;
            return;
        }

        // Check base classes
        foreach (var baseClass in ResolvedBases)
        {
            if (baseClass.LookupVariable(name) is { IsComum: true })
            {
                baseClass.SetCommonVariable(name, value);
                return;
            }
        }

        // Variable not found - store in this class
        CommonVariables[name] = value;
    }

    /// <summary>
    /// Look up a constant by name.
    /// </summary>
    public CompiledConstant? LookupConstant(string name)
    {
        if (Constants.TryGetValue(name, out var constant))
            return constant;

        foreach (var baseClass in ResolvedBases)
        {
            var c = baseClass.LookupConstant(name);
            if (c != null)
                return c;
        }

        return null;
    }

    /// <summary>
    /// Check if this class inherits from another class.
    /// </summary>
    public bool InheritsFrom(string className)
    {
        if (Name.Equals(className, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var baseClass in ResolvedBases)
        {
            if (baseClass.InheritsFrom(className))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get all ancestor classes (depth-first).
    /// </summary>
    public IEnumerable<CompiledClass> GetAllAncestors()
    {
        foreach (var baseClass in ResolvedBases)
        {
            yield return baseClass;
            foreach (var ancestor in baseClass.GetAllAncestors())
            {
                yield return ancestor;
            }
        }
    }
}

/// <summary>
/// Base class for compiled members.
/// </summary>
public abstract class CompiledMember
{
    public required string Name { get; init; }
    public string? SourceFile { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}

/// <summary>
/// A compiled variable declaration.
/// </summary>
public class CompiledVariable : CompiledMember
{
    public required string TypeName { get; init; }
    public bool IsComum { get; set; }  // Static/shared
    public bool IsSav { get; set; }    // Should be saved
    public int VectorSize { get; set; }
    public ExpressionNode? Initializer { get; set; }
}

/// <summary>
/// A compiled function.
/// </summary>
public class CompiledFunction : CompiledMember
{
    public bool IsVarFunc { get; set; }  // varfunc
    public List<StatementNode> Body { get; } = new();
}

/// <summary>
/// A compiled constant.
/// </summary>
public class CompiledConstant : CompiledMember
{
    public bool IsVarConst { get; set; }  // varconst
    public required ExpressionNode Value { get; init; }
}

/// <summary>
/// A runtime object instance.
/// </summary>
public class RuntimeObject
{
    /// <summary>
    /// The class this object belongs to.
    /// </summary>
    public required CompiledClass Class { get; init; }

    /// <summary>
    /// Unique object ID.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Instance variable values.
    /// </summary>
    public Dictionary<string, Values.RuntimeValue> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this object is marked for deletion.
    /// </summary>
    public bool IsMarkedForDeletion { get; set; }

    /// <summary>
    /// Reference count for garbage collection.
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// Index within the class's object list.
    /// </summary>
    public int IndexInClass { get; set; } = -1;

    /// <summary>
    /// Get a variable value, including common (static) variables.
    /// </summary>
    public Values.RuntimeValue GetVariable(string name)
    {
        // Check instance variables first
        if (Variables.TryGetValue(name, out var value))
            return value;

        // Check if it's a common variable
        var varDef = Class.LookupVariable(name);
        if (varDef != null && varDef.IsComum)
        {
            return Class.GetCommonVariable(name);
        }

        return Values.RuntimeValue.Null;
    }

    /// <summary>
    /// Set a variable value, including common (static) variables.
    /// </summary>
    public void SetVariable(string name, Values.RuntimeValue value)
    {
        // Check if it's a common variable
        var varDef = Class.LookupVariable(name);
        if (varDef != null && varDef.IsComum)
        {
            Class.SetCommonVariable(name, value);
            return;
        }

        Variables[name] = value;
    }

    /// <summary>
    /// Check if this object has a variable (instance or inherited).
    /// </summary>
    public bool HasVariable(string name)
    {
        return Variables.ContainsKey(name) || Class.LookupVariable(name) != null;
    }

    /// <summary>
    /// Get the next object in the class's object list.
    /// </summary>
    public RuntimeObject? NextObject
    {
        get
        {
            var idx = Class.Objects.IndexOf(this);
            if (idx >= 0 && idx < Class.Objects.Count - 1)
                return Class.Objects[idx + 1];
            return null;
        }
    }

    /// <summary>
    /// Get the previous object in the class's object list.
    /// </summary>
    public RuntimeObject? PreviousObject
    {
        get
        {
            var idx = Class.Objects.IndexOf(this);
            if (idx > 0)
                return Class.Objects[idx - 1];
            return null;
        }
    }

    /// <summary>
    /// Check if this object is an instance of a class (including inherited).
    /// </summary>
    public bool IsInstanceOf(string className)
    {
        return Class.InheritsFrom(className);
    }

    /// <summary>
    /// Get a function from this object's class.
    /// </summary>
    public CompiledFunction? GetFunction(string name)
    {
        return Class.LookupFunction(name);
    }

    /// <summary>
    /// Get a constant from this object's class.
    /// </summary>
    public CompiledConstant? GetConstant(string name)
    {
        return Class.LookupConstant(name);
    }

    public override string ToString() => $"{Class.Name}[{Id}]";
}
