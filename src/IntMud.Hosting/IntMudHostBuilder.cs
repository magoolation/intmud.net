using System.Diagnostics;
using IntMud.Compiler.Bytecode;
using IntMud.Compiler.Parsing;
using IntMud.Networking;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;
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
        _builder.Services.AddSingleton<ConnectionManager>();
        _builder.Services.AddSingleton<SessionManager>();
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
    public int ServerPort { get; set; } = 4000;

    /// <summary>
    /// Server bind address.
    /// </summary>
    public string ServerAddress { get; set; } = "0.0.0.0";

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

    /// <summary>
    /// Welcome message for new connections.
    /// </summary>
    public string WelcomeMessage { get; set; } = @"
╔═══════════════════════════════════════════════════╗
║           Welcome to IntMUD Server                ║
║         Powered by IntMUD.NET Runtime             ║
╚═══════════════════════════════════════════════════╝

Type 'help' for available commands.
";

    /// <summary>
    /// Command handler for processing player input.
    /// </summary>
    public Func<PlayerSession, string, Task>? CommandHandler { get; set; }
}

/// <summary>
/// The IntMUD execution engine.
/// </summary>
public sealed class IntMudEngine : IDisposable
{
    private readonly IntMudHostOptions _options;
    private readonly ConnectionManager _connectionManager;
    private readonly SessionManager _sessionManager;
    private readonly ILogger<IntMudEngine> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly Dictionary<string, CompiledUnit> _compiledUnits = new(StringComparer.OrdinalIgnoreCase);
    private SocketListener? _listener;
    private FileSystemWatcher? _fileWatcher;
    private ScriptEventHandler? _eventHandler;
    private bool _running;
    private bool _disposed;
    private DateTime _lastReload = DateTime.MinValue;

    public IntMudEngine(
        IntMudHostOptions options,
        ConnectionManager connectionManager,
        SessionManager sessionManager,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _connectionManager = connectionManager;
        _sessionManager = sessionManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<IntMudEngine>();
    }

    /// <summary>
    /// Whether the engine is running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// The session manager.
    /// </summary>
    public SessionManager Sessions => _sessionManager;

    /// <summary>
    /// Get compiled units.
    /// </summary>
    public IReadOnlyDictionary<string, CompiledUnit> CompiledUnits => _compiledUnits;

    /// <summary>
    /// Script event handler.
    /// </summary>
    public ScriptEventHandler? EventHandler => _eventHandler;

    /// <summary>
    /// Start the engine.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
            return;

        _logger.LogInformation("Starting IntMUD engine...");
        _logger.LogInformation("Source path: {SourcePath}", _options.SourcePath);

        // Load and compile source files
        await LoadSourceFilesAsync(cancellationToken);

        // Initialize event handler
        _eventHandler = new ScriptEventHandler(
            _loggerFactory.CreateLogger<ScriptEventHandler>(),
            _compiledUnits);
        _eventHandler.SetMainClass("main");

        // Start hot-reload if enabled
        if (_options.EnableHotReload)
        {
            StartHotReload();
        }

        // Start TCP server
        if (_options.ServerPort > 0)
        {
            StartServer();
        }

        _running = true;
        _logger.LogInformation("IntMUD engine started");
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

        // Stop file watcher
        _fileWatcher?.Dispose();
        _fileWatcher = null;

        // Disconnect all players
        await _sessionManager.DisconnectAllAsync("Server shutting down. Goodbye!");

        // Stop listener
        _listener?.Stop();
        _listener?.Dispose();
        _listener = null;

        // Cleanup connection manager
        await _connectionManager.CloseAllAsync();

