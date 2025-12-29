using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Base class for variable type handlers.
/// Provides common functionality and default implementations.
/// </summary>
public abstract class VariableTypeHandlerBase : IVariableTypeHandler
{
    /// <inheritdoc />
    public abstract OpCode OpCode { get; }

    /// <inheritdoc />
    public abstract VariableType RuntimeType { get; }

    /// <inheritdoc />
    public abstract string TypeName { get; }

    /// <inheritdoc />
    public abstract int GetSize(ReadOnlySpan<byte> instruction);

    /// <inheritdoc />
    public virtual int GetArrayElementSize(ReadOnlySpan<byte> instruction) => GetSize(instruction);

    /// <inheritdoc />
    public virtual void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    /// <inheritdoc />
    public virtual void Destroy(Span<byte> memory)
    {
        // Default: no cleanup needed for basic types
    }

    /// <inheritdoc />
    public abstract bool GetBool(ReadOnlySpan<byte> memory);

    /// <inheritdoc />
    public abstract int GetInt(ReadOnlySpan<byte> memory);

    /// <inheritdoc />
    public abstract double GetDouble(ReadOnlySpan<byte> memory);

    /// <inheritdoc />
    public abstract string GetText(ReadOnlySpan<byte> memory);

    /// <inheritdoc />
    public abstract void SetInt(Span<byte> memory, int value);

    /// <inheritdoc />
    public abstract void SetDouble(Span<byte> memory, double value);

    /// <inheritdoc />
    public abstract void SetText(Span<byte> memory, string value);

    /// <inheritdoc />
    public virtual void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source.CopyTo(dest);
    }

    /// <inheritdoc />
    public virtual bool Add(Span<byte> memory, ReadOnlySpan<byte> value)
    {
        // Default: not supported
        return false;
    }

    /// <inheritdoc />
    public virtual int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var leftVal = GetInt(left);
        var rightVal = GetInt(right);
        return leftVal.CompareTo(rightVal);
    }

    /// <inheritdoc />
    public virtual bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return Compare(left, right) == 0;
    }

    /// <inheritdoc />
    public virtual bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        // Default: no member functions
        return false;
    }
}

/// <summary>
/// Base class for integer type handlers.
/// </summary>
public abstract class IntegerTypeHandlerBase : VariableTypeHandlerBase
{
    /// <inheritdoc />
    public override VariableType RuntimeType => VariableType.Int;

    /// <inheritdoc />
    public override bool GetBool(ReadOnlySpan<byte> memory) => GetInt(memory) != 0;

    /// <inheritdoc />
    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    /// <inheritdoc />
    public override string GetText(ReadOnlySpan<byte> memory) => GetInt(memory).ToString();

    /// <inheritdoc />
    public override void SetDouble(Span<byte> memory, double value) => SetInt(memory, (int)value);

    /// <inheritdoc />
    public override void SetText(Span<byte> memory, string value)
    {
        if (int.TryParse(value, out var intVal))
            SetInt(memory, intVal);
        else if (double.TryParse(value, out var doubleVal))
            SetInt(memory, (int)doubleVal);
        else
            SetInt(memory, 0);
    }

    /// <inheritdoc />
    public override bool Add(Span<byte> memory, ReadOnlySpan<byte> value)
    {
        SetInt(memory, GetInt(memory) + GetInt(value));
        return true;
    }
}

/// <summary>
/// Base class for floating point type handlers.
/// </summary>
public abstract class FloatTypeHandlerBase : VariableTypeHandlerBase
{
    /// <inheritdoc />
    public override VariableType RuntimeType => VariableType.Double;

    /// <inheritdoc />
    public override bool GetBool(ReadOnlySpan<byte> memory) => GetDouble(memory) != 0.0;

    /// <inheritdoc />
    public override int GetInt(ReadOnlySpan<byte> memory) => (int)GetDouble(memory);

    /// <inheritdoc />
    public override string GetText(ReadOnlySpan<byte> memory) => GetDouble(memory).ToString("G");

    /// <inheritdoc />
    public override void SetInt(Span<byte> memory, int value) => SetDouble(memory, value);

    /// <inheritdoc />
    public override void SetText(Span<byte> memory, string value)
    {
        if (double.TryParse(value, out var doubleVal))
            SetDouble(memory, doubleVal);
        else
            SetDouble(memory, 0.0);
    }

    /// <inheritdoc />
    public override bool Add(Span<byte> memory, ReadOnlySpan<byte> value)
    {
        SetDouble(memory, GetDouble(memory) + GetDouble(value));
        return true;
    }

    /// <inheritdoc />
    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var leftVal = GetDouble(left);
        var rightVal = GetDouble(right);
        return leftVal.CompareTo(rightVal);
    }
}
