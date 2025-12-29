using System.Net.WebSockets;
using System.Text;

namespace IntMud.Networking;

/// <summary>
/// WebSocket connection implementation.
/// </summary>
public sealed class WebSocketConnection : ISocketConnection
{
    private static int _nextId;

    private readonly ClientWebSocket _webSocket;
    private readonly StringBuilder _lineBuffer = new();
    private readonly byte[] _receiveBuffer = new byte[8192];
    private readonly object _stateLock = new();

    private ConnectionState _state = ConnectionState.Disconnected;
    private long _bytesReceived;
    private long _bytesSent;
    private bool _disposed;
    private Uri? _uri;

    public WebSocketConnection()
    {
        Id = Interlocked.Increment(ref _nextId);
        _webSocket = new ClientWebSocket();
    }

    public int Id { get; }

    public ConnectionState State
    {
        get { lock (_stateLock) return _state; }
        private set { lock (_stateLock) _state = value; }
    }

    public SocketProtocol Protocol
    {
        get => SocketProtocol.WebSocket;
        set { } // WebSocket only
    }

    public string? RemoteAddress => _uri?.Host;

    public int RemotePort => _uri?.Port ?? 0;

    public string? LocalAddress => null;

    public int LocalPort => 0;

    public bool IsSecure => _uri?.Scheme == "wss";

    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    public long BytesSent => Interlocked.Read(ref _bytesSent);

    public DateTime? ConnectedAt { get; private set; }

    public bool DataAvailable => _webSocket.State == WebSocketState.Open;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;
    public event EventHandler<ConnectionClosedEventArgs>? Closed;

    public async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Disconnected)
            return false;

        try
        {
            SetState(ConnectionState.Connecting);

            // Determine scheme based on port
            var scheme = port == 443 ? "wss" : "ws";
            _uri = new Uri($"{scheme}://{host}:{port}");

            await _webSocket.ConnectAsync(_uri, cancellationToken);

            ConnectedAt = DateTime.UtcNow;
            SetState(ConnectionState.Connected);
            return true;
        }
        catch
        {
            SetState(ConnectionState.Failed);
            return false;
        }
    }

    /// <summary>
    /// Connect using a WebSocket URI.
    /// </summary>
    public async Task<bool> ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Disconnected)
            return false;

        try
        {
            SetState(ConnectionState.Connecting);
            _uri = uri;

            await _webSocket.ConnectAsync(uri, cancellationToken);

            ConnectedAt = DateTime.UtcNow;
            SetState(ConnectionState.Connected);
            return true;
        }
        catch
        {
            SetState(ConnectionState.Failed);
            return false;
        }
    }

    public Task<bool> StartSslAsync(string? targetHost = null, CancellationToken cancellationToken = default)
    {
        // WebSocket SSL is handled by the URI scheme (wss://)
        return Task.FromResult(IsSecure);
    }

    public async Task<bool> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (_webSocket.State != WebSocketState.Open)
            return false;

        try
        {
            await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
            Interlocked.Add(ref _bytesSent, data.Length);
            return true;
        }
        catch
        {
            await CloseAsync();
            return false;
        }
    }

    public async Task<bool> SendAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_webSocket.State != WebSocketState.Open)
            return false;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            Interlocked.Add(ref _bytesSent, bytes.Length);
            return true;
        }
        catch
        {
            await CloseAsync();
            return false;
        }
    }

    public Task<bool> SendLineAsync(string text, CancellationToken cancellationToken = default)
    {
        return SendAsync(text + "\n", cancellationToken);
    }

    public async Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket.State != WebSocketState.Open)
            return null;

        try
        {
            var result = await _webSocket.ReceiveAsync(_receiveBuffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await CloseAsync();
                return null;
            }

            Interlocked.Add(ref _bytesReceived, result.Count);

            var data = new byte[result.Count];
            Array.Copy(_receiveBuffer, data, result.Count);

            DataReceived?.Invoke(this, new DataReceivedEventArgs
            {
                Connection = this,
                Data = data,
                Text = Encoding.UTF8.GetString(data)
            });

            return data;
        }
        catch
        {
            await CloseAsync();
            return null;
        }
    }

    public async Task<string?> ReceiveLineAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket.State != WebSocketState.Open)
            return null;

        try
        {
            while (true)
            {
                // Check for complete line in buffer
                var content = _lineBuffer.ToString();
                var lineEnd = content.IndexOfAny(['\r', '\n']);
                if (lineEnd >= 0)
                {
                    var line = content[..lineEnd];
                    var skip = lineEnd + 1;
                    if (skip < content.Length && content[lineEnd] == '\r' && content[skip] == '\n')
                        skip++;
                    _lineBuffer.Remove(0, skip);
                    return line;
                }

                // Receive more data
                var result = await _webSocket.ReceiveAsync(_receiveBuffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseAsync();
                    return _lineBuffer.Length > 0 ? _lineBuffer.ToString() : null;
                }

                Interlocked.Add(ref _bytesReceived, result.Count);
                _lineBuffer.Append(Encoding.UTF8.GetString(_receiveBuffer, 0, result.Count));
            }
        }
        catch
        {
            await CloseAsync();
            return null;
        }
    }

    public async Task CloseAsync()
    {
        if (State == ConnectionState.Disconnected || State == ConnectionState.Closing)
            return;

        SetState(ConnectionState.Closing);

        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch { }

        SetState(ConnectionState.Disconnected);

        Closed?.Invoke(this, new ConnectionClosedEventArgs
        {
            Connection = this
        });
    }

    private void SetState(ConnectionState newState)
    {
        ConnectionState oldState;
        lock (_stateLock)
        {
            if (_state == newState)
                return;
            oldState = _state;
            _state = newState;
        }

        StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
        {
            Connection = this,
            OldState = oldState,
            NewState = newState
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseAsync().GetAwaiter().GetResult();
        _webSocket.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await CloseAsync();
        _webSocket.Dispose();
    }
}
