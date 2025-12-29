using System.Diagnostics.Metrics;

namespace IntMud.Hosting.Observability;

/// <summary>
/// Metrics for IntMUD engine.
/// </summary>
public sealed class IntMudMetrics : IDisposable
{
    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _instructionsExecuted;
    private readonly Counter<long> _objectsCreated;
    private readonly Counter<long> _objectsDeleted;
    private readonly Counter<long> _functionsInvoked;
    private readonly Counter<long> _bytesReceived;
    private readonly Counter<long> _bytesSent;
    private readonly Counter<long> _connectionsAccepted;
    private readonly Counter<long> _connectionsClosed;
    private readonly Counter<long> _compilationErrors;

    // Gauges (using ObservableGauge)
    private int _activeConnections;
    private int _activeObjects;
    private int _loadedClasses;
    private long _memoryUsed;

    public IntMudMetrics()
    {
        _meter = new Meter("IntMud.Engine", "1.0.0");

        // Counters
        _instructionsExecuted = _meter.CreateCounter<long>(
            "intmud.instructions.executed",
            description: "Total instructions executed");

        _objectsCreated = _meter.CreateCounter<long>(
            "intmud.objects.created",
            description: "Total objects created");

        _objectsDeleted = _meter.CreateCounter<long>(
            "intmud.objects.deleted",
            description: "Total objects deleted");

        _functionsInvoked = _meter.CreateCounter<long>(
            "intmud.functions.invoked",
            description: "Total functions invoked");

        _bytesReceived = _meter.CreateCounter<long>(
            "intmud.network.bytes.received",
            unit: "bytes",
            description: "Total bytes received from network");

        _bytesSent = _meter.CreateCounter<long>(
            "intmud.network.bytes.sent",
            unit: "bytes",
            description: "Total bytes sent to network");

        _connectionsAccepted = _meter.CreateCounter<long>(
            "intmud.connections.accepted",
            description: "Total connections accepted");

        _connectionsClosed = _meter.CreateCounter<long>(
            "intmud.connections.closed",
            description: "Total connections closed");

        _compilationErrors = _meter.CreateCounter<long>(
            "intmud.compilation.errors",
            description: "Total compilation errors");

        // Gauges
        _meter.CreateObservableGauge(
            "intmud.connections.active",
            () => _activeConnections,
            description: "Current active connections");

        _meter.CreateObservableGauge(
            "intmud.objects.active",
            () => _activeObjects,
            description: "Current active objects");

        _meter.CreateObservableGauge(
            "intmud.classes.loaded",
            () => _loadedClasses,
            description: "Number of loaded classes");

        _meter.CreateObservableGauge(
            "intmud.memory.used",
            () => _memoryUsed,
            unit: "bytes",
            description: "Memory used by runtime");
    }

    // Counter methods
    public void RecordInstructionsExecuted(long count = 1) => _instructionsExecuted.Add(count);
    public void RecordObjectCreated() => _objectsCreated.Add(1);
    public void RecordObjectDeleted() => _objectsDeleted.Add(1);
    public void RecordFunctionInvoked() => _functionsInvoked.Add(1);
    public void RecordBytesReceived(long bytes) => _bytesReceived.Add(bytes);
    public void RecordBytesSent(long bytes) => _bytesSent.Add(bytes);
    public void RecordConnectionAccepted() => _connectionsAccepted.Add(1);
    public void RecordConnectionClosed() => _connectionsClosed.Add(1);
    public void RecordCompilationError() => _compilationErrors.Add(1);

    // Gauge setters
    public void SetActiveConnections(int count) => Interlocked.Exchange(ref _activeConnections, count);
    public void SetActiveObjects(int count) => Interlocked.Exchange(ref _activeObjects, count);
    public void SetLoadedClasses(int count) => Interlocked.Exchange(ref _loadedClasses, count);
    public void SetMemoryUsed(long bytes) => Interlocked.Exchange(ref _memoryUsed, bytes);

    public void Dispose()
    {
        _meter.Dispose();
    }
}
