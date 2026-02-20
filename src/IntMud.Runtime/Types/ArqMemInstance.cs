using System.Text;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an arqmem (memory buffer) instance.
/// Maps to C++ TVarArqMem - in-memory read/write buffer.
/// </summary>
public sealed class ArqMemInstance
{
    private readonly MemoryStream _stream = new();

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    public int Tamanho => (int)_stream.Length;
    public int Pos
    {
        get => (int)_stream.Position;
        set => _stream.Position = Math.Clamp(value, 0, (int)_stream.Length);
    }
    public bool Eof => _stream.Position >= _stream.Length;

    public string Ler(int count = -1)
    {
        if (count < 0) count = (int)(_stream.Length - _stream.Position);
        var buf = new byte[count];
        int read = _stream.Read(buf, 0, count);
        return Encoding.Latin1.GetString(buf, 0, read);
    }

    public void Escr(string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public int LerBin()
    {
        return _stream.ReadByte();
    }

    public void EscrBin(int value)
    {
        _stream.WriteByte((byte)(value & 0xFF));
    }

    public string LerBinEsp(int count)
    {
        var buf = new byte[count];
        int read = _stream.Read(buf, 0, count);
        return Encoding.Latin1.GetString(buf, 0, read);
    }

    public void AddBin(byte[] data)
    {
        _stream.Write(data, 0, data.Length);
    }

    public void Add(string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        var oldPos = _stream.Position;
        _stream.Position = _stream.Length;
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Position = oldPos;
    }

    public void Limpar()
    {
        _stream.SetLength(0);
        _stream.Position = 0;
    }

    public void Truncar()
    {
        _stream.SetLength(_stream.Position);
    }

    public override string ToString() => $"[ArqMem: {Tamanho} bytes]";
}
