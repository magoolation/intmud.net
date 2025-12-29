using IntMud.Runtime.Stacks;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Execution;

/// <summary>
/// Control flow action requested by a statement.
/// </summary>
public enum ControlFlow
{
    None,
    Return,
    Exit,       // sair - exit loop
    Continue,   // continuar - continue to next iteration
    Terminate   // terminar - terminate execution
}

/// <summary>
/// Result of executing a statement or expression.
/// </summary>
public readonly struct ExecutionResult
{
    public RuntimeValue Value { get; init; }
    public ControlFlow ControlFlow { get; init; }

    public static readonly ExecutionResult None = new() { Value = RuntimeValue.Null, ControlFlow = ControlFlow.None };
    public static readonly ExecutionResult Exit = new() { Value = RuntimeValue.Null, ControlFlow = ControlFlow.Exit };
    public static readonly ExecutionResult Continue = new() { Value = RuntimeValue.Null, ControlFlow = ControlFlow.Continue };
    public static readonly ExecutionResult Terminate = new() { Value = RuntimeValue.Null, ControlFlow = ControlFlow.Terminate };

    public static ExecutionResult WithValue(RuntimeValue value) => new() { Value = value, ControlFlow = ControlFlow.None };
    public static ExecutionResult Return(RuntimeValue value) => new() { Value = value, ControlFlow = ControlFlow.Return };

    public bool IsNormal => ControlFlow == ControlFlow.None;
    public bool IsReturn => ControlFlow == ControlFlow.Return;
    public bool IsExit => ControlFlow == ControlFlow.Exit;
    public bool IsContinue => ControlFlow == ControlFlow.Continue;
    public bool IsTerminate => ControlFlow == ControlFlow.Terminate;
    public bool ShouldBreakLoop => ControlFlow is ControlFlow.Exit or ControlFlow.Return or ControlFlow.Terminate;
}

/// <summary>
/// Scope for local variables within a block.
/// </summary>
public class LocalScope : IDisposable
{
    private readonly ExecutionContext _context;
    private readonly Dictionary<string, RuntimeValue> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly LocalScope? _parent;
    private bool _disposed;

    public LocalScope(ExecutionContext context, LocalScope? parent = null)
    {
        _context = context;
        _parent = parent;
    }

    /// <summary>
    /// Declare a new local variable.
    /// </summary>
    public void DeclareVariable(string name, RuntimeValue value)
    {
        _variables[name] = value;
    }

    /// <summary>
    /// Get a variable value (searches parent scopes).
    /// </summary>
    public bool TryGetVariable(string name, out RuntimeValue value)
    {
        if (_variables.TryGetValue(name, out value))
            return true;

        if (_parent != null)
            return _parent.TryGetVariable(name, out value);

        value = RuntimeValue.Null;
        return false;
    }

    /// <summary>
    /// Set a variable value (searches parent scopes).
    /// </summary>
    public bool TrySetVariable(string name, RuntimeValue value)
    {
        if (_variables.ContainsKey(name))
        {
            _variables[name] = value;
            return true;
        }

        if (_parent != null)
            return _parent.TrySetVariable(name, value);

        return false;
    }

    /// <summary>
    /// Check if a variable exists in this scope or parents.
    /// </summary>
    public bool HasVariable(string name)
    {
        if (_variables.ContainsKey(name))
            return true;
        return _parent?.HasVariable(name) ?? false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _variables.Clear();
    }
}

/// <summary>
/// Execution context for running IntMUD code.
/// </summary>
public class ExecutionContext : IDisposable
{
    private readonly DataStack _dataStack;
    private readonly VariableStack _variableStack;
    private readonly FunctionStack _functionStack;
    private LocalScope? _currentScope;
    private int _instructionCount;
    private bool _disposed;

    /// <summary>
    /// Maximum instructions per execution cycle (default 5000).
    /// </summary>
    public int MaxInstructions { get; set; } = 5000;

    /// <summary>
    /// Current instruction count in this execution cycle.
    /// </summary>
    public int InstructionCount => _instructionCount;

    /// <summary>
    /// Whether execution has been terminated.
    /// </summary>
    public bool IsTerminated { get; private set; }

    /// <summary>
    /// The current object (este).
    /// </summary>
    public object? CurrentObject { get; set; }

    /// <summary>
    /// Arguments passed to the current function (arg0-arg9).
    /// </summary>
    public RuntimeValue[] Arguments { get; } = new RuntimeValue[10];

    /// <summary>
    /// Number of arguments passed to the current function.
    /// </summary>
    public int ArgumentCount { get; set; }

    /// <summary>
    /// The class registry for looking up classes.
    /// </summary>
    public IClassRegistry? ClassRegistry { get; set; }

    /// <summary>
    /// The program being executed.
    /// </summary>
    public CompiledProgram? Program { get; set; }

    public ExecutionContext()
    {
        _dataStack = new DataStack();
        _variableStack = new VariableStack();
        _functionStack = new FunctionStack();
    }

