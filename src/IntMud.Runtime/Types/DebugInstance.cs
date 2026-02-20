using System.Diagnostics;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a debug instance.
/// Matches C++ TVarDebug - provides execution control and system diagnostics.
/// </summary>
public sealed class DebugInstance
{
    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Maximum instructions per function call (numfunc=1).
    /// C++ VarExec. Default 5000.
    /// </summary>
    public int Exec { get; set; } = 5000;

    /// <summary>
    /// Initial exec limit (for ini() reset).
    /// </summary>
    public int ExecIni { get; set; } = 5000;

    /// <summary>
    /// Error reporting level. 0=ignore, 1=partial, 2=full.
    /// </summary>
    public int Err { get; set; } = 1;

    /// <summary>
    /// Log level for debugging output.
    /// </summary>
    public int Log { get; set; } = 0;

    /// <summary>
    /// Reset exec counter to initial value. C++ FuncIni.
    /// </summary>
    public void Ini()
    {
        Exec = ExecIni;
    }

    /// <summary>
    /// Get user CPU time in milliseconds. C++ numfunc=2.
    /// </summary>
    public double Utempo()
    {
        try
        {
            return Process.GetCurrentProcess().UserProcessorTime.TotalMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Get system/kernel CPU time in milliseconds. C++ numfunc=3.
    /// </summary>
    public double Stempo()
    {
        try
        {
            return Process.GetCurrentProcess().PrivilegedProcessorTime.TotalMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Get current memory usage in bytes. C++ numfunc=4 (getCurrentRSS).
    /// </summary>
    public double Mem()
    {
        try
        {
            return Process.GetCurrentProcess().WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get peak memory usage in bytes. C++ numfunc=5 (getPeakRSS).
    /// </summary>
    public double MemMax()
    {
        try
        {
            return Process.GetCurrentProcess().PeakWorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get version string. C++ VERSION macro.
    /// </summary>
    public string Ver()
    {
        return typeof(DebugInstance).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Get build date string. C++ __DATE__ macro.
    /// </summary>
    public string Data()
    {
        var assembly = typeof(DebugInstance).Assembly;
        var buildDate = File.GetLastWriteTime(assembly.Location);
        return buildDate.ToString("MMM dd yyyy");
    }

    public override string ToString() => $"[Debug: exec={Exec}]";
}
