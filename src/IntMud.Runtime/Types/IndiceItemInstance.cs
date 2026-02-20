using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an indiceitem (index item cursor) instance.
/// This is used for looking up objects by name.
/// </summary>
public sealed class IndiceItemInstance
{
    private string _currentKey = "";
    private BytecodeRuntimeObject? _currentObj;

    /// <summary>
    /// The owner object that contains this indiceitem variable.
    /// </summary>
    public object? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// The current key/text.
    /// </summary>
    public string Txt => _currentKey;

    /// <summary>
    /// The current object.
    /// </summary>
    public BytecodeRuntimeObject? Obj => _currentObj;

    /// <summary>
    /// Look up object by name and set as current.
    /// Returns the object if found, null otherwise.
    /// </summary>
    public BytecodeRuntimeObject? LookupObj(string name)
    {
        _currentKey = name ?? "";
        _currentObj = IndiceObjInstance.Obj(name);
        return _currentObj;
    }

    /// <summary>
    /// Move to first item (alphabetically).
    /// </summary>
    public BytecodeRuntimeObject? Ini()
    {
        _currentObj = IndiceObjInstance.Ini();
        // We'd need to track the key too, but for now just set object
        return _currentObj;
    }

    /// <summary>
    /// Move to last item (alphabetically).
    /// </summary>
    public BytecodeRuntimeObject? Fim()
    {
        _currentObj = IndiceObjInstance.Fim();
        return _currentObj;
    }

    /// <summary>
    /// Move to next item (not fully implemented - would need sorted key list).
    /// </summary>
    public void Depois()
    {
        // Would need to maintain sorted key list for proper implementation
        _currentObj = null;
    }

    /// <summary>
    /// Move to previous item (not fully implemented).
    /// </summary>
    public void Antes()
    {
        // Would need to maintain sorted key list for proper implementation
        _currentObj = null;
    }

    public override string ToString() => $"[IndiceItem: {_currentKey}]";
}
