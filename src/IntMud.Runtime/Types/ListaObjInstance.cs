using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a listaobj (object list) instance.
/// This implements the object list functionality from original IntMUD.
/// </summary>
public sealed class ListaObjInstance
{
    private readonly List<object> _objects = new();
    private readonly List<ListaItemInstance> _items = new();

    /// <summary>
    /// The owner object that contains this listaobj variable.
    /// </summary>
    public object? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Number of items in the list.
    /// </summary>
    public int Total => _objects.Count;

    /// <summary>
    /// Get all objects in the list.
    /// </summary>
    public IReadOnlyList<object> Objects => _objects;

    /// <summary>
    /// Add object at the beginning.
    /// </summary>
    public ListaItemInstance AddIni(object obj)
    {
        _objects.Insert(0, obj);
        var item = new ListaItemInstance(this, 0);
        _items.Add(item);

        // Update existing item indices
        foreach (var existingItem in _items)
        {
            if (existingItem != item && existingItem.Index >= 0)
                existingItem.Index++;
        }

        return item;
    }

    /// <summary>
    /// Add object at the end.
    /// </summary>
    public ListaItemInstance AddFim(object obj)
    {
        _objects.Add(obj);
        var item = new ListaItemInstance(this, _objects.Count - 1);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Add object at the beginning only if not already in list.
    /// </summary>
    public ListaItemInstance? AddIni1(object obj)
    {
        if (_objects.Contains(obj))
            return null;
        return AddIni(obj);
    }

    /// <summary>
    /// Add object at the end only if not already in list.
    /// </summary>
    public ListaItemInstance? AddFim1(object obj)
    {
        if (_objects.Contains(obj))
            return null;
        return AddFim(obj);
    }

    /// <summary>
    /// Get a ListaItem pointing to the first object.
    /// </summary>
    public ListaItemInstance? Ini()
    {
        if (_objects.Count == 0)
            return null;
        var item = new ListaItemInstance(this, 0);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Get a ListaItem pointing to the last object.
    /// </summary>
    public ListaItemInstance? Fim()
    {
        if (_objects.Count == 0)
            return null;
        var item = new ListaItemInstance(this, _objects.Count - 1);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Get object at index.
    /// </summary>
    public object? GetObject(int index)
    {
        if (index >= 0 && index < _objects.Count)
            return _objects[index];
        return null;
    }

    /// <summary>
    /// Remove object at index.
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index >= 0 && index < _objects.Count)
        {
            _objects.RemoveAt(index);

            // Update item indices
            foreach (var item in _items)
            {
                if (item.Index > index)
                    item.Index--;
                else if (item.Index == index)
                    item.Invalidate();
            }
        }
    }

    /// <summary>
    /// Remove ALL occurrences of a specific object from list.
    /// Matches C++ behavior which removes every occurrence, not just the first.
    /// </summary>
    public int Remove(object obj)
    {
        int count = 0;
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_objects[i], obj))
            {
                RemoveAt(i);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Remove duplicate objects from the list, keeping the first occurrence.
    /// Matches C++ behavior: iterates forward, marks first occurrence, removes subsequent.
    /// </summary>
    public int RemoveDuplicates()
    {
        int count = 0;
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < _objects.Count; )
        {
            if (!seen.Add(_objects[i]))
            {
                RemoveAt(i);
                count++;
                // Don't increment i — next element shifts down
            }
            else
            {
                i++;
            }
        }
        return count;
    }

    /// <summary>
    /// Clear the list.
    /// </summary>
    public void Limpar()
    {
        _objects.Clear();
        foreach (var item in _items)
        {
            item.Invalidate();
        }
    }

    /// <summary>
    /// Delete all objects in the list.
    /// </summary>
    public void Apagar()
    {
        // In IntMUD, this would delete the actual objects
        // For now, just clear the list
        Limpar();
    }

    /// <summary>
    /// Check if list contains object.
    /// </summary>
    public bool Possui(object obj)
    {
        return _objects.Contains(obj);
    }

    /// <summary>
    /// Shuffle objects randomly.
    /// </summary>
    public void Rand()
    {
        var rnd = new Random();
        int n = _objects.Count;
        while (n > 1)
        {
            n--;
            int k = rnd.Next(n + 1);
            (_objects[k], _objects[n]) = (_objects[n], _objects[k]);
        }

        // Invalidate all items since indices changed
        foreach (var item in _items)
        {
            item.Invalidate();
        }
    }

    /// <summary>
    /// Reverse order of objects.
    /// </summary>
    public void Inverter()
    {
        _objects.Reverse();

        // Invalidate all items since indices changed
        foreach (var item in _items)
        {
            item.Invalidate();
        }
    }

    /// <summary>
    /// Get first object.
    /// </summary>
    public object? ObjIni => _objects.Count > 0 ? _objects[0] : null;

    /// <summary>
    /// Get last object.
    /// </summary>
    public object? ObjFim => _objects.Count > 0 ? _objects[^1] : null;

    /// <summary>
    /// Unregister an item.
    /// </summary>
    internal void UnregisterItem(ListaItemInstance item)
    {
        _items.Remove(item);
    }

    /// <summary>
    /// Insert object at specific index.
    /// </summary>
    public void InsertAt(int index, object obj)
    {
        if (index < 0) index = 0;
        if (index > _objects.Count) index = _objects.Count;

        _objects.Insert(index, obj);

        // Update item indices
        foreach (var item in _items)
        {
            if (item.Index >= index)
                item.Index++;
        }
    }

    public override string ToString() => $"[ListaObj: {Total} items]";
}
