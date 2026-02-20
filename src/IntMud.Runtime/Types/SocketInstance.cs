using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a socket (TCP connection) instance.
/// Used for sending and receiving data over the network.
/// Events: {variableName}_msg(text), {variableName}_tecla(key), {variableName}_fechou(reason)
///         {variableName}_con(), {variableName}_err(error)
/// </summary>
public sealed class SocketInstance : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private SslStream? _sslStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _isOpen;
    private bool _useSsl;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private readonly StringBuilder _inputBuffer = new();

    /// <summary>
    /// The owner object that contains this socket variable.
    /// </summary>
    public BytecodeRuntimeObject? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Protocol type (0=none, 2=Telnet, 5=Papovox).
    /// </summary>
    public int Proto { get; set; } = 0;

    /// <summary>
    /// Anti-flood delay in milliseconds.
    /// </summary>
    public int AFlooder { get; set; } = 0;

    /// <summary>
    /// Color support (0=none, 1=ANSI).
    /// </summary>
    public int Cores { get; set; } = 0;

    /// <summary>
    /// Remote IP address.
    /// </summary>
    public string Ip => _client?.Client?.RemoteEndPoint is IPEndPoint ep ? ep.Address.ToString() : "";

    /// <summary>
    /// Local IP address.
    /// </summary>
    public string IpLocal => _client?.Client?.LocalEndPoint is IPEndPoint ep ? ep.Address.ToString() : "";

    /// <summary>
    /// Remote port.
    /// </summary>
    public int Porta => _client?.Client?.RemoteEndPoint is IPEndPoint ep ? ep.Port : 0;

    /// <summary>
    /// Check if socket is connected.
    /// </summary>
    public bool Valido => _isOpen && _client?.Connected == true;

    /// <summary>
    /// Event raised when a line of text is received.
    /// </summary>
    public event Action<string>? OnMessage;

    /// <summary>
    /// Event raised when a key is pressed (for telnet).
    /// </summary>
    public event Action<string>? OnTecla;

    /// <summary>
    /// Event raised when connection is closed.
    /// </summary>
    public event Action<string>? OnFechado;

    /// <summary>
    /// Event raised when connection is established.
    /// </summary>
    public event Action? OnConectado;

    /// <summary>
    /// Event raised on error.
    /// </summary>
    public event Action<string>? OnErro;

    /// <summary>
    /// Open connection to host and port.
    /// </summary>
    public bool Abrir(string host, int port)
    {
        try
        {
            Fechar();

            _client = new TcpClient();
            _client.Connect(host, port);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.Latin1);
            _writer = new StreamWriter(_stream, Encoding.Latin1) { AutoFlush = true };
            _isOpen = true;
            _useSsl = false;

            StartReading();
            OnConectado?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnErro?.Invoke(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Open SSL/TLS connection to host and port.
    /// </summary>
    public bool AbrirSsl(string host, int port)
    {
        try
        {
            Fechar();

            _client = new TcpClient();
            _client.Connect(host, port);
            _stream = _client.GetStream();

            _sslStream = new SslStream(_stream, false, ValidateServerCertificate);
            _sslStream.AuthenticateAsClient(host);

            _reader = new StreamReader(_sslStream, Encoding.Latin1);
            _writer = new StreamWriter(_sslStream, Encoding.Latin1) { AutoFlush = true };
            _isOpen = true;
            _useSsl = true;

            StartReading();
            OnConectado?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnErro?.Invoke(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Accept an incoming connection (called by ServInstance).
    /// </summary>
    internal void AcceptClient(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _reader = new StreamReader(_stream, Encoding.Latin1);
        _writer = new StreamWriter(_stream, Encoding.Latin1) { AutoFlush = true };
        _isOpen = true;
        _useSsl = false;

        StartReading();
    }

    /// <summary>
    /// Accept an incoming SSL connection (called by ServInstance).
    /// </summary>
    internal async Task AcceptSslAsync(TcpClient client, X509Certificate2 certificate)
    {
        _client = client;
        _stream = client.GetStream();

        _sslStream = new SslStream(_stream, false);
        await _sslStream.AuthenticateAsServerAsync(certificate);

        _reader = new StreamReader(_sslStream, Encoding.Latin1);
        _writer = new StreamWriter(_sslStream, Encoding.Latin1) { AutoFlush = true };
        _isOpen = true;
        _useSsl = true;

        StartReading();
    }

    /// <summary>
    /// Send a message with newline.
    /// </summary>
    public void Msg(string text)
    {
        try
        {
            _writer?.WriteLine(text);
        }
        catch (Exception ex)
        {
            OnErro?.Invoke(ex.Message);
            HandleDisconnect("send error");
        }
    }

    /// <summary>
    /// Send raw text without newline.
    /// </summary>
    public void MsgSem(string text)
    {
        try
        {
            _writer?.Write(text);
        }
        catch (Exception ex)
        {
            OnErro?.Invoke(ex.Message);
            HandleDisconnect("send error");
        }
    }

    /// <summary>
    /// Close the connection.
    /// </summary>
    public void Fechar()
    {
        if (_isOpen)
        {
            _isOpen = false;
            _cts?.Cancel();

            try
            {
                _writer?.Dispose();
                _reader?.Dispose();
                _sslStream?.Dispose();
                _stream?.Dispose();
                _client?.Dispose();
            }
            catch { }

            _writer = null;
            _reader = null;
            _sslStream = null;
            _stream = null;
            _client = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void StartReading()
    {
        _cts = new CancellationTokenSource();
        _readTask = ReadDataAsync(_cts.Token);
    }

    private async Task ReadDataAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line == null)
                {
                    HandleDisconnect("connection closed");
                    break;
                }

                // Handle Telnet protocol if enabled
                if (Proto == 2)
                {
                    // Basic telnet handling - just pass through for now
                    OnMessage?.Invoke(line);
                }
                else
                {
                    OnMessage?.Invoke(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            OnErro?.Invoke(ex.Message);
            HandleDisconnect("read error");
        }
    }

    private void HandleDisconnect(string reason)
    {
        if (_isOpen)
        {
            _isOpen = false;
            OnFechado?.Invoke(reason);
            Fechar();
        }
    }

    private static bool ValidateServerCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // Accept all certificates for now (like the original IntMUD)
        // In production, this should validate properly
        return true;
    }

    public void Dispose()
    {
        Fechar();
    }

    public override string ToString() => $"[Socket: {Ip}:{Porta}]";
}
