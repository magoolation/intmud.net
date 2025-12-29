using System.Runtime.InteropServices;
using IntMud.Core.Instructions;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for int1 (1-bit boolean) variables.
/// Uses bit manipulation within a byte.
/// </summary>
public sealed class Int1Handler : IntegerTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Int1;
    public override string TypeName => "int1";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 1;

    public override int GetInt(ReadOnlySpan<byte> memory) => memory[0] != 0 ? 1 : 0;

    public override void SetInt(Span<byte> memory, int value) => memory[0] = (byte)(value != 0 ? 1 : 0);

    /// <summary>
    /// Get bit value at specific bit number.
    /// </summary>
    public static bool GetBit(ReadOnlySpan<byte> memory, int bitNumber)
    {
        return (memory[0] & (1 << bitNumber)) != 0;
    }

    /// <summary>
    /// Set bit value at specific bit number.
    /// </summary>
    public static void SetBit(Span<byte> memory, int bitNumber, bool value)
    {
        if (value)
            memory[0] |= (byte)(1 << bitNumber);
        else
            memory[0] &= (byte)~(1 << bitNumber);
    }
}

/// <summary>
/// Handler for int8 (8-bit signed) variables.
/// </summary>
public sealed class Int8Handler : IntegerTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Int8;
    public override string TypeName => "int8";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 1;

    public override int GetInt(ReadOnlySpan<byte> memory) => (sbyte)memory[0];

    public override void SetInt(Span<byte> memory, int value) => memory[0] = (byte)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
}

/// <summary>
/// Handler for uint8 (8-bit unsigned) variables.
/// </summary>
public sealed class UInt8Handler : IntegerTypeHandlerBase
{
    public override OpCode OpCode => OpCode.UInt8;
    public override string TypeName => "uint8";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 1;

    public override int GetInt(ReadOnlySpan<byte> memory) => memory[0];

    public override void SetInt(Span<byte> memory, int value) => memory[0] = (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
}

/// <summary>
/// Handler for int16 (16-bit signed) variables.
/// </summary>
public sealed class Int16Handler : IntegerTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Int16;
    public override string TypeName => "int16";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 2;

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<short>(memory);
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        var clamped = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        MemoryMarshal.Write(memory, in clamped);
    }
}

/// <summary>
/// Handler for uint16 (16-bit unsigned) variables.
/// </summary>
public sealed class UInt16Handler : IntegerTypeHandlerBase
{
    public override OpCode OpCode => OpCode.UInt16;
    public override string TypeName => "uint16";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 2;

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<ushort>(memory);
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        var clamped = (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
        MemoryMarshal.Write(memory, in clamped);
    }
}

/// <summary>
/// Handler for int32 (32-bit signed) variables.
/// </summary>
public sealed class Int32Handler : IntegerTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Int32;
    public override string TypeName => "int32";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 4;

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory);
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        MemoryMarshal.Write(memory, in value);
    }
}

/// <summary>
/// Handler for uint32 (32-bit unsigned) variables.
/// </summary>
public sealed class UInt32Handler : IntegerTypeHandlerBase
{
    public override OpCode OpCode => OpCode.UInt32;
    public override string TypeName => "uint32";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 4;

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        return (int)MemoryMarshal.Read<uint>(memory);
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        var uval = (uint)value;
        MemoryMarshal.Write(memory, in uval);
    }

    /// <summary>
    /// Get value as unsigned.
    /// </summary>
    public uint GetUInt(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<uint>(memory);
    }

    /// <summary>
    /// Set value as unsigned.
    /// </summary>
    public void SetUInt(Span<byte> memory, uint value)
    {
        MemoryMarshal.Write(memory, in value);
    }
}
