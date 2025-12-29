using IntMud.Core.Types;

namespace IntMud.Core.Execution;

/// <summary>
/// Main execution engine interface.
/// Responsible for executing bytecode and managing execution state.
/// </summary>
public interface IExecutionEngine
{
    /// <summary>
    /// Maximum instructions per execution cycle.
    /// Default is 5000 (VarExecIni in original).
    /// </summary>
    int MaxInstructionsPerCycle { get; set; }

    /// <summary>
    /// Current instruction count in this cycle.
    /// </summary>
    int CurrentInstructionCount { get; }

    /// <summary>
    /// Whether execution is currently in progress.
    /// </summary>
    bool IsExecuting { get; }

    /// <summary>
    /// Execute a function on a class (no object context).
    /// Used for class-level initialization (iniclasse).
    /// </summary>
    /// <param name="intClass">The class to execute on</param>
    /// <param name="functionName">Name of the function to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if function executed successfully</returns>
    ValueTask<bool> ExecuteAsync(
        IIntClass intClass,
        string functionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a function on an object instance.
    /// </summary>
    /// <param name="obj">The object to execute on</param>
    /// <param name="functionName">Name of the function to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if function executed successfully</returns>
    ValueTask<bool> ExecuteAsync(
        IIntObject obj,
        string functionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a function with arguments.
    /// </summary>
    /// <param name="obj">The object to execute on (or null for class context)</param>
    /// <param name="functionName">Name of the function to execute</param>
    /// <param name="args">Arguments to pass to the function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if function executed successfully</returns>
    ValueTask<bool> ExecuteAsync(
        IIntObject? obj,
        string functionName,
        object?[] args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Observable stream of execution events.
    /// </summary>
    IObservable<ExecutionEvent> Events { get; }

    /// <summary>
    /// Reset execution state for a new cycle.
    /// </summary>
    void Reset();
}

/// <summary>
/// Execution event for monitoring and debugging.
/// </summary>
public abstract record ExecutionEvent
{
    /// <summary>Timestamp of the event</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Function execution started.
/// </summary>
public sealed record FunctionStartedEvent(
    IIntClass Class,
    IIntObject? Object,
    string FunctionName
) : ExecutionEvent;

/// <summary>
/// Function execution completed.
/// </summary>
public sealed record FunctionCompletedEvent(
    IIntClass Class,
    IIntObject? Object,
    string FunctionName,
    bool Success,
    TimeSpan Duration
) : ExecutionEvent;

/// <summary>
/// Object created event.
/// </summary>
public sealed record ObjectCreatedEvent(
    IIntObject Object
) : ExecutionEvent;

/// <summary>
/// Object destroyed event.
/// </summary>
public sealed record ObjectDestroyedEvent(
    Guid ObjectId,
    string ClassName
) : ExecutionEvent;

/// <summary>
/// Execution error event.
/// </summary>
public sealed record ExecutionErrorEvent(
    string Message,
    SourceLocation? Location,
    Exception? Exception
) : ExecutionEvent;
