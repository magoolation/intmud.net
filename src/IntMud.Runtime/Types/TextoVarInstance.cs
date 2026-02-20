// TextoVarInstance.cs
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a textovar (text with variable references) instance.
/// Maps to C++ TTextoVar - a named collection of variables indexed by string key.
/// </summary>
public sealed class TextoVarInstance
{
    private readonly SortedDictionary<string, RuntimeValue> _variables = new(StringComparer.OrdinalIgnoreCase);
    private IEnumerator<KeyValuePair<string, RuntimeValue>>? _iterator;
    private string _currentKey = "";

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    /// <summary>Total number of variables.</summary>
    public int Total => _variables.Count;

    /// <summary>Get value by name.</summary>
    public RuntimeValue Valor(string name)
    {
        return _variables.TryGetValue(name, out var val) ? val : RuntimeValue.Null;
    }

    /// <summary>Get first variable value.</summary>
    public RuntimeValue ValorIni()
    {
        if (_variables.Count == 0) return RuntimeValue.Null;
        return _variables.First().Value;
    }

    /// <summary>Get last variable value.</summary>
    public RuntimeValue ValorFim()
    {
        if (_variables.Count == 0) return RuntimeValue.Null;
        return _variables.Last().Value;
    }

    /// <summary>Set or change a variable.</summary>
    public void Mudar(string name, RuntimeValue value)
    {
        _variables[name] = value;
    }

    /// <summary>Get name of first variable.</summary>
    public string Ini()
    {
        if (_variables.Count == 0) return "";
        _iterator = _variables.GetEnumerator();
        _iterator.MoveNext();
        _currentKey = _iterator.Current.Key;
        return _currentKey;
    }

    /// <summary>Get name of last variable.</summary>
    public string Fim()
    {
        if (_variables.Count == 0) return "";
        return _variables.Last().Key;
    }

    /// <summary>Get name of next variable.</summary>
    public string Depois()
    {
        if (_iterator == null) return "";
        if (_iterator.MoveNext())
        {
            _currentKey = _iterator.Current.Key;
            return _currentKey;
        }
        _currentKey = "";
        return "";
    }

    /// <summary>Get name of previous variable.</summary>
    public string Antes()
    {
        if (string.IsNullOrEmpty(_currentKey) || _variables.Count == 0)
            return "";

        // Find the key before _currentKey in the sorted order
        string? prevKey = null;
        foreach (var kvp in _variables)
        {
            if (StringComparer.OrdinalIgnoreCase.Compare(kvp.Key, _currentKey) >= 0)
                break;
            prevKey = kvp.Key;
        }

        if (prevKey == null)
            return "";

        // Reset iterator to the previous key position
        _currentKey = prevKey;
        _iterator = _variables.GetEnumerator();
        while (_iterator.MoveNext())
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(_iterator.Current.Key, prevKey))
                break;
        }

        return _currentKey;
    }

    /// <summary>Get variable name at current position.</summary>
    public string NomeVar()
    {
        if (_iterator == null) return "";
        return _iterator.Current.Key;
    }

    /// <summary>Get type character of a variable. ' '=text, '_'=number, '@'=int, '$'=object.</summary>
    public string Tipo(string name)
    {
        if (!_variables.TryGetValue(name, out var val)) return "";
        return val.Type switch
        {
            RuntimeValueType.String => " ",
            RuntimeValueType.Integer => "@",
            RuntimeValueType.Double => "_",
            RuntimeValueType.Object => "$",
            _ => ""
        };
    }

    /// <summary>Clear all variables.</summary>
    public void Limpar()
    {
        _variables.Clear();
        _iterator = null;
    }

    public override string ToString() => $"[TextoVar: {Total} vars]";
}
