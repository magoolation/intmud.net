using System.Runtime.InteropServices;
using System.Text;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for textotxt (multi-line text buffer) variables.
/// </summary>
public sealed class TextoTxtHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.TextoTxt;
    public override string TypeName => "textotxt";
    public override VariableType RuntimeType => VariableType.Text;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var buffer = new TextBuffer();
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
        return buffer?.LineCount ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var buffer = GetBuffer(memory);
        return buffer?.ToString() ?? "";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }

    public override void SetText(Span<byte> memory, string value)
    {
        var buffer = GetBuffer(memory);
        if (buffer != null)
        {
            buffer.Clear();
            buffer.Append(value);
        }
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        var srcBuffer = GetBuffer(source);
        var destBuffer = GetBuffer(dest);
        if (srcBuffer != null && destBuffer != null)
        {
            destBuffer.Clear();
            destBuffer.Append(srcBuffer.ToString());
        }
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return string.Compare(GetText(left), GetText(right), StringComparison.Ordinal);
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetText(left) == GetText(right);
    }

    public static TextBuffer? GetBuffer(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as TextBuffer;
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

            case "adicionar":
            case "add":
            case "append":
                buffer.AppendLine(context.GetStringArgument(0));
                return true;

            case "linha":
            case "line":
                var lineNum = context.GetIntArgument(0);
                context.SetReturnString(buffer.GetLine(lineNum));
                return true;

            case "linhas":
            case "lines":
                context.SetReturnInt(buffer.LineCount);
                return true;

            case "tamanho":
            case "length":
                context.SetReturnInt(buffer.Length);
                return true;

            case "texto":
            case "text":
                context.SetReturnString(buffer.ToString());
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for textopos (text position/cursor) variables.
/// </summary>
public sealed class TextoPosHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.TextoPos;
    public override string TypeName => "textopos";
    public override VariableType RuntimeType => VariableType.Int;

    // Store: text pointer (IntPtr.Size) + position (4 bytes) + line (4 bytes) + column (4 bytes)
    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size + 12;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        return GetPosition(memory) >= 0;
    }

    public override int GetInt(ReadOnlySpan<byte> memory) => GetPosition(memory);

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetPosition(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var pos = GetPosition(memory);
        var line = GetLine(memory);
        var col = GetColumn(memory);
        return $"{line}:{col} ({pos})";
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        SetPosition(memory, value);
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        SetPosition(memory, (int)value);
    }

    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source.CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetPosition(left).CompareTo(GetPosition(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetPosition(left) == GetPosition(right);
    }

    private static int GetPosition(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory[IntPtr.Size..]);
    }

    private static void SetPosition(Span<byte> memory, int value)
    {
        MemoryMarshal.Write(memory[IntPtr.Size..], in value);
    }

    private static int GetLine(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory[(IntPtr.Size + 4)..]);
    }

    private static int GetColumn(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory[(IntPtr.Size + 8)..]);
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "posicao":
            case "position":
                context.SetReturnInt(GetPosition(memory));
                return true;

            case "linha":
            case "line":
                context.SetReturnInt(GetLine(memory));
                return true;

            case "coluna":
            case "column":
                context.SetReturnInt(GetColumn(memory));
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for nomeobj (object name) variables.
/// Stores object name as text with special formatting.
/// </summary>
public sealed class NomeObjHandler : VariableTypeHandlerBase
{
    private readonly int _maxLength;

    public NomeObjHandler(int maxLength = 64)
    {
        _maxLength = maxLength;
    }

    public override OpCode OpCode => OpCode.NomeObj;
    public override string TypeName => "nomeobj";
    public override VariableType RuntimeType => VariableType.Text;

    public override int GetSize(ReadOnlySpan<byte> instruction) => _maxLength + 4;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        return GetLength(memory) > 0;
    }

    public override int GetInt(ReadOnlySpan<byte> memory) => GetLength(memory);

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetLength(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var len = GetLength(memory);
        if (len == 0)
            return "";
        return Encoding.UTF8.GetString(memory.Slice(4, len));
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }

    public override void SetText(Span<byte> memory, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var len = Math.Min(bytes.Length, _maxLength);
        MemoryMarshal.Write(memory, in len);
        bytes.AsSpan(0, len).CopyTo(memory[4..]);
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        var len = GetLength(source);
        source[..(len + 4)].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return string.Compare(GetText(left), GetText(right), StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetText(left).Equals(GetText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static int GetLength(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory);
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "tamanho":
            case "length":
                context.SetReturnInt(GetLength(memory));
                return true;

            case "limpar":
            case "clear":
                memory.Clear();
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Text buffer helper class.
/// </summary>
public class TextBuffer
{
    private readonly StringBuilder _content = new();
    private readonly List<int> _lineStarts = new() { 0 };

    public int Length => _content.Length;
    public int LineCount => _lineStarts.Count;

    public void Clear()
    {
        _content.Clear();
        _lineStarts.Clear();
        _lineStarts.Add(0);
    }

    public void Append(string text)
    {
        var startPos = _content.Length;
        _content.Append(text);

        // Track line starts
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _lineStarts.Add(startPos + i + 1);
            }
        }
    }

    public void AppendLine(string text)
    {
        Append(text + "\n");
    }

    public string GetLine(int lineNumber)
    {
        if (lineNumber < 0 || lineNumber >= _lineStarts.Count)
            return "";

        var start = _lineStarts[lineNumber];
        var end = lineNumber + 1 < _lineStarts.Count
            ? _lineStarts[lineNumber + 1] - 1
            : _content.Length;

        if (end < start)
            return "";

        return _content.ToString(start, end - start);
    }

    public override string ToString() => _content.ToString();
}
