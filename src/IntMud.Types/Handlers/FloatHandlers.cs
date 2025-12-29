using System.Runtime.InteropServices;
using IntMud.Core.Instructions;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for real (single precision float) variables.
/// </summary>
public sealed class RealHandler : FloatTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Real;
    public override string TypeName => "real";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 4;

    public override double GetDouble(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<float>(memory);
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        var fval = (float)value;
        MemoryMarshal.Write(memory, in fval);
    }

    /// <summary>
    /// Get value as float.
    /// </summary>
    public float GetFloat(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<float>(memory);
    }

    /// <summary>
    /// Set value as float.
    /// </summary>
    public void SetFloat(Span<byte> memory, float value)
    {
        MemoryMarshal.Write(memory, in value);
    }
}

/// <summary>
/// Handler for real2 (double precision float) variables.
/// </summary>
public sealed class Real2Handler : FloatTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Real2;
    public override string TypeName => "real2";

    public override int GetSize(ReadOnlySpan<byte> instruction) => 8;

    public override double GetDouble(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<double>(memory);
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        MemoryMarshal.Write(memory, in value);
    }
}
