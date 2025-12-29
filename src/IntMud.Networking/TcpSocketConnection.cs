using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace IntMud.Networking;

/// <summary>
/// TCP socket connection implementation.
/// </summary>
public sealed class TcpSocketConnection : ISocketConnection
{
    private static int _nextId;

    private readonly Socket _socket;
    private Stream? _stream;
    private NetworkStream? _networkStream;
    private SslStream? _sslStream;
    private readonly StringBuilder _lineBuffer = new();
    private readonly byte[] _receiveBuffer = new byte[8192];
    private readonly object _stateLock = new();

    private ConnectionState _state = ConnectionState.Disconnected;
    private long _bytesReceived;
    private long _bytesSent;
    private bool _disposed;

    public TcpSocketConnection()
    {
        Id = Interlocked.Increment(ref _nextId);
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.NoDelay = true;
    }

    internal TcpSocketConnection(Socket acceptedSocket) : this()
    {
        _socket.Dispose();
        _socket = acceptedSocket;
        _socket.NoDelay = true;
        _networkStream = new NetworkStream(_socket, ownsSocket: false);
        _stream = _networkStream;
        SetState(ConnectionState.Connected);
    }

    public int Id { get; }

    public ConnectionState State
    {
        get { lock (_stateLock) return _state; }
        private set { lock (_stateLock) _state = value; }
    }

    public SocketProtocol Protocol { get; set; } = SocketProtocol.Telnet;

    public string? RemoteAddress
    {
        get
        {
            try
            {
                return (_socket.RemoteEndPoint as IPEndPoint)?.Address.ToString();
            }
            catch
            {
                return null;
            }
        }
    }

    public int RemotePort
    {
        get
        {
            try
            {
                return (_socket.RemoteEndPoint as IPEndPoint)?.Port ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public string? LocalAddress
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

    public int LocalPort
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

    public bool IsSecure => _sslStream != null;

    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    public long BytesSent => Interlocked.Read(ref _bytesSent);

    public DateTime? ConnectedAt { get; private set; }

    public bool DataAvailable
    {
        get
        {
            try
            {
                return _socket.Connected && _socket.Available > 0;
            }
            catch
            {
                return false;
            }
        }
    }

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

            // Resolve DNS if needed
            IPAddress? address;
            if (!IPAddress.TryParse(host, out address))
            {
                var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                       ?? addresses.FirstOrDefault();

                if (address == null)
                {
                    SetState(ConnectionState.Failed);
                    return false;
                }
            }

            await _socket.ConnectAsync(address, port, cancellationToken);

            _networkStream = new NetworkStream(_socket, ownsSocket: false);
            _stream = _networkStream;
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

    public async Task<bool> StartSslAsync(string? targetHost = null, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _networkStream == null)
            return false;

        if (_sslStream != null)
            return true; // Already using SSL

        try
        {
            SetState(ConnectionState.SslHandshake);

            _sslStream = new SslStream(_networkStream, leaveInnerStreamOpen: true);
            await _sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost ?? RemoteAddress,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                                          System.Security.Authentication.SslProtocols.Tls13
                },
                cancellationToken);

            _stream = _sslStream;
            SetState(ConnectionState.Connected);
            return true;
        }
        catch
        {
            _sslStream?.Dispose();
            _sslStream = null;
            SetState(ConnectionState.Connected); // Fallback to non-SSL
            return false;
        }
    }

    public async Task<bool> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _stream == null)
            return false;

        try
        {
            await _stream.WriteAsync(data, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            Interlocked.Add(ref _bytesSent, data.Length);
            return true;
        }
        catch
        {
            await CloseAsync();
            return false;
        }
    }

    public Task<bool> SendAsync(string text, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return SendAsync(bytes, cancellationToken);
    }

    public Task<bool> SendLineAsync(string text, CancellationToken cancellationToken = default)
    {
        var lineEnding = Protocol switch
        {
            SocketProtocol.Telnet => "\r\n",
            SocketProtocol.Irc => "\r\n",
            _ => "\n"
        };
        return SendAsync(text + lineEnding, cancellationToken);
    }

    public async Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _stream == null)
            return null;

        try
        {
            var bytesRead = await _stream.ReadAsync(_receiveBuffer, cancellationToken);
            if (bytesRead == 0)
            {
                await CloseAsync();
                return null;
            }

            Interlocked.Add(ref _bytesReceived, bytesRead);

            var result = new byte[bytesRead];
            Array.Copy(_receiveBuffer, result, bytesRead);

            // Raise event
            DataReceived?.Invoke(this, new DataReceivedEventArgs
            {
                Connection = this,
                Data = result,
                Text = Encoding.UTF8.GetString(result)
            });

            return result;
        }
        catch
        {
            await CloseAsync();
            return null;
        }
    }

    public async Task<string?> ReceiveLineAsync(CancellationToken cancellationToken = default)
    {
        if (State != ConnectionState.Connected || _stream == null)
            return null;

        try
        {
            while (true)
            {
                // Check if we have a complete line in the buffer
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

                // Read more data
                var bytesRead = await _stream.ReadAsync(_receiveBuffer, cancellationToken);
                if (bytesRead == 0)
                {
                    await CloseAsync();
                    return _lineBuffer.Length > 0 ? _lineBuffer.ToString() : null;
                }

                Interlocked.Add(ref _bytesReceived, bytesRead);
                _lineBuffer.Append(Encoding.UTF8.GetString(_receiveBuffer, 0, bytesRead));
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
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
        }
        catch { }

        _sslStream?.Dispose();
        _sslStream = null;

        _networkStream?.Dispose();
        _networkStream = null;

        _stream = null;

        try
        {
            _socket.Close();
        }
        catch { }

        SetState(ConnectionState.Disconnected);

        Closed?.Invoke(this, new ConnectionClosedEventArgs
        {
            Connection = this
        });

        await Task.CompletedTask;
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
        _socket.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await CloseAsync();
        _socket.Dispose();
    }
}
