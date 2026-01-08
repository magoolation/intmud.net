using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Types;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Execution;

/// <summary>
/// The IntMUD runtime that manages object instances and event processing.
/// This mimics the original IntMUD behavior where classes with special type
/// variables (telatxt, inttempo, serv, etc.) are automatically instantiated
/// and their events are processed in the main loop.
/// </summary>
public sealed class IntMudRuntime : IDisposable
{
    private readonly Dictionary<string, CompiledUnit> _compiledUnits;
    private readonly Dictionary<string, BytecodeRuntimeObject> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<BytecodeRuntimeObject, BytecodeInterpreter> _interpreters = new();
    private readonly SpecialTypeManager _specialTypes = new();
    private readonly List<ConsoleInstance> _consoleInstances = new();

    private bool _running;
    private bool _disposed;
    private Thread? _eventLoopThread;
    private CancellationTokenSource? _cancellationSource;

    /// <summary>
    /// Event raised when output should be written to the console.
    /// </summary>
    public event Action<string>? OnOutput;

    /// <summary>
    /// Event raised when the runtime wants to read input.
    /// </summary>
    public event Func<string?>? OnReadKey;

    /// <summary>
    /// Event raised when the runtime is terminating.
    /// </summary>
    public event Action? OnTerminate;

    /// <summary>
    /// Whether the runtime is currently running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// All instantiated objects.
    /// </summary>
    public IReadOnlyDictionary<string, BytecodeRuntimeObject> Instances => _instances;

    /// <summary>
    /// The special type manager.
    /// </summary>
    public SpecialTypeManager SpecialTypes => _specialTypes;

    public IntMudRuntime(Dictionary<string, CompiledUnit> compiledUnits)
    {
        _compiledUnits = compiledUnits ?? throw new ArgumentNullException(nameof(compiledUnits));
    }

    /// <summary>
    /// Initialize the runtime by scanning classes and instantiating those with special types.
    /// </summary>
    public void Initialize()
    {
        // Scan all classes for special type variables
        foreach (var (className, unit) in _compiledUnits)
        {
            var hasSpecialTypes = false;

            foreach (var variable in unit.Variables)
            {
                if (SpecialTypeRegistry.IsSpecialType(variable.TypeName))
                {
                    hasSpecialTypes = true;
                    break;
                }
            }

            if (hasSpecialTypes)
            {
                // Create an instance of this class
                var instance = CreateInstance(className);
                if (instance != null)
                {
                    _instances[className] = instance;
                    RegisterSpecialTypes(instance, unit);
                }
            }
        }
    }

    /// <summary>
    /// Create an instance of a class.
    /// </summary>
    public BytecodeRuntimeObject? CreateInstance(string className)
    {
        if (!_compiledUnits.TryGetValue(className, out var unit))
            return null;

        // Resolve base classes
        var baseUnits = new List<CompiledUnit>();
        ResolveBaseClasses(unit, baseUnits, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var instance = new BytecodeRuntimeObject(unit, baseUnits);

        // Create an interpreter for this instance
        var interpreter = new BytecodeInterpreter(unit, _compiledUnits);
        interpreter.WriteOutput = text => OnOutput?.Invoke(text);
        _interpreters[instance] = interpreter;

        return instance;
    }

    private void ResolveBaseClasses(CompiledUnit unit, List<CompiledUnit> result, HashSet<string> visited)
    {
        foreach (var baseName in unit.BaseClasses)
        {
            if (visited.Contains(baseName))
                continue;

            visited.Add(baseName);

            if (_compiledUnits.TryGetValue(baseName, out var baseUnit))
            {
                result.Add(baseUnit);
                ResolveBaseClasses(baseUnit, result, visited);
            }
        }
    }

    private void RegisterSpecialTypes(BytecodeRuntimeObject instance, CompiledUnit unit)
    {
        foreach (var variable in unit.Variables)
        {
            if (SpecialTypeRegistry.IsTimerType(variable.TypeName))
            {
                _specialTypes.RegisterTimer(instance, variable.Name);
            }
            else if (SpecialTypeRegistry.IsExecTriggerType(variable.TypeName))
            {
                _specialTypes.RegisterExecTrigger(instance, variable.Name);
            }
            else if (SpecialTypeRegistry.IsConsoleType(variable.TypeName))
            {
                _consoleInstances.Add(new ConsoleInstance(instance, variable.Name));
            }
        }
    }

    /// <summary>
    /// Start the runtime event loop.
    /// </summary>
    public void Start()
    {
        if (_running)
            return;

        _running = true;
        _cancellationSource = new CancellationTokenSource();

        // Call initialization functions
        CallInitializationFunctions();

        // Start the event loop in a separate thread
        _eventLoopThread = new Thread(EventLoop)
        {
            Name = "IntMUD Event Loop",
            IsBackground = true
        };
        _eventLoopThread.Start();
    }

    /// <summary>
    /// Stop the runtime.
    /// </summary>
    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        _cancellationSource?.Cancel();
        _eventLoopThread?.Join(TimeSpan.FromSeconds(2));
    }

