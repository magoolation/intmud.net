namespace IntMud.Core.Types;

/// <summary>
/// Represents an IntMUD object instance.
/// Equivalent to TObjeto from the original C++ implementation.
/// </summary>
public interface IIntObject : IDisposable
{
    /// <summary>
    /// Unique identifier for this object.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The class this object is an instance of.
    /// </summary>
    IIntClass Class { get; }

    /// <summary>
    /// Previous object in the class's linked list.
    /// </summary>
    IIntObject? Previous { get; }

    /// <summary>
    /// Next object in the class's linked list.
    /// </summary>
    IIntObject? Next { get; }

    /// <summary>
    /// Whether this object is marked for deletion.
    /// </summary>
    bool IsMarkedForDeletion { get; }

    /// <summary>
    /// Whether this object has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Raw variable data for this object instance.
    /// </summary>
    Memory<byte> VariableData { get; }

    /// <summary>
    /// Mark this object for deletion.
    /// The object will be cleaned up at the end of the current execution cycle.
    /// </summary>
    void MarkForDeletion();

    /// <summary>
    /// Get a variable value by name.
    /// </summary>
    T GetVariable<T>(string name);

    /// <summary>
    /// Set a variable value by name.
    /// </summary>
    void SetVariable<T>(string name, T value);

    /// <summary>
    /// Check if a variable exists on this object.
    /// </summary>
    bool HasVariable(string name);
}
