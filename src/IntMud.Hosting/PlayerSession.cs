using IntMud.Compiler.Bytecode;
using IntMud.Networking;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;
using Microsoft.Extensions.Logging;

namespace IntMud.Hosting;

/// <summary>
/// State of a player session.
/// </summary>
public enum SessionState
{
    /// <summary>Just connected, awaiting login</summary>
    Connected,

    /// <summary>Login in progress</summary>
    Authenticating,

    /// <summary>Logged in and playing</summary>
    Playing,

    /// <summary>Disconnecting</summary>
    Disconnecting,

    /// <summary>Disconnected</summary>
    Disconnected
}

/// <summary>
/// Represents a connected player session.
/// </summary>
public sealed class PlayerSession : IAsyncDisposable
{
    private readonly ISocketConnection _connection;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Queue<string> _inputQueue = new();
    private readonly Queue<string> _outputQueue = new();
    private readonly object _inputLock = new();
    private readonly object _outputLock = new();

    private SessionState _state = SessionState.Connected;
    private Task? _receiveTask;
    private BytecodeInterpreter? _interpreter;
    private bool _disposed;

    public PlayerSession(ISocketConnection connection, ILogger logger)
    {
        _connection = connection;
        _logger = logger;
        Id = connection.Id;
    }

    /// <summary>
    /// Unique session identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Player name (after login).
    /// </summary>
    public string? PlayerName { get; set; }

    /// <summary>
    /// Current session state.
    /// </summary>
    public SessionState State
    {
        get => _state;
        set => _state = value;
    }

    /// <summary>
    /// Remote IP address.
    /// </summary>
    public string? RemoteAddress => _connection.RemoteAddress;

    /// <summary>
    /// Time when the session started.
    /// </summary>
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity time.
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// The underlying socket connection.
    /// </summary>
    public ISocketConnection Connection => _connection;

    /// <summary>
    /// The bytecode interpreter for this session's scripts.
    /// </summary>
    public BytecodeInterpreter? Interpreter
    {
        get => _interpreter;
        set => _interpreter = value;
    }

    /// <summary>
    /// Custom data attached to this session.
    /// </summary>
    public Dictionary<string, object> Data { get; } = new();

    /// <summary>
    /// Event raised when a line of input is received.
    /// </summary>
    public event EventHandler<string>? InputReceived;

    /// <summary>
    /// Event raised when the session disconnects.
    /// </summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// Start receiving input from the connection.
    /// </summary>
    public void StartReceiving()
    {
        _receiveTask = Task.Run(ReceiveLoopAsync);
    }

    /// <summary>
    /// Send text to the player.
    /// </summary>
    public async Task SendAsync(string text)
    {
        if (_connection.State != ConnectionState.Connected)
            return;

        try
        {
            await _connection.SendAsync(text);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending to session {SessionId}", Id);
        }
    }

    /// <summary>
    /// Send a line of text to the player.
    /// </summary>
    public async Task SendLineAsync(string text)
    {
        if (_connection.State != ConnectionState.Connected)
            return;

        try
        {
            await _connection.SendLineAsync(text);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending line to session {SessionId}", Id);
        }
    }

    /// <summary>
    /// Queue output to be sent.
    /// </summary>
    public void QueueOutput(string text)
    {
        lock (_outputLock)
        {
            _outputQueue.Enqueue(text);
        }
    }

    /// <summary>
    /// Flush queued output.
    /// </summary>
    public async Task FlushOutputAsync()
    {
        List<string> toSend;
        lock (_outputLock)
        {
            if (_outputQueue.Count == 0)
                return;

            toSend = _outputQueue.ToList();
            _outputQueue.Clear();
        }

        foreach (var text in toSend)
        {
            await SendAsync(text);
        }
    }

    /// <summary>
    /// Check if there's pending input.
    /// </summary>
    public bool HasInput
    {
        get
        {
            lock (_inputLock)
            {
                return _inputQueue.Count > 0;
            }
        }
    }

    /// <summary>
    /// Get the next line of input (or null if none).
    /// </summary>
    public string? GetNextInput()
    {
        lock (_inputLock)
        {
            return _inputQueue.Count > 0 ? _inputQueue.Dequeue() : null;
        }
    }

    /// <summary>
    /// Disconnect the session.
    /// </summary>
    public async Task DisconnectAsync(string? reason = null)
    {
        if (_state == SessionState.Disconnected || _state == SessionState.Disconnecting)
            return;

        _state = SessionState.Disconnecting;

        if (!string.IsNullOrEmpty(reason))
        {
            try
            {
                await SendLineAsync(reason);
            }
            catch { }
        }

        _cts.Cancel();
        await _connection.CloseAsync();
        _state = SessionState.Disconnected;

        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested && _connection.State == ConnectionState.Connected)
            {
                var line = await _connection.ReceiveLineAsync(_cts.Token);
                if (line == null)
                {
                    // Connection closed
                    break;
                }

                LastActivity = DateTime.UtcNow;

                // Strip telnet control codes if present
                line = StripTelnetCodes(line);

                lock (_inputLock)
                {
                    _inputQueue.Enqueue(line);
                }

                InputReceived?.Invoke(this, line);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Receive loop error for session {SessionId}", Id);
        }
        finally
        {
            if (_state != SessionState.Disconnected)
            {
                _state = SessionState.Disconnected;
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private static string StripTelnetCodes(string input)
    {
        // Simple telnet IAC stripping
        var result = new System.Text.StringBuilder();
        var i = 0;
        while (i < input.Length)
        {
            var c = input[i];
            if (c == '\xff' && i + 1 < input.Length)
            {
                // IAC - skip command
                var cmd = input[i + 1];
                if (cmd >= '\xfb' && cmd <= '\xfe' && i + 2 < input.Length)
                {
                    // WILL/WONT/DO/DONT - skip 3 bytes
                    i += 3;
                }
                else if (cmd == '\xfa')
                {
                    // Subnegotiation - skip until SE
                    i += 2;
                    while (i < input.Length && input[i] != '\xf0')
                        i++;
                    i++;
                }
                else
                {
                    // Other command - skip 2 bytes
                    i += 2;
                }
            }
            else if (c >= ' ' || c == '\t')
            {
                result.Append(c);
                i++;
            }
            else
            {
                i++;
            }
        }
        return result.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        await _connection.DisposeAsync();
        _cts.Dispose();
    }
}
