using System.Buffers;
using System.Text;

namespace IntMud.Runtime.Stacks;

/// <summary>
/// Data stack for raw byte storage during execution.
/// Equivalent to DadosPilha (64KB) from the original C++ implementation.
/// Uses ArrayPool for efficient memory management.
/// </summary>
public sealed class DataStack : IDisposable
{
    private const int DefaultCapacity = 65536; // 64KB

    private byte[] _buffer;
    private int _top;
    private readonly ArrayPool<byte> _pool;
    private bool _disposed;

    /// <summary>
    /// Create a new data stack with default capacity.
    /// </summary>
    public DataStack() : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Create a new data stack with specified capacity.
    /// </summary>
    public DataStack(int initialCapacity)
    {
        _pool = ArrayPool<byte>.Shared;
        _buffer = _pool.Rent(initialCapacity);
        _top = 0;
    }

    /// <summary>
    /// Current position in the stack (bytes used).
    /// </summary>
    public int Position => _top;

    /// <summary>
    /// Total capacity of the stack.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Available space remaining.
    /// </summary>
    public int Available => _buffer.Length - _top;

    /// <summary>
    /// Allocate space on the stack.
    /// </summary>
    /// <param name="size">Number of bytes to allocate</param>
    /// <returns>Span to the allocated memory</returns>
    /// <exception cref="InvalidOperationException">If stack is full</exception>
    public Span<byte> Allocate(int size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (size <= 0)
            return Span<byte>.Empty;

        EnsureCapacity(_top + size);

        var span = _buffer.AsSpan(_top, size);
        span.Clear(); // Initialize to zero
        _top += size;

        return span;
    }

    /// <summary>
    /// Push a string onto the stack.
    /// </summary>
    /// <param name="value">String to push</param>
    /// <returns>Offset where string was stored</returns>
    public int PushString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return _top;

        var offset = _top;
        var bytes = Encoding.UTF8.GetBytes(value);
        var span = Allocate(bytes.Length + 1); // +1 for null terminator
        bytes.CopyTo(span);
        span[bytes.Length] = 0; // Null terminator

        return offset;
    }

    /// <summary>
    /// Push bytes onto the stack.
    /// </summary>
    /// <param name="data">Bytes to push</param>
    /// <returns>Offset where data was stored</returns>
    public int Push(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return _top;

        var offset = _top;
        var span = Allocate(data.Length);
        data.CopyTo(span);

        return offset;
    }

    /// <summary>
    /// Get a string from the stack at the specified offset.
    /// </summary>
    public string GetString(int offset)
    {
        if (offset < 0 || offset >= _top)
            return string.Empty;

        var span = _buffer.AsSpan(offset);
        var nullIndex = span.IndexOf((byte)0);
        if (nullIndex < 0)
            nullIndex = _top - offset;

        return Encoding.UTF8.GetString(span[..nullIndex]);
    }

    /// <summary>
    /// Get a span to data at the specified offset.
    /// </summary>
    public Span<byte> GetSpan(int offset, int length)
    {
        if (offset < 0 || offset + length > _top)
            throw new ArgumentOutOfRangeException(nameof(offset));

        return _buffer.AsSpan(offset, length);
    }

    /// <summary>
    /// Get a read-only span to data at the specified offset.
    /// </summary>
    public ReadOnlySpan<byte> GetReadOnlySpan(int offset, int length)
    {
        if (offset < 0 || offset + length > _top)
            throw new ArgumentOutOfRangeException(nameof(offset));

        return _buffer.AsSpan(offset, length);
    }

    /// <summary>
    /// Free space from the top of the stack.
    /// </summary>
    /// <param name="size">Number of bytes to free</param>
    public void Free(int size)
    {
        _top = Math.Max(0, _top - size);
    }

    /// <summary>
    /// Free everything above the specified position.
    /// </summary>
    public void FreeAbove(int position)
    {
        _top = Math.Max(0, Math.Min(position, _top));
    }

    /// <summary>
    /// Reset the stack to empty.
    /// </summary>
    public void Clear()
    {
        _top = 0;
    }

    /// <summary>
    /// Save current position for later restoration.
    /// </summary>
    public int SavePosition() => _top;

    /// <summary>
    /// Restore to a previously saved position.
    /// </summary>
    public void RestorePosition(int position)
    {
        if (position < 0 || position > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        _top = position;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
            return;

        var newSize = Math.Max(_buffer.Length * 2, required);
        var newBuffer = _pool.Rent(newSize);
        _buffer.AsSpan(0, _top).CopyTo(newBuffer);
        _pool.Return(_buffer);
        _buffer = newBuffer;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pool.Return(_buffer);
        _buffer = Array.Empty<byte>();
    }
}