        _logger.LogInformation("IntMUD engine stopped");
    }

    /// <summary>
    /// Reload all scripts from disk.
    /// </summary>
    public async Task ReloadScriptsAsync()
    {
        _logger.LogInformation("Reloading scripts...");
        _compiledUnits.Clear();
        await LoadSourceFilesAsync(CancellationToken.None);
        _eventHandler?.RefreshMainUnit();
        await _sessionManager.BroadcastAsync(AnsiColors.Colorize("\r\n[Server] Scripts reloaded.\r\n", AnsiColors.Yellow));
        _logger.LogInformation("Scripts reloaded. {Count} classes loaded.", _compiledUnits.Count);
    }

    private void StartHotReload()
    {
        var sourcePath = Path.GetFullPath(_options.SourcePath);
        if (!Directory.Exists(sourcePath))
            return;

        _fileWatcher = new FileSystemWatcher(sourcePath, "*.int")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += OnSourceFileChanged;
        _fileWatcher.Created += OnSourceFileChanged;
        _fileWatcher.Renamed += (s, e) => OnSourceFileChanged(s, e);

        _logger.LogInformation("Hot-reload enabled for {Path}", sourcePath);
    }

    private async void OnSourceFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce - ignore events within 1 second of last reload
        if ((DateTime.Now - _lastReload).TotalSeconds < 1)
            return;

        _lastReload = DateTime.Now;

        // Small delay to allow file to finish writing
        await Task.Delay(200);

        try
        {
            await ReloadScriptsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading scripts");
        }
    }

    /// <summary>
    /// Execute one tick of the engine.
    /// </summary>
    public async void Tick()
    {
        if (!_running)
            return;

        try
        {
            // Call script tick event
            if (_eventHandler != null)
            {
                await _eventHandler.OnTickAsync();
            }

            // Process input from all sessions
            foreach (var (session, input) in _sessionManager.CollectInput())
            {
                await ProcessInputAsync(session, input);
            }

            // Flush output to all sessions
            await _sessionManager.FlushAllOutputAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tick processing");
        }
    }

    private async Task LoadSourceFilesAsync(CancellationToken cancellationToken)
    {
        var sourcePath = Path.GetFullPath(_options.SourcePath);

        if (!Directory.Exists(sourcePath))
        {
            _logger.LogWarning("Source path does not exist: {SourcePath}", sourcePath);
            return;
        }

        var files = Directory.GetFiles(sourcePath, "*.int", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} source files", files.Length);

        var parser = new IntMudSourceParser();

        foreach (var file in files)
        {
            try
            {
                var source = await File.ReadAllTextAsync(file, cancellationToken);
                var ast = parser.Parse(source, file);

                if (ast.Classes.Count > 0)
                {
                    var unit = BytecodeCompiler.Compile(ast);
                    _compiledUnits[unit.ClassName] = unit;
                    _logger.LogDebug("Compiled class '{ClassName}' from {File}",
                        unit.ClassName, Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling {File}", file);
            }
        }

        _logger.LogInformation("Compiled {Count} classes", _compiledUnits.Count);
    }

    private void StartServer()
    {
        _listener = _connectionManager.CreateListener();

        if (!_listener.Listen(_options.ServerPort, _options.ServerAddress))
        {
            _logger.LogError("Failed to start listener on port {Port}", _options.ServerPort);
            return;
        }

        _logger.LogInformation("Server listening on {Address}:{Port}",
            _options.ServerAddress, _options.ServerPort);

        _listener.StartAccepting(OnConnectionAccepted);
    }

    private async void OnConnectionAccepted(ISocketConnection connection)
    {
        _logger.LogInformation("New connection from {RemoteAddress}:{RemotePort}",
            connection.RemoteAddress, connection.RemotePort);

        var session = _sessionManager.CreateSession(connection);

        // Send welcome message with ANSI color support BEFORE starting to receive
        await session.SendAsync(AnsiColors.ParseColorCodes(_options.WelcomeMessage));

        // Call script connect event
        var handled = false;
        if (_eventHandler != null)
        {
            handled = await _eventHandler.OnConnectAsync(session);
        }

        // Show prompt if script didn't handle it
        if (!handled)
        {
            await session.SendAsync("\r\n> ");
        }

        // Start receiving input AFTER welcome and connect events are done
        session.StartReceiving();
    }

    private void OnSessionDisconnected(PlayerSession session)
    {
        // Call script disconnect event
        _eventHandler?.OnDisconnectAsync(session);
    }

    private async Task ProcessInputAsync(PlayerSession session, string input)
    {
        input = input.Trim();

        if (string.IsNullOrEmpty(input))
        {
            await session.SendAsync("> ");
            return;
        }

        _logger.LogDebug("Session {SessionId} input: {Input}", session.Id, input);

        // Parse command and args
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1] : "";

        // Try script event handler first
        if (_eventHandler != null)
        {
            var handled = await _eventHandler.OnCommandAsync(session, command, args);
            if (handled)
            {
                await session.SendAsync("\r\n> ");
                return;
            }
        }

        // Check for custom command handler
        if (_options.CommandHandler != null)
        {
            await _options.CommandHandler(session, input);
            await session.SendAsync("\r\n> ");
            return;
        }

        // Default command processing
        await ProcessDefaultCommandAsync(session, command, args);
        await session.SendAsync("\r\n> ");
    }

    private async Task ProcessDefaultCommandAsync(PlayerSession session, string command, string args)
    {
        switch (command)
        {
            case "help":
                await SendHelpAsync(session);
                break;

            case "who":
                await SendWhoListAsync(session);
                break;

            case "say":
                await BroadcastSayAsync(session, args);
                break;

            case "quit":
            case "exit":
                await session.DisconnectAsync("Goodbye!");
                break;

            case "exec":
                await ExecuteScriptAsync(session, args);
                break;

            case "classes":
                await ListClassesAsync(session);
                break;

            case "call":
                await CallFunctionAsync(session, args);
                break;

            case "stats":
                await SendStatsAsync(session);
                break;

            case "reload":
                await ReloadScriptsAsync();
                break;

            case "color":
            case "colors":
                await ShowColorDemoAsync(session);
                break;

            default:
                await session.SendLineAsync(AnsiColors.Colorize($"Unknown command: {command}. Type 'help' for available commands.", AnsiColors.Red));
                break;
        }
    }

    private async Task SendHelpAsync(PlayerSession session)
    {
        var help = $@"
{AnsiColors.BrightCyan}Available commands:{AnsiColors.Reset}
  {AnsiColors.Yellow}help{AnsiColors.Reset}              - Show this help message
  {AnsiColors.Yellow}who{AnsiColors.Reset}               - List connected players
  {AnsiColors.Yellow}say <message>{AnsiColors.Reset}     - Broadcast a message to all players
  {AnsiColors.Yellow}quit{AnsiColors.Reset}              - Disconnect from the server
  {AnsiColors.Yellow}exec <code>{AnsiColors.Reset}       - Execute IntMUD code
  {AnsiColors.Yellow}classes{AnsiColors.Reset}           - List loaded classes
  {AnsiColors.Yellow}call <class.func>{AnsiColors.Reset} - Call a function from a loaded class
  {AnsiColors.Yellow}stats{AnsiColors.Reset}             - Show server statistics
  {AnsiColors.Yellow}reload{AnsiColors.Reset}            - Reload all scripts from disk
  {AnsiColors.Yellow}colors{AnsiColors.Reset}            - Show color demo
";
        await session.SendLineAsync(help);
    }

    private async Task ShowColorDemoAsync(PlayerSession session)
    {
        var demo = $@"
{AnsiColors.BrightCyan}ANSI Color Demo:{AnsiColors.Reset}

{AnsiColors.Black}Black{AnsiColors.Reset} {AnsiColors.Red}Red{AnsiColors.Reset} {AnsiColors.Green}Green{AnsiColors.Reset} {AnsiColors.Yellow}Yellow{AnsiColors.Reset} {AnsiColors.Blue}Blue{AnsiColors.Reset} {AnsiColors.Magenta}Magenta{AnsiColors.Reset} {AnsiColors.Cyan}Cyan{AnsiColors.Reset} {AnsiColors.White}White{AnsiColors.Reset}

{AnsiColors.BrightBlack}BrightBlack{AnsiColors.Reset} {AnsiColors.BrightRed}BrightRed{AnsiColors.Reset} {AnsiColors.BrightGreen}BrightGreen{AnsiColors.Reset} {AnsiColors.BrightYellow}BrightYellow{AnsiColors.Reset}
{AnsiColors.BrightBlue}BrightBlue{AnsiColors.Reset} {AnsiColors.BrightMagenta}BrightMagenta{AnsiColors.Reset} {AnsiColors.BrightCyan}BrightCyan{AnsiColors.Reset} {AnsiColors.BrightWhite}BrightWhite{AnsiColors.Reset}

{AnsiColors.Bold}Bold{AnsiColors.Reset} {AnsiColors.Dim}Dim{AnsiColors.Reset} {AnsiColors.Italic}Italic{AnsiColors.Reset} {AnsiColors.Underline}Underline{AnsiColors.Reset} {AnsiColors.Reverse}Reverse{AnsiColors.Reset}

Use color codes in scripts: {{red}}, {{green}}, {{yellow}}, {{cyan}}, {{reset}}
";
        await session.SendLineAsync(demo);
    }

    private async Task SendWhoListAsync(PlayerSession session)
    {
        var sessions = _sessionManager.Sessions.ToList();
        await session.SendLineAsync($"\r\nConnected players ({sessions.Count}):");
        await session.SendLineAsync("─────────────────────────────────────");

        foreach (var s in sessions)
        {
            var name = s.PlayerName ?? $"Guest#{s.Id}";
            var status = s.State == SessionState.Playing ? "Playing" : s.State.ToString();
            var duration = DateTime.UtcNow - s.ConnectedAt;
            await session.SendLineAsync($"  {name,-20} {status,-12} {FormatDuration(duration)}");
        }
    }

    private async Task BroadcastSayAsync(PlayerSession session, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await session.SendLineAsync("Say what?");
            return;
        }

        var name = session.PlayerName ?? $"Guest#{session.Id}";
        var formattedMessage = $"\r\n{name} says: {message}";

        await _sessionManager.BroadcastAsync(formattedMessage);
    }

    private async Task ExecuteScriptAsync(PlayerSession session, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            await session.SendLineAsync("Usage: exec <code>");
            return;
        }

        try
        {
            // Wrap code in a simple class/function structure
            var fullCode = $@"
classe _repl

func executar
  {code}
";
            var parser = new IntMudSourceParser();
            var ast = parser.Parse(fullCode, "repl");
            var unit = BytecodeCompiler.Compile(ast);

            var interpreter = new BytecodeInterpreter(unit, _compiledUnits.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase));

            // Capture output
            interpreter.WriteOutput = text => session.QueueOutput(text);

            var result = interpreter.Execute("executar");

            // Send any captured output
            foreach (var output in interpreter.OutputBuffer)
            {
                await session.SendAsync(output);
            }

            // Send result if not null
            if (!result.IsNull)
            {
                await session.SendLineAsync($"Result: {result.AsString()}");
            }
        }
        catch (Exception ex)
        {
            await session.SendLineAsync($"Error: {ex.Message}");
        }
    }

    private async Task ListClassesAsync(PlayerSession session)
    {
        await session.SendLineAsync($"\r\nLoaded classes ({_compiledUnits.Count}):");
        await session.SendLineAsync("─────────────────────────────────────");

        foreach (var (name, unit) in _compiledUnits)
        {
            var funcCount = unit.Functions.Count;
            var constCount = unit.Constants.Count;
            await session.SendLineAsync($"  {name,-20} {funcCount} functions, {constCount} constants");
        }
    }

    private async Task CallFunctionAsync(PlayerSession session, string args)
    {
        // Format: class.function arg1 arg2 ...
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            await session.SendLineAsync("Usage: call <class.function> [args...]");
            return;
        }

        var funcPath = parts[0].Split('.');
        if (funcPath.Length != 2)
        {
            await session.SendLineAsync("Usage: call <class.function> [args...]");
            return;
        }

        var className = funcPath[0];
        var funcName = funcPath[1];

        if (!_compiledUnits.TryGetValue(className, out var unit))
        {
            await session.SendLineAsync($"Class '{className}' not found.");
            return;
        }

        if (!unit.Functions.ContainsKey(funcName))
        {
            await session.SendLineAsync($"Function '{funcName}' not found in class '{className}'.");
            return;
        }

        try
        {
            var interpreter = new BytecodeInterpreter(unit, _compiledUnits.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase));

            interpreter.WriteOutput = text => session.QueueOutput(text);

            // Parse arguments
            var funcArgs = parts.Skip(1)
                .Select(arg =>
                {
                    if (long.TryParse(arg, out var intVal))
                        return RuntimeValue.FromInt(intVal);
                    if (double.TryParse(arg, out var dblVal))
                        return RuntimeValue.FromDouble(dblVal);
                    return RuntimeValue.FromString(arg);
                })
                .ToArray();

            var result = interpreter.Execute(funcName, funcArgs);

            // Send any captured output
            foreach (var output in interpreter.OutputBuffer)
            {
                await session.SendAsync(output);
            }

            if (!result.IsNull)
            {
                await session.SendLineAsync($"Result: {result.AsString()}");
            }
        }
        catch (Exception ex)
        {
            await session.SendLineAsync($"Error: {ex.Message}");
        }
    }

    private async Task SendStatsAsync(PlayerSession session)
    {
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

        await session.SendLineAsync("\r\nServer Statistics:");
        await session.SendLineAsync("─────────────────────────────────────");
        await session.SendLineAsync($"  Uptime:            {FormatDuration(uptime)}");
        await session.SendLineAsync($"  Connected players: {_sessionManager.SessionCount}");
        await session.SendLineAsync($"  Loaded classes:    {_compiledUnits.Count}");
        await session.SendLineAsync($"  Server port:       {_options.ServerPort}");
        await session.SendLineAsync($"  Memory usage:      {GC.GetTotalMemory(false) / 1024 / 1024} MB");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{(int)duration.TotalSeconds}s";
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
