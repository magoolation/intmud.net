using System.Text;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an arqprog (program source) instance.
/// Maps to C++ TVarArqProg - reads source code files.
/// </summary>
public sealed class ArqProgInstance
{
    private string[]? _lines;
    private int _index;
    private string _filename = "";

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    public bool IsOpen => _lines != null;

    public bool Abrir(string filename)
    {
        try
        {
            Fechar();
            _filename = filename;
            if (!File.Exists(filename)) return false;
            _lines = File.ReadAllLines(filename, Encoding.Latin1);
            _index = 0;
            return true;
        }
        catch { return false; }
    }

    public void Fechar()
    {
        _lines = null;
        _index = 0;
    }

    public bool Lin => _lines != null && _index < _lines.Length;

    public string Texto()
    {
        if (_lines == null || _index >= _lines.Length) return "";
        return _lines[_index];
    }

    public void Depois()
    {
        if (_lines != null && _index < _lines.Length)
            _index++;
    }

    public override string ToString() => $"[ArqProg: {_filename}]";
}
