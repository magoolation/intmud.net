using IntMud.Core.Variables;

namespace IntMud.Runtime.Stacks;

/// <summary>
/// Variable stack for RuntimeVariable entries during execution.
/// Equivalent to VarPilha (500 entries) from the original C++ implementation.
/// </summary>
public sealed class VariableStack
{
    private const int DefaultMaxEntries = 500;

    private readonly RuntimeVariable[] _variables;
    private int _current;
    private readonly int _maxEntries;

    /// <summary>
    /// Create a new variable stack with default capacity.
    /// </summary>
    public VariableStack() : this(DefaultMaxEntries)
    {
    }

    /// <summary>
    /// Create a new variable stack with specified capacity.
    /// </summary>
    public VariableStack(int maxEntries)
    {
        _maxEntries = maxEntries;
        _variables = new RuntimeVariable[maxEntries];
        _current = 0;
    }

    /// <summary>
    /// Current number of variables on the stack.
    /// </summary>
    public int Count => _current;

    /// <summary>
    /// Maximum capacity of the stack.
    /// </summary>
    public int Capacity => _maxEntries;

    /// <summary>
    /// Available space remaining.
    /// </summary>
    public int Available => _maxEntries - _current;

    /// <summary>
    /// Whether the stack is empty.
    /// </summary>
    public bool IsEmpty => _current == 0;

    /// <summary>
    /// Whether the stack is full.
    /// </summary>
    public bool IsFull => _current >= _maxEntries;

    /// <summary>
    /// Reference to the current (top) variable.
    /// </summary>
    public ref RuntimeVariable Current
    {
        get
        {
            if (_current == 0)
                throw new InvalidOperationException("Variable stack is empty");
            return ref _variables[_current - 1];
        }
    }

    /// <summary>
    /// Get variable at specific index.
    /// </summary>
    public ref RuntimeVariable this[int index]
    {
        get
        {
            if (index < 0 || index >= _current)
                throw new IndexOutOfRangeException();
            return ref _variables[index];
        }
    }

    /// <summary>
    /// Push a new variable onto the stack.
    /// </summary>
    /// <returns>Reference to the new variable</returns>
    public ref RuntimeVariable Push()
    {
        if (_current >= _maxEntries)
            throw new StackOverflowException("Variable stack overflow");

        _variables[_current] = default;
        return ref _variables[_current++];
    }

    /// <summary>
    /// Push a specific variable onto the stack.
    /// </summary>
    public void Push(in RuntimeVariable variable)
    {
        if (_current >= _maxEntries)
            throw new StackOverflowException("Variable stack overflow");

        _variables[_current++] = variable;
    }

    /// <summary>
    /// Push an integer value.
    /// </summary>
    public void PushInt(int value)
    {
        ref var v = ref Push();
        v.Type = VariableType.Int;
        v.IntValue = value;
    }

    /// <summary>
    /// Push a double value.
    /// </summary>
    public void PushDouble(double value)
    {
        ref var v = ref Push();
        v.Type = VariableType.Double;
        v.DoubleValue = value;
    }

    /// <summary>
    /// Push a boolean value (as int).
    /// </summary>
    public void PushBool(bool value)
    {
        PushInt(value ? 1 : 0);
    }

    /// <summary>
    /// Pop the top variable from the stack.
    /// </summary>
    public RuntimeVariable Pop()
    {
        if (_current == 0)
            throw new InvalidOperationException("Variable stack underflow");

        return _variables[--_current];
    }

    /// <summary>
    /// Pop and discard the top variable.
    /// </summary>
    public void Discard()
    {
        if (_current > 0)
            _current--;
    }

    /// <summary>
    /// Pop multiple variables.
    /// </summary>
    public void Discard(int count)
    {
        _current = Math.Max(0, _current - count);
    }

    /// <summary>
    /// Peek at the top variable without removing it.
    /// </summary>
    public ref readonly RuntimeVariable Peek()
    {
        if (_current == 0)
            throw new InvalidOperationException("Variable stack is empty");
        return ref _variables[_current - 1];
    }

    /// <summary>
    /// Peek at a variable at offset from top.
    /// </summary>
    /// <param name="offset">0 = top, 1 = one below top, etc.</param>
    public ref readonly RuntimeVariable PeekAt(int offset)
    {
        var index = _current - 1 - offset;
        if (index < 0 || index >= _current)
            throw new IndexOutOfRangeException();
        return ref _variables[index];
    }

    /// <summary>
    /// Get a span of variables from a starting index.
    /// </summary>
    public Span<RuntimeVariable> GetRange(int start, int count)
    {
        if (start < 0 || start + count > _current)
            throw new ArgumentOutOfRangeException();
        return _variables.AsSpan(start, count);
    }

    /// <summary>
    /// Save current position for later restoration.
    /// </summary>
    public int SavePosition() => _current;

    /// <summary>
    /// Restore to a previously saved position.
    /// </summary>
    public void RestorePosition(int position)
    {
        if (position < 0 || position > _maxEntries)
            throw new ArgumentOutOfRangeException(nameof(position));
        _current = position;
    }

    /// <summary>
    /// Clear the stack.
    /// </summary>
    public void Clear()
    {
        // Clear references to allow GC
        Array.Clear(_variables, 0, _current);
        _current = 0;
    }

    /// <summary>
    /// Duplicate the top variable.
    /// </summary>
    public void Duplicate()
    {
        if (_current == 0)
            throw new InvalidOperationException("Variable stack is empty");
        if (_current >= _maxEntries)
            throw new StackOverflowException("Variable stack overflow");

        _variables[_current] = _variables[_current - 1];
        _current++;
    }

    /// <summary>
    /// Swap the top two variables.
    /// </summary>
    public void Swap()
    {
        if (_current < 2)
            throw new InvalidOperationException("Not enough variables to swap");

        (_variables[_current - 1], _variables[_current - 2]) =
            (_variables[_current - 2], _variables[_current - 1]);
    }
}
