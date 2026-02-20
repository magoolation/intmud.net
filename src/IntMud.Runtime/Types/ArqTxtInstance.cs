using System.Text;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an arqtxt (text file) instance.
/// For reading and writing text files line by line.
/// </summary>
public sealed class ArqTxtInstance : IDisposable
{
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _isOpen;
    private string _filename = "";

    /// <summary>
    /// The owner object that contains this arqtxt variable.
    /// </summary>
    public object? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Check if file is open.
    /// </summary>
    public bool Valido => _isOpen;

    /// <summary>
    /// Check if file exists.
    /// </summary>
    public bool Existe(string filename) => File.Exists(filename);

    /// <summary>
    /// Open file for reading.
    /// </summary>
    public bool Abrir(string filename, string mode = "r")
    {
        try
        {
            Fechar();
            _filename = filename;

            if (mode == "r" || mode == "ler")
            {
                if (!File.Exists(filename))
                    return false;
                _reader = new StreamReader(filename, Encoding.Latin1);
            }
            else if (mode == "w" || mode == "escr")
            {
                _writer = new StreamWriter(filename, false, Encoding.Latin1);
            }
            else if (mode == "a" || mode == "add")
            {
                _writer = new StreamWriter(filename, true, Encoding.Latin1);
            }
            else
            {
                return false;
            }

            _isOpen = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Read a line from the file.
    /// </summary>
    public string Ler()
    {
        if (_reader == null || _reader.EndOfStream)
            return "";
        return _reader.ReadLine() ?? "";
    }

    /// <summary>
    /// Write a line to the file.
    /// </summary>
    public void Escr(string text)
    {
        _writer?.WriteLine(text);
    }

    /// <summary>
    /// Write text without newline.
    /// </summary>
    public void EscrSem(string text)
    {
        _writer?.Write(text);
    }

    /// <summary>
    /// Check if at end of file.
    /// </summary>
    public bool Eof => _reader == null || _reader.EndOfStream;

    /// <summary>
    /// Get current position in file.
    /// </summary>
    public long Pos
    {
        get
        {
            if (_reader != null)
                return _reader.BaseStream.Position;
            if (_writer != null)
                return _writer.BaseStream.Position;
            return 0;
        }
    }

    /// <summary>
    /// Flush buffer to file.
    /// </summary>
    public void Flush()
    {
        _writer?.Flush();
    }

    /// <summary>
    /// Close the file.
    /// </summary>
    public void Fechar()
    {
        _reader?.Dispose();
        _reader = null;
        _writer?.Dispose();
        _writer = null;
        _isOpen = false;
    }

    /// <summary>
    /// Truncate file.
    /// </summary>
    public bool Truncar(string filename)
    {
        try
        {
            File.WriteAllText(filename, "", Encoding.Latin1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Fechar();
    }

    public override string ToString() => $"[ArqTxt: {_filename}]";
}
