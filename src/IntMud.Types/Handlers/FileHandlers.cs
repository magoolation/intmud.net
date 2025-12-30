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

/// <summary>
/// Handler for arqsav (save file) variables - for saving/loading game state.
/// </summary>
public sealed class ArqSavHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqSav;
    public override string TypeName => "arqsav";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var sav = new SaveFileHandle();
        var handle = GCHandle.Alloc(sav);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var sav = GetSave(memory);
        return sav?.LastOperationSuccess ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var sav = GetSave(memory);
        return sav?.ObjectCount ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var sav = GetSave(memory);
        return sav?.LastFile ?? "";
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

    public static SaveFileHandle? GetSave(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as SaveFileHandle;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var sav = GetSave(memory);
        if (sav == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "salvar":
            case "save":
                sav.Save(context.GetStringArgument(0), context.GetStringArgument(1), false);
                context.SetReturnBool(sav.LastOperationSuccess);
                return true;

            case "salvarcod":
            case "saveencoded":
                sav.Save(context.GetStringArgument(0), context.GetStringArgument(1), true);
                context.SetReturnBool(sav.LastOperationSuccess);
                return true;

            case "ler":
            case "load":
                sav.Load(context.GetStringArgument(0), context.GetStringArgument(1));
                context.SetReturnInt(sav.ObjectCount);
                return true;

            case "existe":
            case "exists":
                context.SetReturnBool(sav.Exists(context.GetStringArgument(0)));
                return true;

            case "apagar":
            case "delete":
                context.SetReturnBool(sav.Delete(context.GetStringArgument(0)));
                return true;

            case "valido":
            case "valid":
                context.SetReturnBool(sav.IsValidPath(context.GetStringArgument(0)));
                return true;

            case "dias":
            case "days":
                context.SetReturnInt(sav.GetDaysSinceModified(context.GetStringArgument(0)));
                return true;

            case "senha":
            case "password":
                context.SetReturnString(sav.EncodePassword(context.GetStringArgument(0)));
                return true;

            case "senhacod":
            case "checkpassword":
                context.SetReturnBool(sav.CheckPassword(context.GetStringArgument(0), context.GetStringArgument(1)));
                return true;

            case "limpar":
            case "cleanup":
                sav.Cleanup(context.GetStringArgument(0), context.GetIntArgument(1));
                return true;

            case "limpou":
            case "cleanedfiles":
                context.SetReturnString(sav.GetCleanedFiles());
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for arqexec (external command execution) variables.
/// </summary>
public sealed class ArqExecHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqExec;
    public override string TypeName => "arqexec";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var exec = new ExecHandle();
        var handle = GCHandle.Alloc(exec);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var exec = GetExec(memory);
        return exec?.IsRunning ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var exec = GetExec(memory);
        return exec?.ExitCode ?? -1;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var exec = GetExec(memory);
        return exec?.Output ?? "";
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

    public static ExecHandle? GetExec(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as ExecHandle;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var exec = GetExec(memory);
        if (exec == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "executar":
            case "execute":
            case "run":
                exec.Execute(context.GetStringArgument(0), context.GetStringArgument(1));
                return true;

            case "enviar":
            case "send":
                exec.SendInput(context.GetStringArgument(0));
                return true;

            case "ler":
            case "read":
                context.SetReturnString(exec.ReadOutput());
                return true;

            case "fechar":
            case "close":
            case "kill":
                exec.Kill();
                return true;

            case "ativo":
            case "isrunning":
                context.SetReturnBool(exec.IsRunning);
                return true;

            case "saida":
            case "exitcode":
                context.SetReturnInt(exec.ExitCode);
                return true;

            case "erro":
            case "error":
                context.SetReturnString(exec.Error);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for arqprog (program/include file) variables.
/// </summary>
public sealed class ArqProgHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ArqProg;
    public override string TypeName => "arqprog";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var prog = new ProgHandle();
        var handle = GCHandle.Alloc(prog);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var prog = GetProg(memory);
        return prog?.IsLoaded ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var prog = GetProg(memory);
        return prog?.LineCount ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var prog = GetProg(memory);
        return prog?.Path ?? "";
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

    public static ProgHandle? GetProg(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as ProgHandle;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var prog = GetProg(memory);
        if (prog == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "abrir":
            case "open":
                prog.Open(context.GetStringArgument(0));
                context.SetReturnBool(prog.IsLoaded);
                return true;

            case "fechar":
            case "close":
                prog.Close();
                return true;

            case "incluir":
            case "include":
                context.SetReturnBool(prog.Include(context.GetStringArgument(0)));
                return true;

            case "linha":
            case "line":
                context.SetReturnString(prog.GetLine(context.GetIntArgument(0)));
                return true;

            case "total":
            case "count":
                context.SetReturnInt(prog.LineCount);
                return true;

            case "existe":
            case "exists":
                context.SetReturnBool(prog.FileExists(context.GetStringArgument(0)));
                return true;

            case "mudar":
            case "reload":
                prog.Reload();
                return true;

            default:
                return false;
        }
    }
}

// Helper classes for new handlers

public class SaveFileHandle
{
    private readonly List<string> _cleanedFiles = new();

    public string LastFile { get; private set; } = "";
    public bool LastOperationSuccess { get; private set; }
    public int ObjectCount { get; private set; }

    public void Save(string filename, string password, bool encoded)
    {
        LastFile = filename;
        LastOperationSuccess = false;
        ObjectCount = 0;

        try
        {
            var dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var writer = new StreamWriter(filename, false, Encoding.UTF8);

            // Write header
            writer.WriteLine("# IntMUD Save File");
            writer.WriteLine($"data={GetCurrentTimeMinutes()}");
            if (!string.IsNullOrEmpty(password))
            {
                var encodedPwd = encoded ? password : EncodePassword(password);
                writer.WriteLine($"senha={encodedPwd}");
            }
            writer.WriteLine("+++");

            // Note: In a full implementation, this would serialize objects
            // For now we just mark as success
            LastOperationSuccess = true;
        }
        catch
        {
            LastOperationSuccess = false;
        }
    }

    public void Load(string filename, string password)
    {
        LastFile = filename;
        ObjectCount = 0;
        LastOperationSuccess = false;

        try
        {
            if (!File.Exists(filename))
                return;

            using var reader = new StreamReader(filename, Encoding.UTF8);
            string? line;
            bool inHeader = true;
            string? savedPassword = null;

            while ((line = reader.ReadLine()) != null)
            {
                if (line == "+++")
                {
                    inHeader = false;
                    continue;
                }

                if (inHeader)
                {
                    if (line.StartsWith("senha="))
                        savedPassword = line[6..];
                }
                else
                {
                    // Count objects (lines starting with specific markers)
                    if (line.StartsWith("[") || line.StartsWith("obj:"))
                        ObjectCount++;
                }
            }

            // Verify password if required
            if (!string.IsNullOrEmpty(savedPassword) && !string.IsNullOrEmpty(password))
            {
                if (!CheckPassword(password, savedPassword))
                    return;
            }

            LastOperationSuccess = true;
        }
        catch
        {
            LastOperationSuccess = false;
        }
    }

    public bool Exists(string filename)
    {
        try
        {
            return File.Exists(filename);
        }
        catch
        {
            return false;
        }
    }

    public bool Delete(string filename)
    {
        try
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool IsValidPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Check for invalid characters and path traversal
        try
        {
            var fullPath = Path.GetFullPath(path);
            return !path.Contains("..") && Path.IsPathRooted(fullPath);
        }
        catch
        {
            return false;
        }
    }

    public int GetDaysSinceModified(string filename)
    {
        try
        {
            if (!File.Exists(filename))
                return -1;

            var lastWrite = File.GetLastWriteTime(filename);
            return (DateTime.Now - lastWrite).Days;
        }
        catch
        {
            return -1;
        }
    }

    public string EncodePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return "";

        // Use SHA1 hash similar to original IntMUD
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var factor = (byte)(new Random().Next(90) + 33);
        var input = new byte[password.Length + 1];
        input[0] = factor;
        Encoding.UTF8.GetBytes(password, 0, password.Length, input, 1);

        var hash = sha1.ComputeHash(input);
        var sb = new StringBuilder();
        sb.Append((char)factor);

        for (int i = 0; i < 20; i += 4)
        {
            uint value = (uint)(hash[i] << 24 | hash[i + 1] << 16 | hash[i + 2] << 8 | hash[i + 3]);
            for (int j = 0; j < 5; j++)
            {
                sb.Append((char)(value % 90 + 33));
                value /= 90;
            }
        }

        return sb.ToString();
    }

    public bool CheckPassword(string password, string encoded)
    {
        if (string.IsNullOrEmpty(encoded) || encoded.Length < 1)
            return string.IsNullOrEmpty(password);

        var factor = encoded[0];
        var check = EncodePasswordWithFactor(password, (byte)factor);
        return check == encoded;
    }

    private string EncodePasswordWithFactor(string password, byte factor)
    {
        if (string.IsNullOrEmpty(password))
            return "";

        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var input = new byte[password.Length + 1];
        input[0] = factor;
        Encoding.UTF8.GetBytes(password, 0, password.Length, input, 1);

        var hash = sha1.ComputeHash(input);
        var sb = new StringBuilder();
        sb.Append((char)factor);

        for (int i = 0; i < 20; i += 4)
        {
            uint value = (uint)(hash[i] << 24 | hash[i + 1] << 16 | hash[i + 2] << 8 | hash[i + 3]);
            for (int j = 0; j < 5; j++)
            {
                sb.Append((char)(value % 90 + 33));
                value /= 90;
            }
        }

        return sb.ToString();
    }

    public void Cleanup(string directory, int maxDays)
    {
        _cleanedFiles.Clear();

        try
        {
            if (!Directory.Exists(directory))
                return;

            var files = Directory.GetFiles(directory, "*.sav");
            foreach (var file in files)
            {
                var days = GetDaysSinceModified(file);
                if (days > maxDays)
                {
                    File.Delete(file);
                    _cleanedFiles.Add(Path.GetFileName(file));
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public string GetCleanedFiles()
    {
        if (_cleanedFiles.Count == 0)
            return "0";

        return "1\n" + string.Join("\n", _cleanedFiles);
    }

    private static int GetCurrentTimeMinutes()
    {
        var now = DateTime.Now;
        var baseDate = new DateTime(1600, 1, 1);
        var days = (int)(now.Date - baseDate).TotalDays - 584389;
        return days * 1440 + now.Hour * 60 + now.Minute;
    }
}

public class ExecHandle : IDisposable
{
    private System.Diagnostics.Process? _process;
    private readonly StringBuilder _output = new();
    private readonly StringBuilder _error = new();
    private bool _disposed;

    public bool IsRunning => _process != null && !_process.HasExited;
    public int ExitCode => _process?.ExitCode ?? -1;
    public string Output => _output.ToString();
    public string Error => _error.ToString();

    public void Execute(string command, string arguments)
    {
        Kill();
        _output.Clear();
        _error.Clear();

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new System.Diagnostics.Process { StartInfo = startInfo };
            _process.OutputDataReceived += (s, e) => { if (e.Data != null) _output.AppendLine(e.Data); };
            _process.ErrorDataReceived += (s, e) => { if (e.Data != null) _error.AppendLine(e.Data); };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _error.AppendLine(ex.Message);
        }
    }

    public void SendInput(string input)
    {
        if (_process != null && !_process.HasExited)
        {
            _process.StandardInput.WriteLine(input);
        }
    }

    public string ReadOutput()
    {
        var result = _output.ToString();
        _output.Clear();
        return result;
    }

    public void Kill()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill();
            }
            catch { }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Kill();
            _disposed = true;
        }
    }
}

public class ProgHandle
{
    private string[] _lines = Array.Empty<string>();
    private readonly HashSet<string> _includedFiles = new(StringComparer.OrdinalIgnoreCase);

    public string Path { get; private set; } = "";
    public bool IsLoaded => _lines.Length > 0;
    public int LineCount => _lines.Length;

    public void Open(string path)
    {
        Close();
        Path = path;

        try
        {
            if (File.Exists(path))
            {
                _lines = File.ReadAllLines(path, Encoding.UTF8);
                _includedFiles.Add(path);
            }
        }
        catch
        {
            _lines = Array.Empty<string>();
        }
    }

    public void Close()
    {
        _lines = Array.Empty<string>();
        _includedFiles.Clear();
        Path = "";
    }

    public bool Include(string path)
    {
        if (_includedFiles.Contains(path))
            return false; // Already included

        try
        {
            if (!File.Exists(path))
                return false;

            var newLines = File.ReadAllLines(path, Encoding.UTF8);
            var combined = new string[_lines.Length + newLines.Length];
            _lines.CopyTo(combined, 0);
            newLines.CopyTo(combined, _lines.Length);
            _lines = combined;
            _includedFiles.Add(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetLine(int index)
    {
        if (index < 0 || index >= _lines.Length)
            return "";
        return _lines[index];
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public void Reload()
    {
        if (!string.IsNullOrEmpty(Path))
        {
            var path = Path;
            Close();
            Open(path);
        }
    }
}

/// <summary>
/// Handler for debug variables - for debugging and performance monitoring.
/// </summary>
public sealed class DebugHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Debug;
    public override string TypeName => "debug";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var debug = new DebugHandle();
        var handle = GCHandle.Alloc(debug);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var debug = GetDebug(memory);
        return debug?.IsEnabled ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var debug = GetDebug(memory);
        return debug?.StepCount ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var debug = GetDebug(memory);
        return debug?.Version ?? "IntMUD.NET 1.0";
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

    public static DebugHandle? GetDebug(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as DebugHandle;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var debug = GetDebug(memory);
        if (debug == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "cmd":
            case "command":
                // Execute instructions from text (returns result of execution)
                debug.ExecuteCommand(context.GetStringArgument(0));
                context.SetReturnInt(debug.LastCommandResult);
                return true;

            case "data":
            case "date":
                // Get current date/time as string
                context.SetReturnString(debug.GetCurrentDateTime());
                return true;

            case "exec":
            case "execute":
                // Execute single step
                debug.ExecuteStep();
                context.SetReturnInt(debug.StepCount);
                return true;

            case "func":
            case "function":
                // Get current function level/depth
                context.SetReturnInt(debug.FunctionLevel);
                return true;

            case "ini":
            case "init":
            case "initialize":
                // Initialize debug session
                debug.Initialize();
                context.SetReturnBool(debug.IsEnabled);
                return true;

            case "mem":
            case "memory":
                // Get current memory usage in bytes
                context.SetReturnInt(debug.GetMemoryUsage());
                return true;

            case "memmax":
            case "peakmemory":
                // Get peak memory usage
                context.SetReturnInt(debug.GetPeakMemoryUsage());
                return true;

            case "passo":
            case "step":
                // Enable/disable step mode
                debug.SetStepMode(context.GetIntArgument(0) != 0);
                return true;

            case "stempo":
            case "systemtime":
                // Get system time in milliseconds
                context.SetReturnInt(debug.GetSystemTime());
                return true;

            case "utempo":
            case "usertime":
                // Get user/process time in milliseconds
                context.SetReturnInt(debug.GetUserTime());
                return true;

            case "ver":
            case "version":
                // Get version string
                context.SetReturnString(debug.Version);
                return true;

            case "ligar":
            case "enable":
                // Enable debugging
                debug.IsEnabled = true;
                return true;

            case "desligar":
            case "disable":
                // Disable debugging
                debug.IsEnabled = false;
                return true;

            case "pausar":
            case "pause":
                // Pause execution
                debug.IsPaused = true;
                return true;

            case "continuar":
            case "continue":
                // Continue execution
                debug.IsPaused = false;
                return true;

            case "breakpoint":
                // Add breakpoint at line
                debug.AddBreakpoint(context.GetIntArgument(0));
                return true;

            case "remover":
            case "removebreakpoint":
                // Remove breakpoint
                debug.RemoveBreakpoint(context.GetIntArgument(0));
                return true;

            case "limparbreakpoints":
            case "clearbreakpoints":
                // Clear all breakpoints
                debug.ClearBreakpoints();
                return true;

            case "variaveis":
            case "variables":
                // Get variables dump
                context.SetReturnString(debug.GetVariablesDump());
                return true;

            case "pilha":
            case "stack":
                // Get call stack
                context.SetReturnString(debug.GetCallStack());
                return true;

            default:
                return false;
        }
    }
}

public class DebugHandle
{
    private readonly HashSet<int> _breakpoints = new();
    private readonly List<string> _callStack = new();
    private readonly Dictionary<string, string> _variables = new();
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private long _peakMemory;
    private readonly DateTime _startTime = DateTime.Now;

    public string Version => "IntMUD.NET 1.0";
    public bool IsEnabled { get; set; }
    public bool IsPaused { get; set; }
    public int StepCount { get; private set; }
    public int FunctionLevel { get; private set; }
    public int LastCommandResult { get; private set; }
    public bool StepMode { get; private set; }

    public void Initialize()
    {
        IsEnabled = true;
        IsPaused = false;
        StepCount = 0;
        FunctionLevel = 0;
        _breakpoints.Clear();
        _callStack.Clear();
        _variables.Clear();
        _stopwatch.Restart();
    }

    public void ExecuteCommand(string command)
    {
        // Simplified command execution for debug purposes
        LastCommandResult = 0;
        if (!string.IsNullOrEmpty(command))
        {
            // In a full implementation, this would parse and execute the command
            LastCommandResult = 1;
        }
    }

    public string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void ExecuteStep()
    {
        StepCount++;
    }

    public int GetMemoryUsage()
    {
        var currentMemory = GC.GetTotalMemory(false);
        if (currentMemory > _peakMemory)
            _peakMemory = currentMemory;
        return (int)(currentMemory / 1024); // Return in KB
    }

    public int GetPeakMemoryUsage()
    {
        return (int)(_peakMemory / 1024); // Return in KB
    }

    public void SetStepMode(bool enabled)
    {
        StepMode = enabled;
    }

    public int GetSystemTime()
    {
        return (int)(DateTime.Now - _startTime).TotalMilliseconds;
    }

    public int GetUserTime()
    {
        return (int)_stopwatch.ElapsedMilliseconds;
    }

    public void AddBreakpoint(int line)
    {
        _breakpoints.Add(line);
    }

    public void RemoveBreakpoint(int line)
    {
        _breakpoints.Remove(line);
    }

    public void ClearBreakpoints()
    {
        _breakpoints.Clear();
    }

    public bool HasBreakpoint(int line)
    {
        return _breakpoints.Contains(line);
    }

    public void PushFunction(string functionName)
    {
        _callStack.Add(functionName);
        FunctionLevel = _callStack.Count;
    }

    public void PopFunction()
    {
        if (_callStack.Count > 0)
        {
            _callStack.RemoveAt(_callStack.Count - 1);
            FunctionLevel = _callStack.Count;
        }
    }

    public void SetVariable(string name, string value)
    {
        _variables[name] = value;
    }

    public string GetVariablesDump()
    {
        if (_variables.Count == 0)
            return "(empty)";

        var sb = new StringBuilder();
        foreach (var kvp in _variables)
        {
            sb.AppendLine($"{kvp.Key} = {kvp.Value}");
        }
        return sb.ToString();
    }

    public string GetCallStack()
    {
        if (_callStack.Count == 0)
            return "(empty)";

        var sb = new StringBuilder();
        for (int i = _callStack.Count - 1; i >= 0; i--)
        {
            sb.AppendLine($"[{i}] {_callStack[i]}");
        }
        return sb.ToString();
    }
}
