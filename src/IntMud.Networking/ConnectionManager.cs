using System.Collections.Concurrent;

namespace IntMud.Networking;

/// <summary>
/// Manages all active socket connections.
/// </summary>
public sealed class ConnectionManager : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<int, ISocketConnection> _connections = new();
    private readonly ConcurrentDictionary<int, SocketListener> _listeners = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Number of active connections.
    /// </summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>
    /// Number of active listeners.
    /// </summary>
    public int ListenerCount => _listeners.Count;

    /// <summary>
    /// Get all active connections.
    /// </summary>
    public IEnumerable<ISocketConnection> Connections => _connections.Values;

    /// <summary>
    /// Get all active listeners.
    /// </summary>
    public IEnumerable<SocketListener> Listeners => _listeners.Values;

    /// <summary>
    /// Event raised when a connection is added.
    /// </summary>
    public event EventHandler<ISocketConnection>? ConnectionAdded;

    /// <summary>
    /// Event raised when a connection is removed.
    /// </summary>
    public event EventHandler<ISocketConnection>? ConnectionRemoved;

    /// <summary>
    /// Create a new TCP connection.
    /// </summary>
    public TcpSocketConnection CreateConnection()
    {
        var connection = new TcpSocketConnection();
        RegisterConnection(connection);
        return connection;
    }

    /// <summary>
    /// Create a new listener.
    /// </summary>
    public SocketListener CreateListener()
    {
        var listener = new SocketListener();
        RegisterListener(listener);
        return listener;
    }

    /// <summary>
    /// Register an existing connection.
    /// </summary>
    public void RegisterConnection(ISocketConnection connection)
    {
        if (_connections.TryAdd(connection.Id, connection))
        {
            connection.Closed += OnConnectionClosed;
            ConnectionAdded?.Invoke(this, connection);
        }
    }

    /// <summary>
    /// Register an existing listener.
    /// </summary>
    public void RegisterListener(SocketListener listener)
    {
        _listeners.TryAdd(listener.Id, listener);
        listener.ConnectionAccepted += OnConnectionAccepted;
    }

    /// <summary>
    /// Get a connection by ID.
    /// </summary>
    public ISocketConnection? GetConnection(int id)
    {
        return _connections.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get a listener by ID.
    /// </summary>
    public SocketListener? GetListener(int id)
    {
        return _listeners.GetValueOrDefault(id);
    }

    /// <summary>
    /// Remove a connection by ID.
    /// </summary>
    public async Task<bool> RemoveConnectionAsync(int id)
    {
        if (_connections.TryRemove(id, out var connection))
        {
            connection.Closed -= OnConnectionClosed;
            await connection.CloseAsync();
            ConnectionRemoved?.Invoke(this, connection);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Remove a listener by ID.
    /// </summary>
    public bool RemoveListener(int id)
    {
        if (_listeners.TryRemove(id, out var listener))
        {
            listener.ConnectionAccepted -= OnConnectionAccepted;
            listener.Stop();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Close all connections and listeners.
    /// </summary>
    public async Task CloseAllAsync()
    {
        // Close all listeners first
        foreach (var listener in _listeners.Values)
        {
            listener.ConnectionAccepted -= OnConnectionAccepted;
            listener.Stop();
        }
        _listeners.Clear();

        // Close all connections
        var closeTasks = _connections.Values.Select(async c =>
        {
            c.Closed -= OnConnectionClosed;
            await c.CloseAsync();
        });

        await Task.WhenAll(closeTasks);
        _connections.Clear();
    }

    /// <summary>
    /// Broadcast data to all connections.
    /// </summary>
    public async Task BroadcastAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var sendTasks = _connections.Values
            .Where(c => c.State == ConnectionState.Connected)
            .Select(c => c.SendAsync(data, cancellationToken));

        await Task.WhenAll(sendTasks);
    }

    /// <summary>
    /// Broadcast text to all connections.
    /// </summary>
    public async Task BroadcastAsync(string text, CancellationToken cancellationToken = default)
    {
        var sendTasks = _connections.Values
            .Where(c => c.State == ConnectionState.Connected)
            .Select(c => c.SendAsync(text, cancellationToken));

        await Task.WhenAll(sendTasks);
    }

    private void OnConnectionAccepted(object? sender, ConnectionAcceptedEventArgs e)
    {
        RegisterConnection(e.Connection);
    }

    private void OnConnectionClosed(object? sender, ConnectionClosedEventArgs e)
    {
        if (_connections.TryRemove(e.Connection.Id, out var connection))
        {
            connection.Closed -= OnConnectionClosed;
            ConnectionRemoved?.Invoke(this, connection);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseAllAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await CloseAllAsync();
    }
}
