using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an indiceobj (indexed object reference) instance.
/// This provides a named key for looking up objects.
/// When assigned a string, it registers the owner object with that name.
/// </summary>
public sealed class IndiceObjInstance
{
    private string _nome = "";

    // Global registry of indexed objects
    private static readonly Dictionary<string, BytecodeRuntimeObject> _globalIndex
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The owner object that contains this indiceobj variable.
    /// </summary>
    public BytecodeRuntimeObject? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// The index name/key.
    /// Setting this registers the owner object with the new name.
    /// </summary>
    public string Nome
    {
        get => _nome;
        set
        {
            // Unregister old name
            if (!string.IsNullOrEmpty(_nome))
            {
                _globalIndex.Remove(_nome);
            }

            _nome = value ?? "";

            // Register new name
            if (!string.IsNullOrEmpty(_nome) && Owner != null)
            {
                _globalIndex[_nome] = Owner;
            }
        }
    }

    /// <summary>
    /// Look up an object by name.
    /// </summary>
    public static BytecodeRuntimeObject? Obj(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        _globalIndex.TryGetValue(name, out var obj);
        return obj;
    }

    /// <summary>
    /// Get first object in the index (alphabetically).
    /// </summary>
    public static BytecodeRuntimeObject? Ini()
    {
        if (_globalIndex.Count == 0)
            return null;
        var firstKey = _globalIndex.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).First();
        return _globalIndex[firstKey];
    }

    /// <summary>
    /// Get last object in the index (alphabetically).
    /// </summary>
    public static BytecodeRuntimeObject? Fim()
    {
        if (_globalIndex.Count == 0)
            return null;
        var lastKey = _globalIndex.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Last();
        return _globalIndex[lastKey];
    }

    /// <summary>
    /// Unregister this object from the index.
    /// </summary>
    public void Unregister()
    {
        if (!string.IsNullOrEmpty(_nome))
        {
            _globalIndex.Remove(_nome);
            _nome = "";
        }
    }

    /// <summary>
    /// Clear all registered objects (for cleanup).
    /// </summary>
    public static void ClearAll()
    {
        _globalIndex.Clear();
    }

    public override string ToString() => $"[IndiceObj: {_nome}]";
}
