using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an intinc (incrementing counter) instance.
/// Automatically increments by 1 each decisecond.
/// Used for measuring elapsed time.
/// </summary>
public sealed class IntIncInstance
{
    private int _valor;

    /// <summary>
    /// The owner object that contains this intinc variable.
    /// </summary>
    public BytecodeRuntimeObject? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// The current value (in deciseconds).
    /// </summary>
    public int Valor
    {
        get => _valor;
        set => _valor = value;
    }

    /// <summary>
    /// Process a time tick, incrementing the counter.
    /// </summary>
    /// <param name="elapsedDeciseconds">Elapsed time in deciseconds.</param>
    public void ProcessTick(int elapsedDeciseconds)
    {
        _valor += elapsedDeciseconds;
    }

    /// <summary>
    /// Reset to zero.
    /// </summary>
    public void Reset()
    {
        _valor = 0;
    }

    /// <summary>
    /// Get value in seconds.
    /// </summary>
    public double Seconds => _valor / 10.0;

    public override string ToString() => $"[IntInc: {_valor}]";
}
