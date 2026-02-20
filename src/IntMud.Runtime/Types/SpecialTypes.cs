using System.Collections.Concurrent;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Registry for special IntMUD types that have automatic event handling.
/// </summary>
public static class SpecialTypeRegistry
{
    private static readonly HashSet<string> _timerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "inttempo"
    };

    private static readonly HashSet<string> _execTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "intexec"
    };

    private static readonly HashSet<string> _consoleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "telatxt"
    };

    private static readonly HashSet<string> _socketTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "socket"
    };

    private static readonly HashSet<string> _serverTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "serv"
    };

    private static readonly HashSet<string> _arqExecTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "arqexec"
    };

    private static readonly HashSet<string> _debugTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "debug"
    };

    /// <summary>
    /// Check if a type is a timer type (calls varname_exec when time expires).
    /// </summary>
    public static bool IsTimerType(string typeName) => _timerTypes.Contains(typeName);

    /// <summary>
    /// Check if a type is an exec trigger type (calls varname_exec when set to non-zero).
    /// </summary>
    public static bool IsExecTriggerType(string typeName) => _execTriggerTypes.Contains(typeName);

    /// <summary>
    /// Check if a type is a console type (calls varname_tecla on key press).
    /// </summary>
    public static bool IsConsoleType(string typeName) => _consoleTypes.Contains(typeName);

    /// <summary>
    /// Check if a type is a socket type (fires msg/fechou events on I/O).
    /// </summary>
    public static bool IsSocketType(string typeName) => _socketTypes.Contains(typeName);

    /// <summary>
    /// Check if a type is a server type (manages socket connections).
    /// </summary>
    public static bool IsServerType(string typeName) => _serverTypes.Contains(typeName);

    /// <summary>
    /// Check if a type is an arqexec type (fires msg/fechou events on process I/O).
    /// </summary>
    public static bool IsArqExecType(string typeName) => _arqExecTypes.Contains(typeName);

    /// <summary>
    /// Check if a type is a debug type (provides debug information).
    /// </summary>
    public static bool IsDebugType(string typeName) => _debugTypes.Contains(typeName);

    /// <summary>
    /// Check if a type is any special type that requires event handling.
    /// </summary>
    public static bool IsSpecialType(string typeName) =>
        IsTimerType(typeName) || IsExecTriggerType(typeName) ||
        IsConsoleType(typeName) || IsSocketType(typeName) ||
        IsServerType(typeName) || IsArqExecType(typeName) ||
        IsDebugType(typeName);

    /// <summary>
    /// Get the event function name for a variable of a special type.
    /// </summary>
    public static string? GetEventFunctionName(string typeName, string variableName, SpecialTypeEvent eventType)
    {
        return eventType switch
        {
            SpecialTypeEvent.TimerExpired when IsTimerType(typeName) => $"{variableName}_exec",
            SpecialTypeEvent.ValueChanged when IsExecTriggerType(typeName) => $"{variableName}_exec",
            SpecialTypeEvent.KeyPressed when IsConsoleType(typeName) => $"{variableName}_tecla",
            SpecialTypeEvent.DebugError when IsDebugType(typeName) => $"{variableName}_erro",
            _ => null
        };
    }
}

/// <summary>
/// Events that can be triggered by special types.
/// </summary>
public enum SpecialTypeEvent
{
    /// <summary>Timer expired (inttempo).</summary>
    TimerExpired,
    /// <summary>Value changed to non-zero (intexec).</summary>
    ValueChanged,
    /// <summary>Key was pressed (telatxt).</summary>
    KeyPressed,
    /// <summary>Debug error occurred.</summary>
    DebugError
}

/// <summary>
/// Represents a timer variable instance (inttempo).
/// </summary>
public sealed class TimerInstance
{
    /// <summary>The object that owns this timer.</summary>
    public BytecodeRuntimeObject Owner { get; }

    /// <summary>The variable name.</summary>
    public string VariableName { get; }

    /// <summary>Current timer value in deciseconds (1/10 second).</summary>
    public long Value { get; set; }

    /// <summary>Whether the timer is active.</summary>
    public bool IsActive => Value > 0;

    public TimerInstance(BytecodeRuntimeObject owner, string variableName)
    {
        Owner = owner;
        VariableName = variableName;
    }
}

/// <summary>
/// Represents an exec trigger variable instance (intexec).
/// </summary>
public sealed class ExecTriggerInstance
{
    /// <summary>The object that owns this trigger.</summary>
    public BytecodeRuntimeObject Owner { get; }

    /// <summary>The variable name.</summary>
    public string VariableName { get; }

    /// <summary>Current value.</summary>
    public long Value { get; set; }

    /// <summary>Previous value (to detect changes).</summary>
    public long PreviousValue { get; set; }

    /// <summary>Whether the trigger should fire.</summary>
    public bool ShouldFire => Value != 0 && Value != PreviousValue;