    /// <summary>
    /// Create a new local scope for a block.
    /// </summary>
    public LocalScope PushScope()
    {
        var scope = new LocalScope(this, _currentScope);
        _currentScope = scope;
        return scope;
    }

    /// <summary>
    /// Pop the current local scope.
    /// </summary>
    public void PopScope()
    {
        if (_currentScope != null)
        {
            var parent = _currentScope;
            _currentScope = null;
            parent.Dispose();
        }
    }

    /// <summary>
    /// Get a local variable value.
    /// </summary>
    public RuntimeValue GetLocalVariable(string name)
    {
        if (_currentScope != null && _currentScope.TryGetVariable(name, out var value))
            return value;
        return RuntimeValue.Null;
    }

    /// <summary>
    /// Set a local variable value.
    /// </summary>
    public bool SetLocalVariable(string name, RuntimeValue value)
    {
        if (_currentScope != null)
            return _currentScope.TrySetVariable(name, value);
        return false;
    }

    /// <summary>
    /// Declare a new local variable.
    /// </summary>
    public void DeclareLocalVariable(string name, RuntimeValue value)
    {
        _currentScope?.DeclareVariable(name, value);
    }

    /// <summary>
    /// Check if a local variable exists.
    /// </summary>
    public bool HasLocalVariable(string name)
    {
        return _currentScope?.HasVariable(name) ?? false;
    }

    /// <summary>
    /// Increment the instruction counter and check limits.
    /// </summary>
    public void IncrementInstructionCount()
    {
        _instructionCount++;
        if (_instructionCount >= MaxInstructions)
        {
            throw new ExecutionLimitException($"Execution limit reached ({MaxInstructions} instructions)");
        }
    }

    /// <summary>
    /// Reset the instruction counter (for new execution cycle).
    /// </summary>
    public void ResetInstructionCount()
    {
        _instructionCount = 0;
    }

    /// <summary>
    /// Mark execution as terminated.
    /// </summary>
    public void Terminate()
    {
        IsTerminated = true;
    }

    /// <summary>
    /// Get argument by index (arg0-arg9).
    /// </summary>
    public RuntimeValue GetArgument(int index)
    {
        if (index < 0 || index >= 10)
            return RuntimeValue.Null;
        if (index >= ArgumentCount)
            return RuntimeValue.Null;
        return Arguments[index];
    }

    /// <summary>
    /// Set arguments for a function call.
    /// </summary>
    public void SetArguments(RuntimeValue[] args)
    {
        ArgumentCount = Math.Min(args.Length, 10);
        for (int i = 0; i < 10; i++)
        {
            Arguments[i] = i < args.Length ? args[i] : RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Clear arguments.
    /// </summary>
    public void ClearArguments()
    {
        ArgumentCount = 0;
        Array.Fill(Arguments, RuntimeValue.Null);
    }

    /// <summary>
    /// Push a function call onto the stack.
    /// </summary>
    public void PushFunction(string functionName, object? targetObject, RuntimeValue[] args)
    {
        // Push a new frame
        ref var frame = ref _functionStack.Push();
        frame.FunctionName = functionName;
        frame.ArgumentCount = args.Length;
        frame.BytecodeOffset = 0;  // Not used in AST interpreter
        frame.CurrentOffset = 0;
        frame.VariableStackBase = 0;
        frame.DataStackBase = 0;
        frame.Flags = FunctionFrameFlags.None;

        // Set new arguments
        SetArguments(args);
        CurrentObject = targetObject;
    }

    /// <summary>
    /// Pop a function call from the stack.
    /// </summary>
    public void PopFunction()
    {
        if (_functionStack.Depth > 0)
        {
            _functionStack.Pop();
        }
    }

    /// <summary>
    /// Current function call depth.
    /// </summary>
    public int CallDepth => _functionStack.Depth;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _currentScope?.Dispose();
        _dataStack.Dispose();
    }
}

/// <summary>
/// Interface for looking up classes.
/// </summary>
public interface IClassRegistry
{
    /// <summary>
    /// Get a class by name.
    /// </summary>
    CompiledClass? GetClass(string name);

    /// <summary>
    /// Get all classes.
    /// </summary>
    IEnumerable<CompiledClass> GetAllClasses();

    /// <summary>
    /// Get the first object of a class.
    /// </summary>
    object? GetFirstObject(string className);
}

/// <summary>
/// Exception thrown when execution limit is reached.
/// </summary>
public class ExecutionLimitException : Exception
{
    public ExecutionLimitException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown during execution.
/// </summary>
public class ExecutionException : Exception
{
    public string? SourceFile { get; }
    public int Line { get; }
    public int Column { get; }

    public ExecutionException(string message, string? sourceFile = null, int line = 0, int column = 0)
        : base(FormatMessage(message, sourceFile, line, column))
    {
        SourceFile = sourceFile;
        Line = line;
        Column = column;
    }

    private static string FormatMessage(string message, string? sourceFile, int line, int column)
    {
        if (sourceFile != null && line > 0)
            return $"{sourceFile}({line},{column}): {message}";
        return message;
    }
}
