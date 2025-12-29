using System.Net;
using System.Net.Sockets;

namespace IntMud.Networking;

/// <summary>
/// TCP socket listener for accepting incoming connections.
/// </summary>
public sealed class SocketListener : IAsyncDisposable, IDisposable
{
    private static int _nextId;

    private readonly Socket _socket;
    private readonly object _stateLock = new();
    private bool _listening;
    private bool _disposed;
    private CancellationTokenSource? _acceptCts;

    public SocketListener()
    {
        Id = Interlocked.Increment(ref _nextId);
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    }

    /// <summary>
    /// Unique identifier for this listener.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Whether the listener is currently accepting connections.
    /// </summary>
    public bool IsListening
    {
        get { lock (_stateLock) return _listening; }
        private set { lock (_stateLock) _listening = value; }
    }

    /// <summary>
    /// Port the listener is bound to.
    /// </summary>
    public int Port
    {
        get
        {
            try
            {
                return (_socket.LocalEndPoint as IPEndPoint)?.Port ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Address the listener is bound to.
    /// </summary>
    public string? Address
    {
        get
        {
            try
            {
                return (_socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Event raised when a new connection is accepted.
    /// </summary>
    public event EventHandler<ConnectionAcceptedEventArgs>? ConnectionAccepted;

    /// <summary>
    /// Event raised when an error occurs.
    /// </summary>
    public event EventHandler<ListenerErrorEventArgs>? Error;

    /// <summary>
    /// Start listening on the specified port.
    /// </summary>
    public bool Listen(int port, string? address = null, int backlog = 100)
    {
        if (IsListening)
            return false;

        try
        {
            var ipAddress = string.IsNullOrEmpty(address)
                ? IPAddress.Any
                : IPAddress.Parse(address);

            _socket.Bind(new IPEndPoint(ipAddress, port));
            _socket.Listen(backlog);
            IsListening = true;
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, new ListenerErrorEventArgs
            {
                Listener = this,
                Exception = ex,
                Message = $"Failed to listen on port {port}: {ex.Message}"
            });
            return false;
        }
    }

    /// <summary>
    /// Accept a single incoming connection.
    /// </summary>
    public async Task<ISocketConnection?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        if (!IsListening)
            return null;

        try
        {
            var clientSocket = await _socket.AcceptAsync(cancellationToken);
            var connection = new TcpSocketConnection(clientSocket);

            ConnectionAccepted?.Invoke(this, new ConnectionAcceptedEventArgs
            {
                Listener = this,
                Connection = connection
            });

            return connection;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, new ListenerErrorEventArgs
            {
                Listener = this,
                Exception = ex,
                Message = $"Error accepting connection: {ex.Message}"
            });
            return null;
        }
    }

    /// <summary>
    /// Start accepting connections in a background loop.
    /// </summary>
    public void StartAccepting(Action<ISocketConnection> onAccept)
    {
        if (!IsListening)
            throw new InvalidOperationException("Listener is not listening");

        _acceptCts = new CancellationTokenSource();
        var token = _acceptCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && IsListening)
            {
                try
                {
                    var connection = await AcceptAsync(token);
                    if (connection != null)
                    {
                        onAccept(connection);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Continue accepting despite errors
                }
            }
        }, token);
    }

    /// <summary>
    /// Stop accepting connections.
    /// </summary>
    public void StopAccepting()
    {
        _acceptCts?.Cancel();
        _acceptCts?.Dispose();
        _acceptCts = null;
    }

    /// <summary>
    /// Stop listening and close the socket.
    /// </summary>
    public void Stop()
    {
        StopAccepting();

        try
        {
            _socket.Close();
        }
        catch { }

        IsListening = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
        _socket.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Event args for connection accepted event.
/// </summary>
public class ConnectionAcceptedEventArgs : EventArgs
{
    public required SocketListener Listener { get; init; }
    public required ISocketConnection Connection { get; init; }
}

/// <summary>
/// Event args for listener error event.
/// </summary>
public class ListenerErrorEventArgs : EventArgs
{
    public required SocketListener Listener { get; init; }
    public required Exception Exception { get; init; }
    public required string Message { get; init; }
}
