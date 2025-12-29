using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntMud.Hosting;

/// <summary>
/// Builder for configuring and creating the IntMUD host.
/// </summary>
public sealed class IntMudHostBuilder
{
    private readonly HostApplicationBuilder _builder;
    private readonly IntMudHostOptions _options = new();

    public IntMudHostBuilder(string[] args)
    {
        _builder = Host.CreateApplicationBuilder(args);
    }

    /// <summary>
    /// Configure the host options.
    /// </summary>
    public IntMudHostBuilder Configure(Action<IntMudHostOptions> configure)
    {
        configure(_options);
        return this;
    }

    /// <summary>
    /// Add services to the container.
    /// </summary>
    public IntMudHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        configure(_builder.Services);
        return this;
    }

    /// <summary>
    /// Configure logging.
    /// </summary>
    public IntMudHostBuilder ConfigureLogging(Action<ILoggingBuilder> configure)
    {
        configure(_builder.Logging);
        return this;
    }

    /// <summary>
    /// Build the host.
    /// </summary>
    public IHost Build()
    {
        // Register core services
        _builder.Services.AddSingleton(_options);
        _builder.Services.AddSingleton<IntMudEngine>();
        _builder.Services.AddHostedService<IntMudHostedService>();

        return _builder.Build();
    }
}

/// <summary>
/// Options for configuring the IntMUD host.
/// </summary>
public sealed class IntMudHostOptions
{
    /// <summary>
    /// Path to the .int source files directory.
    /// </summary>
    public string SourcePath { get; set; } = ".";

    /// <summary>
    /// Main file to load.
    /// </summary>
    public string MainFile { get; set; } = "main.int";

    /// <summary>
    /// Enable hot-reload of source files.
    /// </summary>
    public bool EnableHotReload { get; set; }

    /// <summary>
    /// Server port to listen on (0 = disabled).
    /// </summary>
    public int ServerPort { get; set; }

    /// <summary>
    /// Enable debug mode.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Maximum execution cycles per tick.
    /// </summary>
    public int MaxCyclesPerTick { get; set; } = 5000;

    /// <summary>
    /// Tick interval in milliseconds.
    /// </summary>
    public int TickIntervalMs { get; set; } = 100;
}

/// <summary>
/// The IntMUD execution engine.
/// </summary>
public sealed class IntMudEngine : IDisposable
{
    private readonly IntMudHostOptions _options;
    private readonly ILogger<IntMudEngine> _logger;
    private bool _running;
    private bool _disposed;

    public IntMudEngine(IntMudHostOptions options, ILogger<IntMudEngine> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Whether the engine is running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Start the engine.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            return;

        _logger.LogInformation("Starting IntMUD engine...");
        _logger.LogInformation("Source path: {SourcePath}", _options.SourcePath);
        _logger.LogInformation("Main file: {MainFile}", _options.MainFile);

        _running = true;

        // TODO: Load and compile source files
        // TODO: Initialize runtime
        // TODO: Start server if port is configured

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stop the engine.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_running)
            return;

        _logger.LogInformation("Stopping IntMUD engine...");
        _running = false;

        // TODO: Cleanup resources

        await Task.CompletedTask;
    }

    /// <summary>
    /// Execute one tick of the engine.
    /// </summary>
    public void Tick()
    {
        if (!_running)
            return;

        // TODO: Process pending events
        // TODO: Execute instructions
        // TODO: Process timers
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
    }
}

/// <summary>
/// Hosted service for running the IntMUD engine.
/// </summary>
public sealed class IntMudHostedService : IHostedService, IDisposable
{
    private readonly IntMudEngine _engine;
    private readonly IntMudHostOptions _options;
    private readonly ILogger<IntMudHostedService> _logger;
    private Timer? _tickTimer;
    private bool _disposed;

    public IntMudHostedService(
        IntMudEngine engine,
        IntMudHostOptions options,
        ILogger<IntMudHostedService> logger)
    {
        _engine = engine;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _engine.StartAsync(cancellationToken);

        // Start tick timer
        _tickTimer = new Timer(
            _ => _engine.Tick(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(_options.TickIntervalMs));

        _logger.LogInformation("IntMUD hosted service started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _tickTimer?.Change(Timeout.Infinite, 0);
        await _engine.StopAsync(cancellationToken);
        _logger.LogInformation("IntMUD hosted service stopped");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _tickTimer?.Dispose();
    }
}
