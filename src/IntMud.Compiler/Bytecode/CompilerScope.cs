using IntMud.Core.Text;

namespace IntMud.Compiler.Bytecode;

/// <summary>
/// Types of variables in the scope.
/// </summary>
public enum VariableKind
{
    Unknown,
    Local,
    Global,
    Argument,
    Constant
}

/// <summary>
/// Information about a local variable.
/// </summary>
public sealed class LocalVariableInfo
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public int Index { get; init; }
}

/// <summary>
/// Represents a compilation scope for variable resolution.
/// </summary>
public sealed class CompilerScope
{
    private readonly CompilerScope? _parent;
    private readonly Dictionary<string, int> _locals = new(IntMudNameComparer.Instance);
    private readonly Dictionary<string, int> _globals = new(IntMudNameComparer.Instance);
    private readonly HashSet<string> _constants = new(IntMudNameComparer.Instance);
    private readonly List<LocalVariableInfo> _localVariables = new();
    private int _nextLocalIndex;

    public CompilerScope(CompilerScope? parent)
    {
        _parent = parent;
        _nextLocalIndex = 0;
    }

    /// <summary>
    /// Define a local variable in this scope.
    /// </summary>
    public int DefineLocal(string name, string typeName)
    {
        if (_locals.ContainsKey(name))
        {
            throw new CompilerException($"Variable '{name}' is already defined in this scope");
        }

        var index = _nextLocalIndex++;
        _locals[name] = index;
        _localVariables.Add(new LocalVariableInfo
        {
            Name = name,
            TypeName = typeName,
            Index = index
        });
        return index;
    }

    /// <summary>
    /// Define a global variable.
    /// </summary>
    public void DefineVariable(string name, int index)
    {
        _globals[name] = index;
    }

    /// <summary>
    /// Define a constant.
    /// </summary>
    public void DefineConstant(string name)
    {
        _constants.Add(name);
    }

    /// <summary>
    /// Resolve a variable name to its kind and index.
    /// </summary>
    public (VariableKind Kind, int Index) ResolveVariable(string name)
    {
        // Check local scope first
        if (_locals.TryGetValue(name, out var localIndex))
        {
            return (VariableKind.Local, localIndex);
        }

        // Check parent scope for locals
        if (_parent != null)
        {
            var (kind, index) = _parent.ResolveVariable(name);
            if (kind != VariableKind.Unknown)
            {
                return (kind, index);
            }
        }

        // Check globals (only in root scope)
        if (_parent == null)
        {
            if (_globals.TryGetValue(name, out var globalIndex))
            {
                return (VariableKind.Global, globalIndex);
            }

            if (_constants.Contains(name))
            {
                return (VariableKind.Constant, 0);
            }
        }
        else
        {
            // Check root scope globals
            var root = GetRoot();
            if (root._globals.TryGetValue(name, out var globalIndex))
            {
                return (VariableKind.Global, globalIndex);
            }

            if (root._constants.Contains(name))
            {
                return (VariableKind.Constant, 0);
            }
        }

        // Not found - assume it's a global (will be resolved at runtime)
        return (VariableKind.Global, -1);
    }

    /// <summary>
    /// Check if a name exists in this scope or parent scopes.
    /// </summary>
    public bool IsDefined(string name)
    {
        if (_locals.ContainsKey(name) || _globals.ContainsKey(name) || _constants.Contains(name))
            return true;

        return _parent?.IsDefined(name) ?? false;
    }

    /// <summary>
    /// Get the root scope.
    /// </summary>
    public CompilerScope GetRoot()
    {
        return _parent?.GetRoot() ?? this;
    }

    /// <summary>
    /// Get all local variables defined in this scope.
    /// </summary>
    public IReadOnlyList<LocalVariableInfo> GetLocalVariables() => _localVariables;

    /// <summary>
    /// Get the number of local variables in this scope.
    /// </summary>
    public int LocalCount => _nextLocalIndex;
}
