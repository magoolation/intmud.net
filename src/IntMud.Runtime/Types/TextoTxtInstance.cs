namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a textotxt (multi-line text container) instance.
/// This implements the text document functionality from original IntMUD.
/// Lines are stored as a list of strings for simplicity.
/// </summary>
public sealed class TextoTxtInstance
{
    private readonly List<string> _lines = new();
    private readonly List<TextoPosInstance> _positions = new();

    /// <summary>
    /// The owner object that contains this textotxt variable.
    /// </summary>
    public object? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Number of lines in the text.
    /// </summary>
    public int Linhas => _lines.Count;

    /// <summary>
    /// Total bytes in the text.
    /// </summary>
    public int Bytes => _lines.Sum(l => l.Length + 1); // +1 for newline

    /// <summary>
    /// Get line at index (0-based).
    /// </summary>
    public string GetLine(int index)
    {
        if (index >= 0 && index < _lines.Count)
            return _lines[index];
        return "";
    }

    /// <summary>
    /// Set line at index (0-based).
    /// </summary>
    public void SetLine(int index, string text)
    {
        while (_lines.Count <= index)
            _lines.Add("");
        _lines[index] = text;
    }

    /// <summary>
    /// Add a line at the end.
    /// </summary>
    public void AddFim(string text)
    {
        _lines.Add(text);
    }

    /// <summary>
    /// Add a line at the beginning.
    /// </summary>
    public void AddIni(string text)
    {
        _lines.Insert(0, text);
    }

    /// <summary>
    /// Add lines from another TextoTxt or object with text.
    /// </summary>
    public void AddIni(object obj)
    {
        if (obj is TextoTxtInstance other)
        {
            for (int i = other._lines.Count - 1; i >= 0; i--)
                _lines.Insert(0, other._lines[i]);
        }
    }

    /// <summary>
    /// Add lines from another TextoTxt at the end.
    /// </summary>
    public void AddFim(object obj)
    {
        if (obj is TextoTxtInstance other)
        {
            _lines.AddRange(other._lines);
        }
    }

    /// <summary>
    /// Clear all text.
    /// </summary>
    public void Limpar()
    {
        _lines.Clear();
        // Update all positions
        foreach (var pos in _positions)
        {
            pos.Invalidate();
        }
    }

    /// <summary>
    /// Get a position pointing to the first line.
    /// </summary>
    public TextoPosInstance Ini()
    {
        var pos = new TextoPosInstance(this, 0);
        _positions.Add(pos);
        return pos;
    }

    /// <summary>
    /// Get a position pointing to the last line.
    /// </summary>
    public TextoPosInstance Fim()
    {
        var pos = new TextoPosInstance(this, Math.Max(0, _lines.Count - 1));
        _positions.Add(pos);
        return pos;
    }

    /// <summary>
    /// Read text from a file.
    /// </summary>
    public bool Ler(string filename)
    {
        try
        {
            var fullPath = Path.GetFullPath(filename);
            if (!File.Exists(fullPath))
                return false;

            _lines.Clear();
            var content = File.ReadAllText(fullPath, System.Text.Encoding.Latin1);
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                _lines.Add(line.TrimEnd('\r'));
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Save text to a file.
    /// </summary>
    public bool Salvar(string filename)
    {
        try
        {
            var content = string.Join("\n", _lines);
            File.WriteAllText(filename, content, System.Text.Encoding.Latin1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Shuffle lines randomly.
    /// </summary>
    public void Rand()
    {
        var rnd = new Random();
        int n = _lines.Count;
        while (n > 1)
        {
            n--;
            int k = rnd.Next(n + 1);
            (_lines[k], _lines[n]) = (_lines[n], _lines[k]);
        }
    }

    /// <summary>
    /// Sort lines alphabetically.
    /// </summary>
    public void Ordena()
    {
        _lines.Sort(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Insert a line at specified position.
    /// </summary>
    public void InsertLine(int index, string text)
    {
        if (index < 0) index = 0;
        if (index > _lines.Count) index = _lines.Count;
        _lines.Insert(index, text);

        // Update positions after this line
        foreach (var pos in _positions)
        {
            if (pos.Linha >= index)
                pos.Linha++;
        }
    }

    /// <summary>
    /// Remove a line at specified position.
    /// </summary>
    public void RemoveLine(int index)
    {
        if (index >= 0 && index < _lines.Count)
        {
            _lines.RemoveAt(index);

            // Update positions
            foreach (var pos in _positions)
            {
                if (pos.Linha > index)
                    pos.Linha--;
                else if (pos.Linha == index && pos.Linha >= _lines.Count)
                    pos.Linha = Math.Max(0, _lines.Count - 1);
            }
        }
    }

    /// <summary>
    /// Unregister a position.
    /// </summary>
    internal void UnregisterPosition(TextoPosInstance pos)
    {
        _positions.Remove(pos);
    }

    public override string ToString() => $"[TextoTxt: {Linhas} lines]";
}