    private void CallInitializationFunctions()
    {
        // Call "ini" or "inicializar" function on each instance
        foreach (var (className, instance) in _instances)
        {
            if (!_interpreters.TryGetValue(instance, out var interpreter))
                continue;

            // Try to call "ini" first (IntMUD standard)
            var (initFunc, definingUnit) = instance.GetMethodWithUnit("ini");
            if (initFunc == null)
            {
                (initFunc, definingUnit) = instance.GetMethodWithUnit("inicializar");
            }

            if (initFunc != null && definingUnit != null)
            {
                try
                {
                    // Pass definingUnit to use the correct string pool for inherited methods
                    interpreter.ExecuteFunctionWithThis(initFunc, instance, definingUnit, Array.Empty<RuntimeValue>());
                }
                catch (TerminateException)
                {
                    _running = false;
                    OnTerminate?.Invoke();
                    return;
                }
                catch (Exception ex)
                {
                    OnOutput?.Invoke($"Error in {className}.{initFunc.Name}: {ex.Message}\n");
                    // Include stack trace for debugging
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }
    }

    private void EventLoop()
    {
        var lastTick = DateTime.UtcNow;

        while (_running && !(_cancellationSource?.IsCancellationRequested ?? true))
        {
            try
            {
                // Calculate elapsed time in deciseconds (1/10 second)
                var now = DateTime.UtcNow;
                var elapsed = (int)((now - lastTick).TotalMilliseconds / 100);
                if (elapsed > 0)
                {
                    lastTick = now;
                    ProcessTimerEvents(elapsed);
                }

                // Process exec triggers
                ProcessExecTriggers();

                // Process console input (non-blocking)
                ProcessConsoleInput();

                // Small sleep to avoid busy-waiting
                Thread.Sleep(10);
            }
            catch (TerminateException)
            {
                _running = false;
                OnTerminate?.Invoke();
                break;
            }
            catch (Exception ex)
            {
                OnOutput?.Invoke($"Event loop error: {ex.Message}\n");
            }
        }
    }

    private void ProcessTimerEvents(int elapsedDeciseconds)
    {
        var firedTimers = _specialTypes.ProcessTimerTick(elapsedDeciseconds);

        foreach (var timer in firedTimers)
        {
            CallEventFunction(timer.Owner, $"{timer.VariableName}_exec");
        }
    }

    private void ProcessExecTriggers()
    {
        var firedTriggers = _specialTypes.ProcessExecTriggers();

        foreach (var trigger in firedTriggers)
        {
            CallEventFunction(trigger.Owner, $"{trigger.VariableName}_exec");
        }
    }

    private void ProcessConsoleInput()
    {
        // Check if there's a key available
        var key = OnReadKey?.Invoke();
        if (string.IsNullOrEmpty(key))
            return;

        // Send to all console instances
        foreach (var console in _consoleInstances)
        {
            CallEventFunction(console.Owner, $"{console.VariableName}_tecla", RuntimeValue.FromString(key));
        }
    }

    private void CallEventFunction(BytecodeRuntimeObject owner, string functionName, params RuntimeValue[] args)
    {
        if (!_interpreters.TryGetValue(owner, out var interpreter))
            return;

        var (func, definingUnit) = owner.GetMethodWithUnit(functionName);
        if (func == null || definingUnit == null)
            return;

        try
        {
            // Pass definingUnit to use the correct string pool for inherited methods
            interpreter.ExecuteFunctionWithThis(func, owner, definingUnit, args);
        }
        catch (TerminateException)
        {
            _running = false;
            OnTerminate?.Invoke();
        }
        catch (Exception ex)
        {
            OnOutput?.Invoke($"Error in {owner.ClassName}.{functionName}: {ex.Message}\n");
        }
    }

    /// <summary>
    /// Set a timer value (in deciseconds).
    /// </summary>
    public void SetTimer(string className, string variableName, long value)
    {
        if (_instances.TryGetValue(className, out var instance))
        {
            _specialTypes.SetTimerValue(instance, variableName, value);
        }
    }

    /// <summary>
    /// Set an exec trigger value.
    /// </summary>
    public void SetExecTrigger(string className, string variableName, long value)
    {
        if (_instances.TryGetValue(className, out var instance))
        {
            _specialTypes.SetExecTriggerValue(instance, variableName, value);
        }
    }

    /// <summary>
    /// Write output to the console.
    /// </summary>
    public void WriteOutput(string text)
    {
        OnOutput?.Invoke(text);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
        _cancellationSource?.Dispose();
    }
}

/// <summary>
/// Represents a console (telatxt) instance.
/// </summary>
public sealed class ConsoleInstance
{
    public BytecodeRuntimeObject Owner { get; }
    public string VariableName { get; }

    public ConsoleInstance(BytecodeRuntimeObject owner, string variableName)
    {
        Owner = owner;
        VariableName = variableName;
    }
}
