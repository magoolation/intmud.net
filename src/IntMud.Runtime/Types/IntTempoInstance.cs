using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an inttempo (timer) instance.
/// When value reaches 0, fires {variableName}_exec event.
/// Value is in deciseconds (1/10 of a second).
/// Negative value means timer is stopped.
/// </summary>
public sealed class IntTempoInstance
{
    private int _valor;
    private bool _fired;

    /// <summary>
    /// The owner object that contains this inttempo variable.
    /// </summary>
    public BytecodeRuntimeObject? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Timer value in deciseconds.
    /// Positive = counting down.
    /// Zero = will fire.
    /// Negative = stopped.
    /// </summary>
    public int Valor
    {
        get => _valor;
        set
        {
            _valor = value;
            _fired = false; // Reset fired state when value changes
        }
    }

    /// <summary>
    /// Check if timer is active (counting down).
    /// </summary>
    public bool IsActive => _valor > 0;

    /// <summary>
    /// Check if timer is stopped.
    /// </summary>
    public bool IsStopped => _valor < 0;

    /// <summary>
    /// Process a time tick.
    /// </summary>
    /// <param name="elapsedDeciseconds">Elapsed time in deciseconds.</param>
    /// <returns>True if timer fired (reached 0).</returns>
    public bool ProcessTick(int elapsedDeciseconds)
    {
        if (_valor <= 0 || _fired)
            return false;

        _valor -= elapsedDeciseconds;

        if (_valor <= 0)
        {
            _valor = 0;
            _fired = true;
            return true; // Timer fired
        }

        return false;
    }

    /// <summary>
    /// Reset fired state (call after handling the event).
    /// </summary>
    public void ResetFired()
    {
        _fired = false;
    }

    /// <summary>
    /// Get time remaining (for comparison operations).
    /// </summary>
    public int GetTimeRemaining() => _valor > 0 ? _valor : 0;

    /// <summary>
    /// Get absolute value.
    /// </summary>
    public int Abs => Math.Abs(_valor);

    /// <summary>
    /// Get positive value (0 if negative).
    /// </summary>
    public int Pos => _valor > 0 ? _valor : 0;

    /// <summary>
    /// Get negative value (0 if positive).
    /// </summary>
    public int Neg => _valor < 0 ? _valor : 0;

    public override string ToString() => $"[IntTempo: {_valor}]";
}
