using IntMud.Core.Types;

namespace IntMud.Core.Registry;

/// <summary>
/// Manages source files (.int files) and supports hot-reload.
/// </summary>
public interface ISourceFileManager
{
    /// <summary>
    /// Get a source file by path.
    /// </summary>
    ISourceFile? GetFile(string path);

    /// <summary>
    /// Get all loaded source files.
    /// </summary>
    IEnumerable<ISourceFile> GetAllFiles();

    /// <summary>
    /// Load a source file.
    /// </summary>
    /// <param name="path">Path to the .int file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The loaded source file</returns>
    ValueTask<ISourceFile> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load all .int files from a directory.
    /// </summary>
    /// <param name="directory">Directory path</param>
    /// <param name="recursive">Whether to search subdirectories</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask LoadDirectoryAsync(string directory, bool recursive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reload a modified source file.
    /// </summary>
    /// <param name="path">Path to the .int file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask ReloadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unload a source file and its classes.
    /// </summary>
    /// <param name="path">Path to the .int file</param>
    void Unload(string path);

    /// <summary>
    /// Enable file watching for hot-reload.
    /// </summary>
    /// <param name="directory">Directory to watch</param>
    void EnableWatching(string directory);

    /// <summary>
    /// Disable file watching.
    /// </summary>
    void DisableWatching();

    /// <summary>
    /// Whether file watching is enabled.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Observable for file change events.
    /// </summary>
    IObservable<FileChangedEvent> FileChanges { get; }
}

/// <summary>
/// File change event.
/// </summary>
public abstract record FileChangedEvent
{
    public required string FilePath { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// File modified event.
/// </summary>
public sealed record FileModifiedEvent : FileChangedEvent;

/// <summary>
/// File created event.
/// </summary>
public sealed record FileCreatedEvent : FileChangedEvent;

/// <summary>
/// File deleted event.
/// </summary>
public sealed record FileDeletedEvent : FileChangedEvent;

/// <summary>
/// File reload completed event.
/// </summary>
public sealed record FileReloadedEvent : FileChangedEvent
{
    public required IReadOnlyList<string> UpdatedClasses { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
