using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Types;
using IntMud.Runtime.Values;
using BytecodeCompiledFunction = IntMud.Compiler.Bytecode.CompiledFunction;

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
    /// Initialize the runtime by calling iniclasse on ALL classes (matching original IntMUD behavior).
    /// In original IntMUD:
    /// 1. All classes are loaded
    /// 2. iniclasse is called on ALL classes with the class name as argument
    /// 3. iniclasse creates objects via criar() if needed
    /// 4. Objects created via criar() automatically have their "ini" function called
    /// </summary>
    public void Initialize()
    {
        // Clear registry before starting
        GlobalObjectRegistry.Clear();

        // Call iniclasse on ALL classes (like original IntMUD)
        // iniclasse is responsible for creating objects via criar()
        // The pattern is: const iniclasse = !$[arg0] && criar(arg0)
        // This means: if no object of class arg0 exists, create one
        CallIniclasseOnAllClasses();

        // Now synchronize: get all objects created by iniclasse and add to runtime
        SyncObjectsFromRegistry();
    }

    /// <summary>
    /// Call iniclasse function on all classes (matching original IntMUD C++ behavior).
    /// In original IntMUD, iniclasse is called for EVERY class at startup with the class name as argument.
    /// </summary>
    private void CallIniclasseOnAllClasses()
    {
        // Clear the global registry before initialization
        GlobalObjectRegistry.Clear();

        foreach (var (className, unit) in _compiledUnits)
        {
            // Check if the class has an 'iniclasse' function or constant
            // In IntMUD, iniclasse can be a const expression like: const iniclasse = !$[arg0] && criar(arg0)
            BytecodeCompiledFunction? iniclasseFunc = null;
            unit.Functions.TryGetValue("iniclasse", out iniclasseFunc);

            // Also check if there's an iniclasse constant (expression that will be evaluated)
            var hasIniclasseConst = unit.Constants.ContainsKey("iniclasse");

            if (iniclasseFunc == null && !hasIniclasseConst)
                continue;

            try
            {
                // Create a temporary interpreter for this call
                var interpreter = new BytecodeInterpreter(unit, _compiledUnits);
                interpreter.WriteOutput = text => OnOutput?.Invoke(text);

                // Create a temporary object for 'this' context
                var tempInstance = new BytecodeRuntimeObject(unit);

                if (iniclasseFunc != null)
                {
                    // Call iniclasse function with the class name as argument
                    interpreter.ExecuteFunctionWithThis(
                        iniclasseFunc,
                        tempInstance,
                        unit,
                        new[] { RuntimeValue.FromString(className) });
                }
                else if (hasIniclasseConst)
                {
                    // For const iniclasse, we need to set arg0 and evaluate the constant
                    // The constant expression will use arg0 to get the class name
                    // We simulate this by storing arg0 in globals before evaluation
                    interpreter.Globals["arg0"] = RuntimeValue.FromString(className);

                    // Evaluate the iniclasse constant (this may call criar() to create objects)
                    if (unit.Constants.TryGetValue("iniclasse", out var constant))
                    {
                        // Evaluate the constant expression in context
                        interpreter.ExecuteExpressionConstant(constant, tempInstance, unit);
                    }
                }
            }
            catch (TerminateException)
            {
                OnTerminate?.Invoke();
                return;
            }
            catch (Exception ex)
            {
                OnOutput?.Invoke($"Error in {className}.iniclasse: {ex.Message}\n");
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

        // Register the object in the global registry (for $classname syntax)
        GlobalObjectRegistry.Register(instance);

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
    /// Synchronize objects from GlobalObjectRegistry into the runtime's instance tracking.
    /// This is called after iniclasse creates objects via criar().
    /// </summary>
    private void SyncObjectsFromRegistry()
    {
        var allObjects = GlobalObjectRegistry.GetAllObjects();

        foreach (var obj in allObjects)
        {
            // Skip if already tracked
            if (_instances.ContainsKey(obj.ClassName) && _instances[obj.ClassName] == obj)
                continue;

            // Add to instances (use class name as key for first object)
            if (!_instances.ContainsKey(obj.ClassName))
            {
                _instances[obj.ClassName] = obj;
            }

            // Create interpreter if needed
            if (!_interpreters.ContainsKey(obj))
            {
                if (_compiledUnits.TryGetValue(obj.ClassName, out var unit))
                {
                    var interpreter = new BytecodeInterpreter(unit, _compiledUnits);
                    interpreter.WriteOutput = text => OnOutput?.Invoke(text);
                    _interpreters[obj] = interpreter;

                    // Register special types for this object
                    RegisterSpecialTypes(obj, unit);
                }
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

        // Note: ini() is already called by CreateObject when objects are created via criar()
        // during iniclasse phase. No need to call CallInitializationFunctions() again.

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
