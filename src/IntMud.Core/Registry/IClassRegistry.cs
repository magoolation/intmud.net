using IntMud.Core.Types;

namespace IntMud.Core.Registry;

/// <summary>
/// Registry for IntMUD class definitions.
/// Manages class loading, lookup, and lifecycle.
/// </summary>
public interface IClassRegistry
{
    /// <summary>
    /// Get a class by name.
    /// </summary>
    /// <param name="name">Class name (case-insensitive)</param>
    /// <returns>The class, or null if not found</returns>
    IIntClass? GetClass(string name);

    /// <summary>
    /// Get all registered classes.
    /// </summary>
    IEnumerable<IIntClass> GetAllClasses();

    /// <summary>
    /// Check if a class exists.
    /// </summary>
    bool HasClass(string name);

    /// <summary>
    /// Register a new class.
    /// </summary>
    /// <param name="intClass">The class to register</param>
    void RegisterClass(IIntClass intClass);

    /// <summary>
    /// Unregister a class.
    /// </summary>
    /// <param name="name">Class name to unregister</param>
    /// <returns>True if class was removed</returns>
    bool UnregisterClass(string name);

    /// <summary>
    /// Clear all registered classes.
    /// </summary>
    void Clear();

    /// <summary>
    /// Number of registered classes.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Observable for class registration events.
    /// </summary>
    IObservable<ClassRegistryEvent> Events { get; }
}

/// <summary>
/// Class registry event.
/// </summary>
public abstract record ClassRegistryEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Class registered event.
/// </summary>
public sealed record ClassRegisteredEvent(string ClassName) : ClassRegistryEvent;

/// <summary>
/// Class unregistered event.
/// </summary>
public sealed record ClassUnregisteredEvent(string ClassName) : ClassRegistryEvent;

/// <summary>
/// Class updated event (hot reload).
/// </summary>
public sealed record ClassUpdatedEvent(string ClassName) : ClassRegistryEvent;
