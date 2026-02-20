using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a textoobj (text with object references) instance.
/// Maps to C++ TTextoObj - a named collection of object references.
/// </summary>
public sealed class TextoObjInstance
{
    private readonly SortedDictionary<string, BytecodeRuntimeObject?> _objects = new(StringComparer.OrdinalIgnoreCase);
    private IEnumerator<KeyValuePair<string, BytecodeRuntimeObject?>>? _iterator;

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    public int Total => _objects.Count;

    public BytecodeRuntimeObject? Valor(string name)
    {
        return _objects.TryGetValue(name, out var obj) ? obj : null;
    }

    public BytecodeRuntimeObject? ValorIni()
    {
        if (_objects.Count == 0) return null;
        return _objects.First().Value;
    }

    public BytecodeRuntimeObject? ValorFim()
    {
        if (_objects.Count == 0) return null;
        return _objects.Last().Value;
    }

    public void Mudar(string name, BytecodeRuntimeObject? obj)
    {
        _objects[name] = obj;
    }

    public string Ini()
    {
        if (_objects.Count == 0) return "";
        _iterator = _objects.GetEnumerator();
        _iterator.MoveNext();
        return _iterator.Current.Key;
    }

    public string Fim()
    {
        if (_objects.Count == 0) return "";
        return _objects.Last().Key;
    }

    public string Depois()
    {
        if (_iterator == null) return "";
        return _iterator.MoveNext() ? _iterator.Current.Key : "";
    }

    public string Antes()
    {
        return "";
    }

    public string NomeVar()
    {
        if (_iterator == null) return "";
        return _iterator.Current.Key;
    }

    public void Apagar(string name)
    {
        _objects.Remove(name);
    }

    public void Limpar()
    {
        _objects.Clear();
        _iterator = null;
    }

    public override string ToString() => $"[TextoObj: {Total} refs]";
}
