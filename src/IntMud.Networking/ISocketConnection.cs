using System.Net;
using System.Net.Sockets;

namespace IntMud.Networking;

/// <summary>
/// Protocol type for socket connections.
/// </summary>
public enum SocketProtocol
{
    /// <summary>Raw TCP (no line processing)</summary>
    Raw,

    /// <summary>Telnet protocol (line-based with ANSI codes)</summary>
    Telnet,

    /// <summary>IRC protocol (line-based with mIRC colors)</summary>
    Irc,

    /// <summary>Papovox binary protocol</summary>
    Papovox,

    /// <summary>WebSocket protocol</summary>
    WebSocket,

    /// <summary>Hexadecimal mode (raw bytes as hex)</summary>
    Hex
}

/// <summary>
/// Connection state for sockets.
/// </summary>
public enum ConnectionState
{
    /// <summary>Not connected</summary>
    Disconnected,

    /// <summary>Connecting to remote host</summary>
    Connecting,

    /// <summary>Connected and ready</summary>
    Connected,

    /// <summary>SSL handshake in progress</summary>
    SslHandshake,

    /// <summary>Connection closing</summary>
    Closing,

    /// <summary>Connection failed</summary>
    Failed
}

/// <summary>
/// Interface for socket connections.
/// </summary>
public interface ISocketConnection : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Unique identifier for this connection.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// Protocol used by this connection.
    /// </summary>
    SocketProtocol Protocol { get; set; }

    /// <summary>
    /// Remote endpoint address.
    /// </summary>
    string? RemoteAddress { get; }

    /// <summary>
    /// Remote endpoint port.
    /// </summary>
    int RemotePort { get; }

    /// <summary>
    /// Local endpoint address.
    /// </summary>
    string? LocalAddress { get; }

    /// <summary>
    /// Local endpoint port.
    /// </summary>
    int LocalPort { get; }

    /// <summary>
    /// Whether the connection is using SSL/TLS.
    /// </summary>
    bool IsSecure { get; }

    /// <summary>
    /// Total bytes received.
    /// </summary>
    long BytesReceived { get; }

    /// <summary>
    /// Total bytes sent.
    /// </summary>
    long BytesSent { get; }

    /// <summary>
    /// Time when connection was established.
    /// </summary>
    DateTime? ConnectedAt { get; }

    /// <summary>
    /// Connect to a remote host.
    /// </summary>
    Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start SSL/TLS handshake.
    /// </summary>
    Task<bool> StartSslAsync(string? targetHost = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send data to the remote host.
    /// </summary>
    Task<bool> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send text to the remote host.
    /// </summary>
    Task<bool> SendAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a line of text (appends line ending).
    /// </summary>
    Task<bool> SendLineAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive data from the remote host.
    /// </summary>
    Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive a line of text.
    /// </summary>
    Task<string?> ReceiveLineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if data is available to read.
    /// </summary>
    bool DataAvailable { get; }

    /// <summary>
    /// Close the connection.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Event raised when data is received.
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when connection is closed.
    /// </summary>
    event EventHandler<ConnectionClosedEventArgs>? Closed;
}

/// <summary>
/// Event args for data received event.
/// </summary>
public class DataReceivedEventArgs : EventArgs
{
    public required ISocketConnection Connection { get; init; }
    public required byte[] Data { get; init; }
    public string? Text { get; init; }
}

/// <summary>
/// Event args for connection state changed event.
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    public required ISocketConnection Connection { get; init; }
    public required ConnectionState OldState { get; init; }
    public required ConnectionState NewState { get; init; }
}

/// <summary>
/// Event args for connection closed event.
/// </summary>
public class ConnectionClosedEventArgs : EventArgs
{
    public required ISocketConnection Connection { get; init; }
    public string? Reason { get; init; }
    public Exception? Exception { get; init; }
}
