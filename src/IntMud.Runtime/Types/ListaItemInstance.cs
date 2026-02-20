namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a listaitem (list item cursor) instance.
/// This implements list navigation functionality from original IntMUD.
/// </summary>
public sealed class ListaItemInstance
{
    private ListaObjInstance? _lista;
    private int _index;
    private bool _valid;

    /// <summary>
    /// Create an uninitialized list item.
    /// </summary>
    public ListaItemInstance()
    {
        _lista = null;
        _index = -1;
        _valid = false;
    }

    /// <summary>
    /// Create a new list item for the given ListaObj.
    /// </summary>
    public ListaItemInstance(ListaObjInstance lista, int index)
    {
        _lista = lista;
        _index = index;
        _valid = true;
    }

    /// <summary>
    /// The associated ListaObj.
    /// </summary>
    public ListaObjInstance? Lista => _lista;

    /// <summary>
    /// Current index in the list.
    /// </summary>
    public int Index
    {
        get => _index;
        internal set => _index = value;
    }

    /// <summary>
    /// Check if this item is valid.
    /// </summary>
    public bool IsValid => _valid && _lista != null && _index >= 0 && _index < _lista.Total;

    /// <summary>
    /// Get the object at this position.
    /// </summary>
    public object? Obj
    {
        get
        {
            if (!IsValid)
                return null;
            return _lista!.GetObject(_index);
        }
    }

    /// <summary>
    /// Get the parent list object (for objlista property).
    /// </summary>
    public ListaObjInstance? ObjLista => _lista;

    /// <summary>
    /// Total items in the list.
    /// </summary>
    public int Total => _lista?.Total ?? 0;

    /// <summary>
    /// Move to next item (mutates in place, matching C++ FuncDepois behavior).
    /// Returns false if already invalid (matches C++ returning false when ListaX==null).
    /// </summary>
    public bool Depois(int count = 1)
    {
        if (!IsValid)
            return false;
        for (int i = 0; i < count && IsValid; i++)
            _index++;
        return true;
    }

    /// <summary>
    /// Move to previous item (mutates in place, matching C++ FuncAntes behavior).
    /// Returns false if already invalid.
    /// </summary>
    public bool Antes(int count = 1)
    {
        if (!IsValid)
            return false;
        for (int i = 0; i < count && IsValid; i++)
            _index--;
        return true;
    }

    /// <summary>
    /// Get next item (returns new ListaItem or null).
    /// </summary>
    public ListaItemInstance? GetDepois()
    {
        if (_lista == null || _index + 1 >= _lista.Total)
            return null;
        return new ListaItemInstance(_lista, _index + 1);
    }

    /// <summary>
    /// Get previous item (returns new ListaItem or null).
    /// </summary>
    public ListaItemInstance? GetAntes()
    {
        if (_lista == null || _index <= 0)
            return null;
        return new ListaItemInstance(_lista, _index - 1);
    }

    /// <summary>
    /// Get next object.
    /// </summary>
    public object? ObjDepois
    {
        get
        {
            if (_lista == null || _index + 1 >= _lista.Total)
                return null;
            return _lista.GetObject(_index + 1);
        }
    }

    /// <summary>
    /// Get previous object.
    /// </summary>
    public object? ObjAntes
    {
        get
        {
            if (_lista == null || _index <= 0)
                return null;
            return _lista.GetObject(_index - 1);
        }
    }

    /// <summary>
    /// Remove current item from list.
    /// </summary>
    public void Remove()
    {
        if (IsValid)
        {
            _lista!.RemoveAt(_index);
        }
    }

    /// <summary>
    /// Remove previous item.
    /// </summary>
    public void RemoveAntes()
    {
        if (_lista != null && _index > 0)
        {
            _lista.RemoveAt(_index - 1);
            // Our index is adjusted by RemoveAt
        }
    }

    /// <summary>
    /// Remove next item.
    /// </summary>
    public void RemoveDepois()
    {
        if (_lista != null && _index + 1 < _lista.Total)
        {
            _lista.RemoveAt(_index + 1);
        }
    }

    /// <summary>
    /// Add object before current position.
    /// </summary>
    public void AddAntes(object obj)
    {
        if (_lista != null)
        {
            _lista.InsertAt(_index, obj);
            // Our index is adjusted by InsertAt
        }
    }

    /// <summary>
    /// Add object after current position.
    /// </summary>
    public void AddDepois(object obj)
    {
        if (_lista != null)
        {
            _lista.InsertAt(_index + 1, obj);
        }
    }

    /// <summary>
    /// Add object before, only if not already in list.
    /// </summary>
    public bool AddAntes1(object obj)
    {
        if (_lista != null && !_lista.Possui(obj))
        {
            AddAntes(obj);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Add object after, only if not already in list.
    /// </summary>
    public bool AddDepois1(object obj)
    {
        if (_lista != null && !_lista.Possui(obj))
        {
            AddDepois(obj);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Invalidate this item.
    /// </summary>
    internal void Invalidate()
    {
        _valid = false;
        _index = -1;
    }

    public override string ToString() => $"[ListaItem: index {_index}]";
}
