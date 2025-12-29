using System.Text;

namespace IntMud.Core.Configuration;

/// <summary>
/// Main configuration options for IntMUD runtime.
/// </summary>
public sealed class IntMudOptions
{
    /// <summary>
    /// Maximum instructions per execution cycle.
    /// Default: 5000 (matches VarExecIni in original).
    /// </summary>
    public int MaxInstructionsPerCycle { get; set; } = 5000;

    /// <summary>
    /// Maximum function call stack depth.
    /// Default: 40 (matches original).
    /// </summary>
    public int MaxCallStackDepth { get; set; } = 40;

    /// <summary>
    /// Maximum variables on variable stack.
    /// Default: 500 (matches original).
    /// </summary>
    public int MaxVariableStackSize { get; set; } = 500;

    /// <summary>
    /// Data stack size in bytes.
    /// Default: 65536 (64KB, matches original).
    /// </summary>
    public int DataStackSize { get; set; } = 65536;

    /// <summary>
    /// Source file encoding.
    /// Default: UTF-8 (original used ISO-8859-1).
    /// </summary>
    public Encoding SourceEncoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Enable hot-reload for .int files.
    /// Default: true.
    /// </summary>
    public bool EnableHotReload { get; set; } = true;

    /// <summary>
    /// Hot-reload debounce time in milliseconds.
    /// Default: 500ms.
    /// </summary>
    public int HotReloadDebounceMs { get; set; } = 500;

    /// <summary>
    /// Maximum base classes per class (herda).
    /// Default: 50 (matches HERDA_TAM in original).
    /// </summary>
    public int MaxBaseClasses { get; set; } = 50;

    /// <summary>
    /// Maximum class name length.
    /// Default: 47 characters (matches original).
    /// </summary>
    public int MaxClassNameLength { get; set; } = 47;

    /// <summary>
    /// Event loop tick interval in milliseconds.
    /// Default: 100ms (original uses 100ms = 1 time unit).
    /// </summary>
    public int EventLoopTickMs { get; set; } = 100;

    /// <summary>
    /// Maximum objects per class (0 = unlimited).
    /// Default: 0.
    /// </summary>
    public int MaxObjectsPerClass { get; set; } = 0;

    /// <summary>
    /// Enable strict type checking.
    /// Default: false (for compatibility).
    /// </summary>
    public bool StrictTypeChecking { get; set; } = false;

    /// <summary>
    /// Enable execution tracing for debugging.
    /// Default: false.
    /// </summary>
    public bool EnableExecutionTracing { get; set; } = false;
}

/// <summary>
/// Networking configuration options.
/// </summary>
public sealed class NetworkingOptions
{
    /// <summary>
    /// Maximum concurrent connections per server.
    /// Default: 128 (matches FD_SETSIZE).
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 128;

    /// <summary>
    /// Socket receive buffer size in bytes.
    /// Default: 512 entries (SOCKET_REC).
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 512;

    /// <summary>
    /// Socket send buffer size in bytes.
    /// Default: 2048 (SOCKET_ENV).
    /// </summary>
    public int SendBufferSize { get; set; } = 2048;

    /// <summary>
    /// Enable WebSocket support.
    /// Default: true.
    /// </summary>
    public bool EnableWebSocket { get; set; } = true;

    /// <summary>
    /// WebSocket endpoint path.
    /// Default: "/ws".
    /// </summary>
    public string WebSocketPath { get; set; } = "/ws";

    /// <summary>
    /// Connection timeout in seconds.
    /// Default: 30 seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// SSL/TLS handshake timeout in seconds.
    /// Default: 10 seconds.
    /// </summary>
    public int SslHandshakeTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Enable anti-flooder protection.
    /// Default: true.
    /// </summary>
    public bool EnableAntiFlooder { get; set; } = true;

    /// <summary>
    /// Anti-flooder threshold in seconds.
    /// Default: 60 seconds.
    /// </summary>
    public int AntiFlooderThresholdSeconds { get; set; } = 60;
}

/// <summary>
/// Logging and metrics configuration.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Enable structured logging.
    /// Default: true.
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Enable OpenTelemetry metrics.
    /// Default: true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Enable distributed tracing.
    /// Default: false.
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// Log file path.
    /// Default: "logs/intmud-.log".
    /// </summary>
    public string LogFilePath { get; set; } = "logs/intmud-.log";

    /// <summary>
    /// Metrics endpoint path.
    /// Default: "/metrics".
    /// </summary>
    public string MetricsPath { get; set; } = "/metrics";
}
