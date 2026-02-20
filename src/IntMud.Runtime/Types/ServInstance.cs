using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a serv (server socket) instance.
/// Used for accepting incoming TCP connections.
/// When a new connection is accepted, fires {variableName}_socket event.
/// </summary>
public sealed class ServInstance : IDisposable
{
    private TcpListener? _listener;
    private bool _isOpen;
    private bool _useSsl;
    private X509Certificate2? _certificate;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;

    /// <summary>
    /// The owner object that contains this serv variable.
    /// </summary>
    public BytecodeRuntimeObject? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Event raised when a new socket connection is accepted.
    /// The parameter is the new SocketInstance.
    /// </summary>
    public event Action<SocketInstance>? OnSocketConnected;

    /// <summary>
    /// Check if server is listening.
    /// </summary>
    public bool Valido => _isOpen;

    /// <summary>
    /// Open server on specified address and port.
    /// </summary>
    public bool Abrir(string address, int port)
    {
        try
        {
            Fechar();

            var ipAddress = string.IsNullOrEmpty(address) || address == "0.0.0.0"
                ? IPAddress.Any
                : IPAddress.Parse(address);

            _listener = new TcpListener(ipAddress, port);
            _listener.Start();
            _isOpen = true;
            _useSsl = false;

            StartAccepting();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Open SSL/TLS server on specified address and port.
    /// </summary>
    public bool AbrirSsl(string address, int port, string certPath = "", string certPassword = "")
    {
        try
        {
            Fechar();

            // Load certificate if provided
            if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
            {
                _certificate = new X509Certificate2(certPath, certPassword);
            }

            var ipAddress = string.IsNullOrEmpty(address) || address == "0.0.0.0"
                ? IPAddress.Any
                : IPAddress.Parse(address);

            _listener = new TcpListener(ipAddress, port);
            _listener.Start();
            _isOpen = true;
            _useSsl = true;

            StartAccepting();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Close the server.
    /// </summary>
    public void Fechar()
    {
        _isOpen = false;
        _cts?.Cancel();

        try
        {
            _listener?.Stop();
        }
        catch { }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _certificate?.Dispose();
        _certificate = null;
    }

    private void StartAccepting()
    {
        _cts = new CancellationTokenSource();
        _acceptTask = AcceptConnectionsAsync(_cts.Token);
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);

                var socket = new SocketInstance
                {
                    Owner = Owner,
                    VariableName = $"{VariableName}_client"
                };

                if (_useSsl && _certificate != null)
                {
                    await socket.AcceptSslAsync(client, _certificate);
                }
                else
                {
                    socket.AcceptClient(client);
                }

                OnSocketConnected?.Invoke(socket);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Connection error, continue accepting
            }
        }
    }

    public void Dispose()
    {
        Fechar();
    }

    public override string ToString() => $"[Serv: {(_isOpen ? "listening" : "closed")}]";
}
