using System.Runtime.InteropServices;
using System.Text;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for indiceobj (object index) variables.
/// Used for iterating through objects of a class.
/// </summary>
public sealed class IndiceObjHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.IndiceObj;
    public override string TypeName => "indiceobj";
    public override VariableType RuntimeType => VariableType.Int;

    // Store: class name pointer (IntPtr.Size) + current index (4 bytes)
    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size + 4;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        return GetIndex(memory) >= 0;
    }

    public override int GetInt(ReadOnlySpan<byte> memory) => GetIndex(memory);

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetIndex(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        return $"<indiceobj:{GetIndex(memory)}>";
    }

    public override void SetInt(Span<byte> memory, int value) => SetIndex(memory, value);

    public override void SetDouble(Span<byte> memory, double value) => SetIndex(memory, (int)value);

    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source[..(IntPtr.Size + 4)].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetIndex(left).CompareTo(GetIndex(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetIndex(left) == GetIndex(right);
    }

    private static int GetIndex(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory[IntPtr.Size..]);
    }

    private static void SetIndex(Span<byte> memory, int value)
    {
        MemoryMarshal.Write(memory[IntPtr.Size..], in value);
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "iniciar":
            case "start":
                SetIndex(memory, 0);
                return true;

            case "proximo":
            case "next":
                SetIndex(memory, GetIndex(memory) + 1);
                return true;

            case "anterior":
            case "prev":
                var idx = GetIndex(memory);
                if (idx > 0)
                    SetIndex(memory, idx - 1);
                return true;

            case "ir":
            case "goto":
                SetIndex(memory, context.GetIntArgument(0));
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for indiceitem (item index) variables.
/// </summary>
public sealed class IndiceItemHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.IndiceItem;
    public override string TypeName => "indiceitem";
    public override VariableType RuntimeType => VariableType.Int;

    public override int GetSize(ReadOnlySpan<byte> instruction) => 4;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory) => GetValue(memory) >= 0;
    public override int GetInt(ReadOnlySpan<byte> memory) => GetValue(memory);
    public override double GetDouble(ReadOnlySpan<byte> memory) => GetValue(memory);
    public override string GetText(ReadOnlySpan<byte> memory) => GetValue(memory).ToString();

    public override void SetInt(Span<byte> memory, int value) => SetValue(memory, value);
    public override void SetDouble(Span<byte> memory, double value) => SetValue(memory, (int)value);
    public override void SetText(Span<byte> memory, string value)
    {
        if (int.TryParse(value, out var v))
            SetValue(memory, v);
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source[..4].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetValue(left).CompareTo(GetValue(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetValue(left) == GetValue(right);
    }

    private static int GetValue(ReadOnlySpan<byte> memory) => MemoryMarshal.Read<int>(memory);
    private static void SetValue(Span<byte> memory, int value) => MemoryMarshal.Write(memory, in value);

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        return false;
    }
}

/// <summary>
/// Handler for prog (program info) variables.
/// </summary>
public sealed class ProgHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Prog;
    public override string TypeName => "prog";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var info = new ProgramInfo();
        var handle = GCHandle.Alloc(info);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory) => true;

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var info = GetProgramInfo(memory);
        return info?.ExitCode ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var info = GetProgramInfo(memory);
        return info?.Name ?? "";
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        var info = GetProgramInfo(memory);
        if (info != null)
            info.ExitCode = value;
    }

    public override void SetDouble(Span<byte> memory, double value) => SetInt(memory, (int)value);
    public override void SetText(Span<byte> memory, string value) { }

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

    private static ProgramInfo? GetProgramInfo(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as ProgramInfo;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var info = GetProgramInfo(memory);
        if (info == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "nome":
            case "name":
                context.SetReturnString(info.Name);
                return true;

            case "versao":
            case "version":
                context.SetReturnString(info.Version);
                return true;

            case "terminar":
            case "exit":
                info.ShouldExit = true;
                info.ExitCode = context.GetIntArgument(0);
                return true;

            case "erro":
            case "error":
                context.SetReturnString(info.LastError);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for telatxt (text screen) variables.
/// </summary>
public sealed class TelaTxtHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.TelaTxt;
    public override string TypeName => "telatxt";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        var screen = new TextScreen();
        var handle = GCHandle.Alloc(screen);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var screen = GetScreen(memory);
        return screen?.IsVisible ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var screen = GetScreen(memory);
        return screen?.LineCount ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var screen = GetScreen(memory);
        return screen?.GetContent() ?? "";
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
        return GetInt(left).CompareTo(GetInt(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    private static TextScreen? GetScreen(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as TextScreen;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var screen = GetScreen(memory);
        if (screen == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "mostrar":
            case "show":
                screen.IsVisible = true;
                return true;

            case "esconder":
            case "hide":
                screen.IsVisible = false;
                return true;

            case "limpar":
            case "clear":
                screen.Clear();
                return true;

            case "escrever":
            case "write":
                screen.Write(context.GetStringArgument(0));
                return true;

            case "linha":
            case "writeline":
                screen.WriteLine(context.GetStringArgument(0));
                return true;

            case "ir":
            case "goto":
                screen.SetPosition(context.GetIntArgument(0), context.GetIntArgument(1));
                return true;

            case "cor":
            case "color":
                screen.SetColor(context.GetIntArgument(0));
                return true;

            default:
                return false;
        }
    }
}

// Helper classes

public class ProgramInfo
{
    public string Name { get; set; } = "IntMUD";
    public string Version { get; set; } = "1.0";
    public int ExitCode { get; set; }
    public bool ShouldExit { get; set; }
    public string LastError { get; set; } = "";
}

public class TextScreen
{
    private readonly StringBuilder _content = new();
    private readonly List<string> _lines = new();
    private int _cursorX;
    private int _cursorY;
    private int _color;

    public bool IsVisible { get; set; }
    public int LineCount => _lines.Count;

    public void Clear()
    {
        _content.Clear();
        _lines.Clear();
        _cursorX = 0;
        _cursorY = 0;
    }

    public void Write(string text)
    {
        _content.Append(text);
        // Update lines
        while (_lines.Count <= _cursorY)
            _lines.Add("");

        _lines[_cursorY] += text;
        _cursorX += text.Length;
    }

    public void WriteLine(string text)
    {
        Write(text + "\n");
        _cursorX = 0;
        _cursorY++;
    }

    public void SetPosition(int x, int y)
    {
        _cursorX = Math.Max(0, x);
        _cursorY = Math.Max(0, y);
    }

    public void SetColor(int color)
    {
        _color = color;
    }

    public string GetContent() => _content.ToString();

    public string GetLine(int index)
    {
        if (index < 0 || index >= _lines.Count)
            return "";
        return _lines[index];
    }
}
