using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an intexec (execution trigger) instance.
/// When value changes to non-zero, fires {variableName}_exec event.
/// Value is reset to 0 after event fires.
/// </summary>
public sealed class IntExecInstance
{
    private int _valor;
    private bool _shouldFire;

    /// <summary>
    /// The owner object that contains this intexec variable.
    /// </summary>
    public BytecodeRuntimeObject? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// The value. When set to non-zero, marks for event firing.
    /// </summary>
    public int Valor
    {
        get => _valor;
        set
        {
            if (value != 0 && _valor == 0)
            {
                _shouldFire = true;
            }
            _valor = value;
        }
    }

    /// <summary>
    /// Check if event should fire.
    /// </summary>
    public bool ShouldFire => _shouldFire && _valor != 0;

    /// <summary>
    /// Process pending trigger.
    /// </summary>
    /// <returns>True if event should fire.</returns>
    public bool ProcessTrigger()
    {
        if (_shouldFire && _valor != 0)
        {
            _shouldFire = false;
            _valor = 0; // Reset after firing
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reset the trigger without firing.
    /// </summary>
    public void Reset()
    {
        _shouldFire = false;
        _valor = 0;
    }

    public override string ToString() => $"[IntExec: {_valor}]";
}
