using System.Text;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an arqdir (directory) instance.
/// Maps to C++ TVarArqDir - directory listing and file operations.
/// </summary>
public sealed class ArqDirInstance
{
    private string[]? _entries;
    private int _index;
    private string _currentPath = "";

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    public bool IsOpen => _entries != null;

    /// <summary>Open directory for listing.</summary>
    public bool Abrir(string path)
    {
        try
        {
            Fechar();
            _currentPath = path;
            if (!Directory.Exists(path)) return false;
            _entries = Directory.GetFileSystemEntries(path);
            _index = 0;
            return true;
        }
        catch { return false; }
    }

    /// <summary>Close directory.</summary>
    public void Fechar()
    {
        _entries = null;
        _index = 0;
    }

    /// <summary>Check if there are entries (lin = entry available).</summary>
    public bool Lin => _entries != null && _index < _entries.Length;

    /// <summary>Get current entry name.</summary>
    public string Texto()
    {
        if (_entries == null || _index >= _entries.Length) return "";
        return Path.GetFileName(_entries[_index]);
    }

    /// <summary>Advance to next entry.</summary>
    public void Depois()
    {
        if (_entries != null && _index < _entries.Length)
            _index++;
    }

    /// <summary>Get entry type: 'D' for directory, 'A' for file.</summary>
    public string Tipo()
    {
        if (_entries == null || _index >= _entries.Length) return "";
        return Directory.Exists(_entries[_index]) ? "D" : "A";
    }

    /// <summary>Get file size.</summary>
    public long Tamanho()
    {
        try
        {
            if (_entries == null || _index >= _entries.Length) return 0;
            var info = new FileInfo(_entries[_index]);
            return info.Exists ? info.Length : 0;
        }
        catch { return 0; }
    }

    /// <summary>Get modification time as Unix timestamp.</summary>
    public long Mtempo()
    {
        try
        {
            if (_entries == null || _index >= _entries.Length) return 0;
            return new DateTimeOffset(File.GetLastWriteTime(_entries[_index])).ToUnixTimeSeconds();
        }
        catch { return 0; }
    }

    /// <summary>Get access time as Unix timestamp.</summary>
    public long Atempo()
    {
        try
        {
            if (_entries == null || _index >= _entries.Length) return 0;
            return new DateTimeOffset(File.GetLastAccessTime(_entries[_index])).ToUnixTimeSeconds();
        }
        catch { return 0; }
    }

    /// <summary>Convert path separators to platform-native.</summary>
    public static string Barra(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    /// <summary>Delete a file.</summary>
    public static bool Apagar(string path)
    {
        try { File.Delete(path); return true; } catch { return false; }
    }

    /// <summary>Delete a directory.</summary>
    public static bool ApagarDir(string path)
    {
        try { Directory.Delete(path, false); return true; } catch { return false; }
    }

    /// <summary>Create a directory.</summary>
    public static bool CriarDir(string path)
    {
        try { Directory.CreateDirectory(path); return true; } catch { return false; }
    }

    /// <summary>Rename a file or directory.</summary>
    public static bool Renomear(string oldPath, string newPath)
    {
        try
        {
            if (File.Exists(oldPath)) { File.Move(oldPath, newPath); return true; }
            if (Directory.Exists(oldPath)) { Directory.Move(oldPath, newPath); return true; }
            return false;
        }
        catch { return false; }
    }

    public override string ToString() => $"[ArqDir: {_currentPath}]";
}
