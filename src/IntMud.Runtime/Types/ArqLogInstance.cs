using System.Text;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an arqlog (log file) instance.
/// Maps to C++ TVarArqLog - append-only log file.
/// </summary>
public sealed class ArqLogInstance : IDisposable
{
    private StreamWriter? _writer;
    private string _filename = "";

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    public bool Valido => _writer != null;

    public bool Existe(string filename)
    {
        try { return File.Exists(filename); } catch { return false; }
    }

    public bool Abrir(string filename)
    {
        try
        {
            Fechar();
            _filename = filename;
            var dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _writer = new StreamWriter(filename, true, Encoding.Latin1);
            _writer.AutoFlush = true;
            return true;
        }
        catch { return false; }
    }

    public void Msg(string text)
    {
        _writer?.WriteLine(text);
    }

    public void Fechar()
    {
        _writer?.Dispose();
        _writer = null;
    }

    public void Dispose()
    {
        Fechar();
    }

    public override string ToString() => $"[ArqLog: {_filename}]";
}
