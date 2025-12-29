using System.Runtime.InteropServices;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for ref (object reference) variables.
/// Stores a pointer to an IIntObject or null (NULO).
/// </summary>
public sealed class RefHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Ref;
    public override string TypeName => "ref";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        // Initialize to null
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        // True if not null
        return GetPointer(memory) != IntPtr.Zero;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        // Return 1 if not null, 0 if null
        return GetPointer(memory) != IntPtr.Zero ? 1 : 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory)
    {
        return GetInt(memory);
    }

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var ptr = GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return "nulo";
        return $"<obj:{ptr:X}>";
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        // Setting to 0 means null
        if (value == 0)
            SetPointer(memory, IntPtr.Zero);
        // Otherwise ignore - can't set object from int
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        SetInt(memory, (int)value);
    }

    public override void SetText(Span<byte> memory, string value)
    {
        // "nulo" or empty means null
        if (string.IsNullOrEmpty(value) ||
            value.Equals("nulo", StringComparison.OrdinalIgnoreCase))
        {
            SetPointer(memory, IntPtr.Zero);
        }
        // Otherwise ignore - can't set object from text
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        SetPointer(dest, GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var leftPtr = GetPointer(left);
        var rightPtr = GetPointer(right);
        return leftPtr.CompareTo(rightPtr);
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetPointer(left) == GetPointer(right);
    }

    /// <summary>
    /// Get the object pointer from memory.
    /// </summary>
    public static IntPtr GetPointer(ReadOnlySpan<byte> memory)
    {
        if (IntPtr.Size == 8)
            return (IntPtr)MemoryMarshal.Read<long>(memory);
        else
            return (IntPtr)MemoryMarshal.Read<int>(memory);
    }

    /// <summary>
    /// Set the object pointer in memory.
    /// </summary>
    public static void SetPointer(Span<byte> memory, IntPtr value)
    {
        if (IntPtr.Size == 8)
        {
            var val = (long)value;
            MemoryMarshal.Write(memory, in val);
        }
        else
        {
            var val = (int)value;
            MemoryMarshal.Write(memory, in val);
        }
    }

    /// <summary>
    /// Check if the reference is null.
    /// </summary>
    public static bool IsNull(ReadOnlySpan<byte> memory)
    {
        return GetPointer(memory) == IntPtr.Zero;
    }

    /// <summary>
    /// Set reference to null.
    /// </summary>
    public static void SetNull(Span<byte> memory)
    {
        SetPointer(memory, IntPtr.Zero);
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "nulo":
            case "isnull":
                // Check if reference is null
                context.SetReturnBool(IsNull(memory));
                return true;

            case "limpar":
            case "clear":
                // Set to null
                SetNull(memory);
                return true;

            default:
                return false;
        }
    }
}