    public ExecTriggerInstance(BytecodeRuntimeObject owner, string variableName)
    {
        Owner = owner;
        VariableName = variableName;
    }
}

/// <summary>
/// Manages all special type instances and their events.
/// </summary>
public sealed class SpecialTypeManager
{
    private readonly List<TimerInstance> _timers = new();
    private readonly List<ExecTriggerInstance> _execTriggers = new();

    /// <summary>
    /// Register a timer instance.
    /// </summary>
    public void RegisterTimer(BytecodeRuntimeObject owner, string variableName)
    {
        _timers.Add(new TimerInstance(owner, variableName));
    }

    /// <summary>
    /// Register an exec trigger instance.
    /// </summary>
    public void RegisterExecTrigger(BytecodeRuntimeObject owner, string variableName)
    {
        _execTriggers.Add(new ExecTriggerInstance(owner, variableName));
    }

    /// <summary>
    /// Get all active timers.
    /// </summary>
    public IEnumerable<TimerInstance> Timers => _timers;

    /// <summary>
    /// Get all exec triggers.
    /// </summary>
    public IEnumerable<ExecTriggerInstance> ExecTriggers => _execTriggers;

    /// <summary>
    /// Process timer tick and return timers that should fire.
    /// </summary>
    /// <param name="elapsedDeciseconds">Time elapsed in deciseconds (1/10 second).</param>
    public IEnumerable<TimerInstance> ProcessTimerTick(int elapsedDeciseconds)
    {
        var firedTimers = new List<TimerInstance>();

        foreach (var timer in _timers)
        {
            if (!timer.IsActive)
                continue;

            timer.Value -= elapsedDeciseconds;
            if (timer.Value <= 0)
            {
                timer.Value = 0;
                firedTimers.Add(timer);
            }
        }

        return firedTimers;
    }

    /// <summary>
    /// Process exec triggers and return those that should fire.
    /// </summary>
    public IEnumerable<ExecTriggerInstance> ProcessExecTriggers()
    {
        var firedTriggers = new List<ExecTriggerInstance>();

        foreach (var trigger in _execTriggers)
        {
            if (trigger.ShouldFire)
            {
                firedTriggers.Add(trigger);
                trigger.PreviousValue = trigger.Value;
            }
        }

        return firedTriggers;
    }

    /// <summary>
    /// Update a timer value by variable name.
    /// </summary>
    public void SetTimerValue(BytecodeRuntimeObject owner, string variableName, long value)
    {
        var timer = _timers.FirstOrDefault(t => t.Owner == owner && t.VariableName == variableName);
        if (timer != null)
            timer.Value = value;
    }

    /// <summary>
    /// Update an exec trigger value by variable name.
    /// </summary>
    public void SetExecTriggerValue(BytecodeRuntimeObject owner, string variableName, long value)
    {
        var trigger = _execTriggers.FirstOrDefault(t => t.Owner == owner && t.VariableName == variableName);
        if (trigger != null)
            trigger.Value = value;
    }

    /// <summary>
    /// Thread-safe queue for events fired from background threads (socket I/O, etc.).
    /// These are drained synchronously in the event loop.
    /// </summary>
    private readonly ConcurrentQueue<PendingScriptEvent> _pendingEvents = new();

    /// <summary>
    /// Enqueue an event to be dispatched in the next event loop iteration.
    /// Thread-safe - can be called from background I/O tasks.
    /// </summary>
    public void EnqueueEvent(PendingScriptEvent evt)
    {
        _pendingEvents.Enqueue(evt);
    }

    /// <summary>
    /// Drain all pending events from the queue.
    /// Called from the event loop thread.
    /// </summary>
    public IEnumerable<PendingScriptEvent> DrainPendingEvents()
    {
        var events = new List<PendingScriptEvent>();
        while (_pendingEvents.TryDequeue(out var evt))
        {
            events.Add(evt);
        }
        return events;
    }

    /// <summary>
    /// Clear all registered instances.
    /// </summary>
    public void Clear()
    {
        _timers.Clear();
        _execTriggers.Clear();
        // Drain any pending events
        while (_pendingEvents.TryDequeue(out _)) { }
    }
}

/// <summary>
/// Represents a script event pending dispatch.
/// Queued from background I/O threads, dispatched synchronously in the event loop.
/// Matches C++ ExecIni/ExecArg/ExecX/ExecFim pattern.
/// </summary>
public sealed class PendingScriptEvent
{
    /// <summary>The object that owns the variable that fired this event.</summary>
    public BytecodeRuntimeObject Owner { get; }

    /// <summary>The handler function name (e.g., "s_msg", "servidor_socket").</summary>
    public string HandlerName { get; }

    /// <summary>Arguments to pass to the handler function.</summary>
    public RuntimeValue[] Args { get; }

    public PendingScriptEvent(BytecodeRuntimeObject owner, string handlerName, params RuntimeValue[] args)
    {
        Owner = owner;
        HandlerName = handlerName;
        Args = args;
    }
}
