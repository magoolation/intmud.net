using System.Collections.Concurrent;
using IntMud.Networking;
using Microsoft.Extensions.Logging;

namespace IntMud.Hosting;

/// <summary>
/// Manages all active player sessions.
/// </summary>
public sealed class SessionManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, PlayerSession> _sessions = new();
    private readonly ILogger<SessionManager> _logger;
    private bool _disposed;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Number of active sessions.
    /// </summary>
    public int SessionCount => _sessions.Count;

    /// <summary>
    /// Get all active sessions.
    /// </summary>
    public IEnumerable<PlayerSession> Sessions => _sessions.Values;

    /// <summary>
    /// Event raised when a session is added.
    /// </summary>
    public event EventHandler<PlayerSession>? SessionAdded;

    /// <summary>
    /// Event raised when a session is removed.
    /// </summary>
    public event EventHandler<PlayerSession>? SessionRemoved;

    /// <summary>
    /// Create a new session for an incoming connection.
    /// </summary>
    public PlayerSession CreateSession(ISocketConnection connection)
    {
        var session = new PlayerSession(connection, _logger);

        if (_sessions.TryAdd(session.Id, session))
        {
            session.Disconnected += OnSessionDisconnected;
            _logger.LogInformation("Session {SessionId} created from {RemoteAddress}",
                session.Id, session.RemoteAddress);
            SessionAdded?.Invoke(this, session);
        }

        return session;
    }

    /// <summary>
    /// Get a session by ID.
    /// </summary>
    public PlayerSession? GetSession(int id)
    {
        return _sessions.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get a session by player name.
    /// </summary>
    public PlayerSession? GetSessionByName(string playerName)
    {
        return _sessions.Values.FirstOrDefault(s =>
            string.Equals(s.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all sessions in a specific state.
    /// </summary>
    public IEnumerable<PlayerSession> GetSessionsByState(SessionState state)
    {
        return _sessions.Values.Where(s => s.State == state);
    }

    /// <summary>
    /// Remove a session.
    /// </summary>
    public async Task RemoveSessionAsync(int id)
    {
        if (_sessions.TryRemove(id, out var session))
        {
            session.Disconnected -= OnSessionDisconnected;
            _logger.LogInformation("Session {SessionId} removed", id);
            SessionRemoved?.Invoke(this, session);
            await session.DisposeAsync();
        }
    }

    /// <summary>
    /// Broadcast a message to all sessions.
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        var tasks = _sessions.Values
            .Where(s => s.State == SessionState.Playing)
            .Select(s => s.SendLineAsync(message));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Broadcast to all sessions except one.
    /// </summary>
    public async Task BroadcastExceptAsync(int exceptId, string message)
    {
        var tasks = _sessions.Values
            .Where(s => s.Id != exceptId && s.State == SessionState.Playing)
            .Select(s => s.SendLineAsync(message));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Process input from all sessions.
    /// </summary>
    public IEnumerable<(PlayerSession Session, string Input)> CollectInput()
    {
        foreach (var session in _sessions.Values)
        {
            while (session.HasInput)
            {
                var input = session.GetNextInput();
                if (input != null)
                {
                    yield return (session, input);
                }
            }
        }
    }

    /// <summary>
    /// Flush output for all sessions.
    /// </summary>
    public async Task FlushAllOutputAsync()
    {
        var tasks = _sessions.Values.Select(s => s.FlushOutputAsync());
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Disconnect all sessions.
    /// </summary>
    public async Task DisconnectAllAsync(string? reason = null)
    {
        var tasks = _sessions.Values.Select(s => s.DisconnectAsync(reason));
        await Task.WhenAll(tasks);
        _sessions.Clear();
    }

    private void OnSessionDisconnected(object? sender, EventArgs e)
    {
        if (sender is PlayerSession session)
        {
            if (_sessions.TryRemove(session.Id, out _))
            {
                session.Disconnected -= OnSessionDisconnected;
                _logger.LogInformation("Session {SessionId} disconnected", session.Id);
                SessionRemoved?.Invoke(this, session);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DisconnectAllAsync("Server shutting down.");
    }
}
