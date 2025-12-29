using IntMud.Core.Types;

namespace IntMud.Runtime.Stacks;

/// <summary>
/// Function call stack for execution context.
/// Equivalent to FuncPilha (40 entries) from the original C++ implementation.
/// </summary>
public sealed class FunctionStack
{
    private const int DefaultMaxDepth = 40;

    private readonly FunctionFrame[] _frames;
    private int _current;
    private readonly int _maxDepth;

    /// <summary>
    /// Create a new function stack with default depth.
    /// </summary>
    public FunctionStack() : this(DefaultMaxDepth)
    {
    }

    /// <summary>
    /// Create a new function stack with specified depth.
    /// </summary>
    public FunctionStack(int maxDepth)
    {
        _maxDepth = maxDepth;
        _frames = new FunctionFrame[maxDepth];
        _current = 0;
    }

    /// <summary>
    /// Current call depth.
    /// </summary>
    public int Depth => _current;

    /// <summary>
    /// Maximum call depth.
    /// </summary>
    public int MaxDepth => _maxDepth;

    /// <summary>
    /// Whether the stack is empty.
    /// </summary>
    public bool IsEmpty => _current == 0;

    /// <summary>
    /// Reference to the current frame.
    /// </summary>
    public ref FunctionFrame Current
    {
        get
        {
            if (_current == 0)
                throw new InvalidOperationException("Function stack is empty");
            return ref _frames[_current - 1];
        }
    }

    /// <summary>
    /// Get frame at specific depth.
    /// </summary>
    public ref FunctionFrame this[int index]
    {
        get
        {
            if (index < 0 || index >= _current)
                throw new IndexOutOfRangeException();
            return ref _frames[index];
        }
    }

    /// <summary>
    /// Push a new function frame onto the stack.
    /// </summary>
    /// <returns>Reference to the new frame</returns>
    public ref FunctionFrame Push()
    {
        if (_current >= _maxDepth)
            throw new StackOverflowException($"Function call stack overflow (max depth: {_maxDepth})");

        _frames[_current] = default;
        return ref _frames[_current++];
    }

    /// <summary>
    /// Push a function call.
    /// </summary>
    public ref FunctionFrame Push(
        IIntObject? obj,
        IIntClass? intClass,
        string functionName,
        int bytecodeOffset,
        int variableStackBase,
        int dataStackBase,
        int argumentCount)
    {
        ref var frame = ref Push();
        frame.Object = obj;
        frame.Class = intClass;
        frame.FunctionName = functionName;
        frame.BytecodeOffset = bytecodeOffset;
        frame.CurrentOffset = bytecodeOffset;
        frame.VariableStackBase = variableStackBase;
        frame.DataStackBase = dataStackBase;
        frame.ArgumentCount = argumentCount;
        frame.ReturnValue = default;
        frame.Flags = FunctionFrameFlags.None;
        return ref frame;
    }

    /// <summary>
    /// Pop the current frame from the stack.
    /// </summary>
    public FunctionFrame Pop()
    {
        if (_current == 0)
            throw new InvalidOperationException("Function stack underflow");

        var frame = _frames[--_current];
        _frames[_current] = default; // Clear reference
        return frame;
    }

    /// <summary>
    /// Peek at the current frame without removing it.
    /// </summary>
    public ref readonly FunctionFrame Peek()
    {
        if (_current == 0)
            throw new InvalidOperationException("Function stack is empty");
        return ref _frames[_current - 1];
    }

    /// <summary>
    /// Peek at the caller's frame (one below current).
    /// </summary>
    public ref readonly FunctionFrame PeekCaller()
    {
        if (_current < 2)
            throw new InvalidOperationException("No caller frame available");
        return ref _frames[_current - 2];
    }

    /// <summary>
    /// Clear the stack.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_frames, 0, _current);
        _current = 0;
    }

    /// <summary>
    /// Get call stack trace for debugging.
    /// </summary>
    public IEnumerable<string> GetStackTrace()
    {
        for (int i = _current - 1; i >= 0; i--)
        {
            var frame = _frames[i];
            var className = frame.Class?.Name ?? "?";
            var objInfo = frame.Object != null ? $" [{frame.Object.Id}]" : "";
            yield return $"  at {className}.{frame.FunctionName}{objInfo} (offset: {frame.CurrentOffset})";
        }
    }
}

/// <summary>
/// Function call frame.
/// Equivalent to Instr::ExecFunc from the original implementation.
/// </summary>
public struct FunctionFrame
{
    /// <summary>
    /// Object instance being executed on (null for class-level calls).
    /// </summary>
    public IIntObject? Object;

    /// <summary>
    /// Class being executed.
    /// </summary>
    public IIntClass? Class;

    /// <summary>
    /// Function name being executed.
    /// </summary>
    public string? FunctionName;

    /// <summary>
    /// Starting bytecode offset for this function.
    /// </summary>
    public int BytecodeOffset;

    /// <summary>
    /// Current bytecode execution offset.
    /// </summary>
    public int CurrentOffset;

    /// <summary>
    /// Current expression evaluation position.
    /// </summary>
    public int ExpressionOffset;

    /// <summary>
    /// Base position in variable stack for this call.
    /// </summary>
    public int VariableStackBase;

    /// <summary>
    /// First variable index for this call.
    /// </summary>
    public int FirstVariable;

    /// <summary>
    /// Last variable index for this call.
    /// </summary>
    public int LastVariable;

    /// <summary>
    /// Base position in data stack for this call.
    /// </summary>
    public int DataStackBase;

    /// <summary>
    /// Number of arguments passed to this function.
    /// </summary>
    public int ArgumentCount;

    /// <summary>
    /// Loop nesting depth for break/continue.
    /// </summary>
    public int LoopDepth;

    /// <summary>
    /// Case statement nesting depth.
    /// </summary>
    public int CaseDepth;

    /// <summary>
    /// Return value from this function.
    /// </summary>
    public RuntimeVariableValue ReturnValue;

    /// <summary>
    /// Frame flags.
    /// </summary>
    public FunctionFrameFlags Flags;
}

/// <summary>
/// Function frame flags.
/// </summary>
[Flags]
public enum FunctionFrameFlags : byte
{
    /// <summary>No special flags</summary>
    None = 0,

    /// <summary>Function has returned</summary>
    Returned = 1,

    /// <summary>Break requested</summary>
    Break = 2,

    /// <summary>Continue requested</summary>
    Continue = 4,

    /// <summary>Terminate requested</summary>
    Terminate = 8,

    /// <summary>Error occurred</summary>
    Error = 16
}

/// <summary>
/// Runtime variable value union for return values.
/// </summary>
public struct RuntimeVariableValue
{
    public int IntValue;
    public double DoubleValue;
    public nint ObjectPtr;
    public byte Type; // VariableType

    public readonly bool IsNull => Type == 0 && IntValue == 0 && ObjectPtr == 0;
}
