using System.Runtime.InteropServices;
using System.Text;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for arqtxt (text file) variables.
/// </summary>
public sealed class ArqTxtHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqTxt;
    public override string TypeName => "arqtxt";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var file = new TextFileHandle();
        var handle = GCHandle.Alloc(file);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var file = GetFile(memory);
        return file?.IsOpen ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var file = GetFile(memory);
        return file?.IsOpen == true ? 1 : 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var file = GetFile(memory);
        return file?.Path ?? "";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }
    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        RefHandler.SetPointer(dest, RefHandler.GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left).CompareTo(RefHandler.GetPointer(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    public static TextFileHandle? GetFile(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as TextFileHandle;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var file = GetFile(memory);
        if (file == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "abrir":
            case "open":
                file.Open(context.GetStringArgument(0), context.GetStringArgument(1));
                return true;

            case "fechar":
            case "close":
                file.Close();
                return true;

            case "ler":
            case "read":
                context.SetReturnString(file.ReadLine() ?? "");
                return true;

            case "escrever":
            case "write":
                file.Write(context.GetStringArgument(0));
                return true;

            case "linha":
            case "writeline":
                file.WriteLine(context.GetStringArgument(0));
                return true;

            case "fim":
            case "eof":
                context.SetReturnBool(file.EndOfFile);
                return true;

            case "aberto":
            case "isopen":
                context.SetReturnBool(file.IsOpen);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for arqmem (memory buffer) variables.
/// </summary>
public sealed class ArqMemHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqMem;
    public override string TypeName => "arqmem";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var buffer = new MemoryBuffer();
        var handle = GCHandle.Alloc(buffer);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var buffer = GetBuffer(memory);
        return buffer != null && buffer.Length > 0;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var buffer = GetBuffer(memory);
        return buffer?.Length ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var buffer = GetBuffer(memory);
        return buffer?.AsString() ?? "";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }

    public override void SetText(Span<byte> memory, string value)
    {
        var buffer = GetBuffer(memory);
        buffer?.SetString(value);
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        RefHandler.SetPointer(dest, RefHandler.GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetInt(left).CompareTo(GetInt(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    public static MemoryBuffer? GetBuffer(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as MemoryBuffer;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var buffer = GetBuffer(memory);
        if (buffer == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "limpar":
            case "clear":
                buffer.Clear();
                return true;

            case "tamanho":
            case "length":
                context.SetReturnInt(buffer.Length);
                return true;

            case "posicao":
            case "position":
                context.SetReturnInt(buffer.Position);
                return true;

            case "ir":
            case "seek":
                buffer.Position = context.GetIntArgument(0);
                return true;

            case "ler":
            case "read":
                context.SetReturnInt(buffer.ReadByte());
                return true;

            case "escrever":
            case "write":
                buffer.WriteByte((byte)context.GetIntArgument(0));
                return true;

            case "texto":
            case "text":
                context.SetReturnString(buffer.AsString());
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for arqdir (directory) variables.
/// </summary>
public sealed class ArqDirHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqDir;
    public override string TypeName => "arqdir";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var dir = new DirectoryHandle();
        var handle = GCHandle.Alloc(dir);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var dir = GetDirectory(memory);
        return dir?.IsOpen ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var dir = GetDirectory(memory);
        return dir?.FileCount ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var dir = GetDirectory(memory);
        return dir?.Path ?? "";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }
    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        RefHandler.SetPointer(dest, RefHandler.GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left).CompareTo(RefHandler.GetPointer(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    public static DirectoryHandle? GetDirectory(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as DirectoryHandle;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var dir = GetDirectory(memory);
        if (dir == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "abrir":
            case "open":
                dir.Open(context.GetStringArgument(0), context.GetStringArgument(1));
                return true;

            case "fechar":
            case "close":
                dir.Close();
                return true;

            case "proximo":
            case "next":
                context.SetReturnString(dir.NextFile() ?? "");
                return true;

            case "fim":
            case "eof":
                context.SetReturnBool(dir.EndOfDirectory);
                return true;

            case "total":
            case "count":
                context.SetReturnInt(dir.FileCount);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for arqlog (log file) variables.
/// </summary>
public sealed class ArqLogHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqLog;
    public override string TypeName => "arqlog";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var log = new LogFileHandle();
        var handle = GCHandle.Alloc(log);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var log = GetLog(memory);
        return log?.IsOpen ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var log = GetLog(memory);
        return log?.IsOpen == true ? 1 : 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var log = GetLog(memory);
        return log?.Path ?? "";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }
    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        RefHandler.SetPointer(dest, RefHandler.GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left).CompareTo(RefHandler.GetPointer(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    public static LogFileHandle? GetLog(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as LogFileHandle;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var log = GetLog(memory);
        if (log == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "abrir":
            case "open":
                log.Open(context.GetStringArgument(0));
                return true;

            case "fechar":
            case "close":
                log.Close();
                return true;

            case "escrever":
            case "write":
                log.Write(context.GetStringArgument(0));
                return true;

            case "linha":
            case "writeline":
                log.WriteLine(context.GetStringArgument(0));
                return true;

            default:
                return false;
        }
    }
}

// Helper classes

public class TextFileHandle : IDisposable
{
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;

    public string Path { get; private set; } = "";
    public bool IsOpen => _reader != null || _writer != null;
    public bool EndOfFile => _reader?.EndOfStream ?? true;

    public void Open(string path, string mode)
    {
        Close();
        Path = path;

        try
        {
            switch (mode.ToLowerInvariant())
            {
                case "r":
                case "read":
                    _reader = new StreamReader(path, Encoding.UTF8);
                    break;
                case "w":
                case "write":
                    _writer = new StreamWriter(path, false, Encoding.UTF8);
                    break;
                case "a":
                case "append":
                    _writer = new StreamWriter(path, true, Encoding.UTF8);
                    break;
            }
        }
        catch
        {
            // Ignore file errors
        }
    }

    public void Close()
    {
        _reader?.Dispose();
        _reader = null;
        _writer?.Dispose();
        _writer = null;
    }

    public string? ReadLine() => _reader?.ReadLine();

    public void Write(string text) => _writer?.Write(text);

    public void WriteLine(string text) => _writer?.WriteLine(text);

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }
}

public class MemoryBuffer
{
    private readonly MemoryStream _stream = new();

    public int Length => (int)_stream.Length;
    public int Position
    {
        get => (int)_stream.Position;
        set => _stream.Position = Math.Clamp(value, 0, (int)_stream.Length);
    }

    public void Clear()
    {
        _stream.SetLength(0);
        _stream.Position = 0;
    }

    public int ReadByte() => _stream.ReadByte();

    public void WriteByte(byte value) => _stream.WriteByte(value);

    public string AsString() => Encoding.UTF8.GetString(_stream.ToArray());

    public void SetString(string value)
    {
        Clear();
        var bytes = Encoding.UTF8.GetBytes(value);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Position = 0;
    }
}

public class DirectoryHandle
{
    private string[]? _files;
    private int _index;

    public string Path { get; private set; } = "";
    public bool IsOpen => _files != null;
    public bool EndOfDirectory => _files == null || _index >= _files.Length;
    public int FileCount => _files?.Length ?? 0;

    public void Open(string path, string pattern = "*")
    {
        Close();
        Path = path;

        try
        {
            if (Directory.Exists(path))
            {
                _files = Directory.GetFiles(path, pattern);
                _index = 0;
            }
        }
        catch
        {
            _files = null;
        }
    }

    public void Close()
    {
        _files = null;
        _index = 0;
    }

    public string? NextFile()
    {
        if (_files == null || _index >= _files.Length)
            return null;

        return System.IO.Path.GetFileName(_files[_index++]);
    }
}

public class LogFileHandle : IDisposable
{
    private StreamWriter? _writer;
    private bool _disposed;

    public string Path { get; private set; } = "";
    public bool IsOpen => _writer != null;

    public void Open(string path)
    {
        Close();
        Path = path;

        try
        {
            _writer = new StreamWriter(path, true, Encoding.UTF8);
            _writer.AutoFlush = true;
        }
        catch
        {
            // Ignore errors
        }
    }

    public void Close()
    {
        _writer?.Dispose();
        _writer = null;
    }

    public void Write(string text) => _writer?.Write(text);

    public void WriteLine(string text) => _writer?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}");

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }
}
