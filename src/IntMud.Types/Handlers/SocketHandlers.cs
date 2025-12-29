using System.Runtime.InteropServices;
using System.Text;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for socket variables.
/// Stores a reference to a socket connection object.
/// </summary>
public sealed class SocketHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Socket;
    public override string TypeName => "socket";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var socket = GetSocket(memory);
        return socket?.IsConnected ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var socket = GetSocket(memory);
        return socket?.Id ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var socket = GetSocket(memory);
        if (socket == null)
            return "nulo";
        return socket.IsConnected
            ? $"<socket:{socket.Id}:{socket.RemoteAddress}:{socket.RemotePort}>"
            : $"<socket:{socket.Id}:disconnected>";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }
    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        RefHandler.SetPointer(dest, RefHandler.GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetInt(left).CompareTo(GetInt(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    public override void Destroy(Span<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr != IntPtr.Zero)
        {
            var handle = GCHandle.FromIntPtr(ptr);
            if (handle.IsAllocated)
            {
                if (handle.Target is SocketInfo socket)
                {
                    socket.Dispose();
                }
                handle.Free();
            }
        }
        memory.Clear();
    }

    public static SocketInfo? GetSocket(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as SocketInfo;
    }

    public static void SetSocket(Span<byte> memory, SocketInfo socket)
    {
        // Free old handle if exists
        var oldPtr = RefHandler.GetPointer(memory);
        if (oldPtr != IntPtr.Zero)
        {
            var oldHandle = GCHandle.FromIntPtr(oldPtr);
            if (oldHandle.IsAllocated)
                oldHandle.Free();
        }

        // Allocate new handle
        var handle = GCHandle.Alloc(socket);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var socket = GetSocket(memory);

        switch (functionName.ToLowerInvariant())
        {
            case "criar":
            case "create":
                var newSocket = new SocketInfo();
                SetSocket(memory, newSocket);
                return true;

            case "conectar":
            case "connect":
                if (socket == null)
                {
                    socket = new SocketInfo();
                    SetSocket(memory, socket);
                }
                var host = context.GetStringArgument(0);
                var port = context.GetIntArgument(1);
                _ = socket.ConnectAsync(host, port);
                return true;

            case "fechar":
            case "close":
                socket?.Close();
                return true;

            case "enviar":
            case "send":
                if (socket != null)
                {
                    var text = context.GetStringArgument(0);
                    _ = socket.SendAsync(text);
                }
                return true;

            case "linha":
            case "sendline":
                if (socket != null)
                {
                    var text = context.GetStringArgument(0);
                    _ = socket.SendLineAsync(text);
                }
                return true;

            case "receber":
            case "receive":
                context.SetReturnString(socket?.ReceiveLine() ?? "");
                return true;

            case "conectado":
            case "connected":
                context.SetReturnBool(socket?.IsConnected ?? false);
                return true;

            case "endereco":
            case "address":
                context.SetReturnString(socket?.RemoteAddress ?? "");
                return true;

            case "porta":
            case "port":
                context.SetReturnInt(socket?.RemotePort ?? 0);
                return true;

            case "protocolo":
            case "protocol":
                if (context.ArgumentCount > 0)
                {
                    var proto = context.GetStringArgument(0).ToLowerInvariant();
                    if (socket != null)
                    {
                        socket.Protocol = proto switch
                        {
                            "telnet" => SocketProtocolType.Telnet,
                            "irc" => SocketProtocolType.Irc,
                            "raw" => SocketProtocolType.Raw,
                            "hex" => SocketProtocolType.Hex,
                            _ => socket.Protocol
                        };
                    }
                }
                else
                {
                    context.SetReturnString(socket?.Protocol.ToString().ToLowerInvariant() ?? "");
                }
                return true;

            case "ssl":
                if (socket != null)
                {
                    _ = socket.StartSslAsync();
                }
                return true;

            case "dados":
            case "available":
                context.SetReturnBool(socket?.HasData ?? false);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for server (listener) variables.
/// </summary>
public sealed class ServerHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.Serv;
    public override string TypeName => "server";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var server = GetServer(memory);
        return server?.IsListening ?? false;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var server = GetServer(memory);
        return server?.Port ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var server = GetServer(memory);
        if (server == null)
            return "nulo";
        return server.IsListening
            ? $"<server:{server.Port}:listening>"
            : "<server:stopped>";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }
    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        RefHandler.SetPointer(dest, RefHandler.GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetInt(left).CompareTo(GetInt(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    public override void Destroy(Span<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr != IntPtr.Zero)
        {
            var handle = GCHandle.FromIntPtr(ptr);
            if (handle.IsAllocated)
            {
                if (handle.Target is ServerInfo server)
                {
                    server.Dispose();
                }
                handle.Free();
            }
        }
        memory.Clear();
    }

    public static ServerInfo? GetServer(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as ServerInfo;
    }

    public static void SetServer(Span<byte> memory, ServerInfo server)
    {
        var oldPtr = RefHandler.GetPointer(memory);
        if (oldPtr != IntPtr.Zero)
        {
            var oldHandle = GCHandle.FromIntPtr(oldPtr);
            if (oldHandle.IsAllocated)
                oldHandle.Free();
        }

        var handle = GCHandle.Alloc(server);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var server = GetServer(memory);

        switch (functionName.ToLowerInvariant())
        {
            case "criar":
            case "create":
                var newServer = new ServerInfo();
                SetServer(memory, newServer);
                return true;

            case "escutar":
            case "listen":
                if (server == null)
                {
                    server = new ServerInfo();
                    SetServer(memory, server);
                }
                var port = context.GetIntArgument(0);
                context.SetReturnBool(server.Listen(port));
                return true;

            case "aceitar":
            case "accept":
                if (server != null)
                {
                    var accepted = server.Accept();
                    context.SetReturnObject(accepted);
                }
                else
                {
                    context.SetReturnNull();
                }
                return true;

            case "fechar":
            case "close":
            case "parar":
            case "stop":
                server?.Stop();
                return true;

            case "escutando":
            case "listening":
                context.SetReturnBool(server?.IsListening ?? false);
                return true;

            case "porta":
            case "port":
                context.SetReturnInt(server?.Port ?? 0);
                return true;

            case "conexoes":
            case "connections":
                context.SetReturnInt(server?.ConnectionCount ?? 0);
                return true;

            default:
                return false;
        }
    }
}

// Helper classes for socket and server info

public enum SocketProtocolType
{
    Raw,
    Telnet,
    Irc,
    Hex,
    WebSocket
}

/// <summary>
/// Socket information wrapper for IntMUD.
/// </summary>
public sealed class SocketInfo : IDisposable
{
    private System.Net.Sockets.Socket? _socket;
    private System.Net.Sockets.NetworkStream? _stream;
    private System.Net.Security.SslStream? _sslStream;
    private Stream? _activeStream;
    private readonly StringBuilder _receiveBuffer = new();
    private readonly byte[] _buffer = new byte[8192];
    private bool _disposed;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;

    public SocketProtocolType Protocol { get; set; } = SocketProtocolType.Telnet;
    public bool IsConnected => _socket?.Connected ?? false;
    public bool HasData => _socket?.Available > 0;
    public string RemoteAddress => (_socket?.RemoteEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "";
    public int RemotePort => (_socket?.RemoteEndPoint as System.Net.IPEndPoint)?.Port ?? 0;

    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            Close();
            _socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);
            _socket.NoDelay = true;

            await _socket.ConnectAsync(host, port);
            _stream = new System.Net.Sockets.NetworkStream(_socket, ownsSocket: false);
            _activeStream = _stream;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StartSslAsync()
    {
        if (_stream == null)
            return false;

        try
        {
            _sslStream = new System.Net.Security.SslStream(_stream, leaveInnerStreamOpen: true);
            await _sslStream.AuthenticateAsClientAsync(RemoteAddress);
            _activeStream = _sslStream;
            return true;
        }
        catch
        {
            _sslStream?.Dispose();
            _sslStream = null;
            return false;
        }
    }

    public async Task<bool> SendAsync(string text)
    {
        if (_activeStream == null || !IsConnected)
            return false;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _activeStream.WriteAsync(bytes);
            await _activeStream.FlushAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendLineAsync(string text)
    {
        var lineEnding = Protocol switch
        {
            SocketProtocolType.Telnet => "\r\n",
            SocketProtocolType.Irc => "\r\n",
            _ => "\n"
        };
        return await SendAsync(text + lineEnding);
    }

    public string? ReceiveLine()
    {
        if (_activeStream == null || !IsConnected)
            return null;

        try
        {
            // Check for complete line in buffer
            var content = _receiveBuffer.ToString();
            var lineEnd = content.IndexOfAny(['\r', '\n']);
            if (lineEnd >= 0)
            {
                var line = content[..lineEnd];
                var skip = lineEnd + 1;
                if (skip < content.Length && content[lineEnd] == '\r' && content[skip] == '\n')
                    skip++;
                _receiveBuffer.Remove(0, skip);
                return line;
            }

            // Read more if available
            if (_socket?.Available > 0)
            {
                var read = _activeStream.Read(_buffer, 0, _buffer.Length);
                if (read > 0)
                {
                    _receiveBuffer.Append(Encoding.UTF8.GetString(_buffer, 0, read));
                    return ReceiveLine();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Close()
    {
        try
        {
            _sslStream?.Dispose();
            _sslStream = null;
            _stream?.Dispose();
            _stream = null;
            _socket?.Close();
            _socket?.Dispose();
            _socket = null;
            _activeStream = null;
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Close();
        }
    }
}

/// <summary>
/// Server information wrapper for IntMUD.
/// </summary>
public sealed class ServerInfo : IDisposable
{
    private System.Net.Sockets.Socket? _socket;
    private readonly List<SocketInfo> _connections = new();
    private bool _disposed;

    public int Port => (_socket?.LocalEndPoint as System.Net.IPEndPoint)?.Port ?? 0;
    public bool IsListening => _socket?.IsBound ?? false;
    public int ConnectionCount => _connections.Count;

    public bool Listen(int port)
    {
        try
        {
            Stop();
            _socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);
            _socket.SetSocketOption(
                System.Net.Sockets.SocketOptionLevel.Socket,
                System.Net.Sockets.SocketOptionName.ReuseAddress, true);
            _socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, port));
            _socket.Listen(100);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public SocketInfo? Accept()
    {
        if (_socket == null || !IsListening)
            return null;

        try
        {
            if (_socket.Poll(0, System.Net.Sockets.SelectMode.SelectRead))
            {
                var clientSocket = _socket.Accept();
                var socketInfo = new SocketInfo();
                // Note: This is a simplified implementation
                // In a full implementation, we'd need to properly transfer the socket
                _connections.Add(socketInfo);
                return socketInfo;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Stop()
    {
        try
        {
            foreach (var conn in _connections)
            {
                conn.Dispose();
            }
            _connections.Clear();
            _socket?.Close();
            _socket?.Dispose();
            _socket = null;
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
    }
}
