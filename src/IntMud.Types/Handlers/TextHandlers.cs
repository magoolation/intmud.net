using System.Text;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Base class for text type handlers (txt1-512).
/// Text is stored as UTF-8 bytes with null terminator.
/// </summary>
public abstract class TextTypeHandlerBase : VariableTypeHandlerBase
{
    /// <summary>
    /// Maximum text length for this type.
    /// </summary>
    public abstract int MaxLength { get; }

    /// <inheritdoc />
    public override VariableType RuntimeType => VariableType.Text;

    /// <inheritdoc />
    public override int GetSize(ReadOnlySpan<byte> instruction) => MaxLength + 1; // +1 for null terminator

    /// <inheritdoc />
    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        // Text is true if not empty
        return memory.Length > 0 && memory[0] != 0;
    }

    /// <inheritdoc />
    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var text = GetText(memory);
        if (int.TryParse(text, out var value))
            return value;
        if (double.TryParse(text, out var dvalue))
            return (int)dvalue;
        return 0;
    }

    /// <inheritdoc />
    public override double GetDouble(ReadOnlySpan<byte> memory)
    {
        var text = GetText(memory);
        if (double.TryParse(text, out var value))
            return value;
        return 0.0;
    }

    /// <inheritdoc />
    public override string GetText(ReadOnlySpan<byte> memory)
    {
        // Find null terminator
        var nullIndex = memory.IndexOf((byte)0);
        if (nullIndex < 0)
            nullIndex = Math.Min(memory.Length, MaxLength);
        else
            nullIndex = Math.Min(nullIndex, MaxLength);

        if (nullIndex == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(memory[..nullIndex]);
    }

    /// <inheritdoc />
    public override void SetInt(Span<byte> memory, int value)
    {
        SetText(memory, value.ToString());
    }

    /// <inheritdoc />
    public override void SetDouble(Span<byte> memory, double value)
    {
        SetText(memory, value.ToString("G"));
    }

    /// <inheritdoc />
    public override void SetText(Span<byte> memory, string value)
    {
        memory.Clear();

        if (string.IsNullOrEmpty(value))
            return;

        // Truncate if too long
        var bytes = Encoding.UTF8.GetBytes(value);
        var copyLength = Math.Min(bytes.Length, MaxLength);

        // Make sure we don't cut in the middle of a UTF-8 sequence
        while (copyLength > 0 && (bytes[copyLength - 1] & 0xC0) == 0x80)
            copyLength--;

        bytes.AsSpan(0, copyLength).CopyTo(memory);
        // Ensure null terminator
        if (copyLength < memory.Length)
            memory[copyLength] = 0;
    }

    /// <inheritdoc />
    public override bool Add(Span<byte> memory, ReadOnlySpan<byte> value)
    {
        // Concatenate strings
        var current = GetText(memory);
        var toAdd = GetTextFromMemory(value);
        SetText(memory, current + toAdd);
        return true;
    }

    /// <inheritdoc />
    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var leftText = GetText(left);
        var rightText = GetTextFromMemory(right);
        return string.Compare(leftText, rightText, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var leftText = GetText(left);
        var rightText = GetTextFromMemory(right);
        return leftText == rightText;
    }

    private static string GetTextFromMemory(ReadOnlySpan<byte> memory)
    {
        var nullIndex = memory.IndexOf((byte)0);
        if (nullIndex < 0)
            nullIndex = memory.Length;
        if (nullIndex == 0)
            return string.Empty;
        return Encoding.UTF8.GetString(memory[..nullIndex]);
    }

    /// <inheritdoc />
    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "tam":
            case "tamanho":
            case "len":
                // Return text length
                context.SetReturnInt(GetText(memory).Length);
                return true;

            case "mai":
            case "maiusculo":
            case "upper":
                // Convert to uppercase
                SetText(memory, GetText(memory).ToUpperInvariant());
                return true;

            case "min":
            case "minusculo":
            case "lower":
                // Convert to lowercase
                SetText(memory, GetText(memory).ToLowerInvariant());
                return true;

            case "limpar":
            case "clear":
                // Clear the text
                memory.Clear();
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for txt1 (1-256 characters) variables.
/// OpCode cTxt1 covers txt1 through txt256.
/// </summary>
public sealed class Txt1Handler : TextTypeHandlerBase
{
    private readonly int _maxLength;

    /// <summary>
    /// Create handler for specific text size (1-256).
    /// </summary>
    public Txt1Handler(int maxLength = 256)
    {
        _maxLength = Math.Clamp(maxLength, 1, 256);
    }

    public override OpCode OpCode => OpCode.Txt1;
    public override string TypeName => $"txt{_maxLength}";
    public override int MaxLength => _maxLength;

    public override int GetSize(ReadOnlySpan<byte> instruction)
    {
        // The instruction contains the actual size (1-256 stored as 0-255)
        if (instruction.Length >= 2)
        {
            // Size is stored as value-1 (so 1-256 maps to 0-255)
            var storedSize = instruction[1];
            var actualSize = storedSize + 1; // 0->1, 255->256
            if (actualSize >= 1 && actualSize <= 256)
                return actualSize + 1; // +1 for null terminator
        }
        return _maxLength + 1;
    }
}

/// <summary>
/// Handler for txt2 (257-512 characters) variables.
/// OpCode cTxt2 covers txt257 through txt512.
/// </summary>
public sealed class Txt2Handler : TextTypeHandlerBase
{
    private readonly int _maxLength;

    /// <summary>
    /// Create handler for specific text size (257-512).
    /// </summary>
    public Txt2Handler(int maxLength = 512)
    {
        _maxLength = Math.Clamp(maxLength, 257, 512);
    }

    public override OpCode OpCode => OpCode.Txt2;
    public override string TypeName => $"txt{_maxLength}";
    public override int MaxLength => _maxLength;

    public override int GetSize(ReadOnlySpan<byte> instruction)
    {
        // The instruction contains the actual size as 2 bytes
        if (instruction.Length >= 3)
        {
            var size = instruction[1] | (instruction[2] << 8);
            if (size >= 257 && size <= 512)
                return size + 1; // +1 for null terminator
        }
        return _maxLength + 1;
    }
}

/// <summary>
/// Factory for creating text handlers with specific sizes.
/// </summary>
public static class TextHandlerFactory
{
    /// <summary>
    /// Create a text handler for the specified size.
    /// </summary>
    public static TextTypeHandlerBase Create(int maxLength)
    {
        if (maxLength <= 256)
            return new Txt1Handler(maxLength);
        else
            return new Txt2Handler(maxLength);
    }

    /// <summary>
    /// Get the appropriate OpCode for a text size.
    /// </summary>
    public static OpCode GetOpCode(int maxLength)
    {
        return maxLength <= 256 ? OpCode.Txt1 : OpCode.Txt2;
    }
}
