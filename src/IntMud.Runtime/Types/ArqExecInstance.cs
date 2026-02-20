using System.Diagnostics;
using System.Text;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an arqexec (external process) instance.
/// Maps to C++ TVarArqExec - executes external programs.
/// </summary>
public sealed class ArqExecInstance : IDisposable
{
    private Process? _process;
    private readonly StringBuilder _outputBuffer = new();
    private bool _isOpen;

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    public bool Valido => _isOpen && _process != null && !_process.HasExited;

    /// <summary>
    /// Event raised when output is received from the process.
    /// Matches C++ GeraEvento("msg", text, 0).
    /// </summary>
    public event Action<string>? OnMessage;

    /// <summary>
    /// Event raised when the process exits.
    /// Matches C++ GeraEvento("fechou", nullptr, exit_code).
    /// </summary>
    public event Action<int>? OnFechado;

    public bool Abrir(string command, string arguments = "")
    {
        try
        {
            Fechar();
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _process = new Process { StartInfo = startInfo };
            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (_outputBuffer) { _outputBuffer.AppendLine(e.Data); }
                    OnMessage?.Invoke(e.Data);
                }
            };
            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) =>
            {
                var exitCode = 0;
                try { exitCode = _process?.ExitCode ?? 0; } catch { }
                _isOpen = false;
                OnFechado?.Invoke(exitCode);
            };
            _process.Start();
            _process.BeginOutputReadLine();
            _isOpen = true;
            return true;
        }
        catch { return false; }
    }

    public void Msg(string text)
    {
        if (_process != null && !_process.HasExited)
        {
            try { _process.StandardInput.WriteLine(text); } catch { }
        }
    }

    public string Ler()
    {
        lock (_outputBuffer)
        {
            var result = _outputBuffer.ToString();
            _outputBuffer.Clear();
            return result;
        }
    }

    public void Fechar()
    {
        if (_process != null)
        {
            try { if (!_process.HasExited) _process.Kill(); } catch { }
            _process.Dispose();
            _process = null;
        }
        _isOpen = false;
    }

    public void Dispose()
    {
        Fechar();
    }

    public override string ToString() => "[ArqExec]";
}
