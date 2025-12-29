using Microsoft.Extensions.Logging;

namespace IntMud.Hosting;

/// <summary>
/// Watches for file changes and triggers recompilation.
/// </summary>
public sealed class HotReloadWatcher : IDisposable
{
    private readonly string _sourcePath;
    private readonly ILogger<HotReloadWatcher> _logger;
    private FileSystemWatcher? _watcher;
    private readonly HashSet<string> _changedFiles = new();
    private readonly object _lock = new();
    private Timer? _debounceTimer;
    private bool _disposed;

    /// <summary>
    /// Debounce delay in milliseconds.
    /// </summary>
    public int DebounceDelayMs { get; set; } = 500;

    /// <summary>
    /// Event raised when files have changed.
    /// </summary>
    public event EventHandler<FileChangedEventArgs>? FilesChanged;

    public HotReloadWatcher(string sourcePath, ILogger<HotReloadWatcher> logger)
    {
        _sourcePath = sourcePath;
        _logger = logger;
    }

    /// <summary>
    /// Start watching for file changes.
    /// </summary>
    public void Start()
    {
        if (_watcher != null)
            return;

        if (!Directory.Exists(_sourcePath))
        {
            _logger.LogWarning("Source path does not exist: {SourcePath}", _sourcePath);
            return;
        }

        _watcher = new FileSystemWatcher(_sourcePath)
        {
            Filter = "*.int",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;

        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Hot-reload watcher started for: {SourcePath}", _sourcePath);
    }

    /// <summary>
    /// Stop watching for file changes.
    /// </summary>
    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        _logger.LogInformation("Hot-reload watcher stopped");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _changedFiles.Add(e.FullPath);
        }

        // Debounce to collect multiple rapid changes
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ => ProcessChanges(),
            null,
            TimeSpan.FromMilliseconds(DebounceDelayMs),
            Timeout.InfiniteTimeSpan);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        lock (_lock)
        {
            _changedFiles.Add(e.OldFullPath);
            _changedFiles.Add(e.FullPath);
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(
            _ => ProcessChanges(),
            null,
            TimeSpan.FromMilliseconds(DebounceDelayMs),
            Timeout.InfiniteTimeSpan);
    }

    private void ProcessChanges()
    {
        string[] changedFiles;
        lock (_lock)
        {
            if (_changedFiles.Count == 0)
                return;

            changedFiles = _changedFiles.ToArray();
            _changedFiles.Clear();
        }

        _logger.LogInformation("Detected {Count} file change(s)", changedFiles.Length);
        foreach (var file in changedFiles)
        {
            _logger.LogDebug("Changed: {File}", file);
        }

        FilesChanged?.Invoke(this, new FileChangedEventArgs
        {
            ChangedFiles = changedFiles
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}

/// <summary>
/// Event args for file changed event.
/// </summary>
public class FileChangedEventArgs : EventArgs
{
    /// <summary>
    /// List of files that changed.
    /// </summary>
    public required string[] ChangedFiles { get; init; }
}
