namespace IntMud.Runtime.Values;

/// <summary>
/// Type of a runtime value.
/// </summary>
public enum RuntimeValueType
{
    Null,
    Integer,
    Double,
    String,
    Object,
    Boolean,
    Array
}

/// <summary>
/// Represents a value during execution.
/// This is a discriminated union that can hold different types of values.
/// </summary>
public readonly struct RuntimeValue : IEquatable<RuntimeValue>
{
    private readonly RuntimeValueType _type;
    private readonly long _intValue;
    private readonly double _doubleValue;
    private readonly string? _stringValue;
    private readonly object? _objectValue;

    private RuntimeValue(RuntimeValueType type, long intValue = 0, double doubleValue = 0,
        string? stringValue = null, object? objectValue = null)
    {
        _type = type;
        _intValue = intValue;
        _doubleValue = doubleValue;
        _stringValue = stringValue;
        _objectValue = objectValue;
    }

    /// <summary>
    /// The null value (nulo).
    /// </summary>
    public static readonly RuntimeValue Null = new(RuntimeValueType.Null);

    /// <summary>
    /// True value (1).
    /// </summary>
    public static readonly RuntimeValue True = new(RuntimeValueType.Integer, 1);

    /// <summary>
    /// False value (0).
    /// </summary>
    public static readonly RuntimeValue False = new(RuntimeValueType.Integer, 0);

    /// <summary>
    /// Zero value.
    /// </summary>
    public static readonly RuntimeValue Zero = new(RuntimeValueType.Integer, 0);

    /// <summary>
    /// One value.
    /// </summary>
    public static readonly RuntimeValue One = new(RuntimeValueType.Integer, 1);

    /// <summary>
    /// Empty string value.
    /// </summary>
    public static readonly RuntimeValue EmptyString = new(RuntimeValueType.String, stringValue: string.Empty);

    /// <summary>
    /// Create an integer value.
    /// </summary>
    public static RuntimeValue FromInt(long value) => new(RuntimeValueType.Integer, intValue: value);

    /// <summary>
    /// Create a double value.
    /// </summary>
    public static RuntimeValue FromDouble(double value) => new(RuntimeValueType.Double, doubleValue: value);

    /// <summary>
    /// Create a string value.
    /// </summary>
    public static RuntimeValue FromString(string? value) =>
        value == null ? Null : new(RuntimeValueType.String, stringValue: value);

    /// <summary>
    /// Create a boolean value.
    /// </summary>
    public static RuntimeValue FromBool(bool value) => value ? True : False;

    /// <summary>
    /// Create an object reference value.
    /// </summary>
    public static RuntimeValue FromObject(object? value) =>
        value == null ? Null : new(RuntimeValueType.Object, objectValue: value);

    /// <summary>
    /// Create an array value.
    /// </summary>
    public static RuntimeValue FromArray(List<RuntimeValue>? value) =>
        value == null ? Null : new(RuntimeValueType.Array, objectValue: value);

    /// <summary>
    /// Create an array value with a specified size.
    /// </summary>
    public static RuntimeValue CreateArray(int size)
    {
        var array = new List<RuntimeValue>(size);
        for (int i = 0; i < size; i++)
            array.Add(Null);
        return new(RuntimeValueType.Array, objectValue: array);
    }

    /// <summary>
    /// The type of this value.
    /// </summary>
    public RuntimeValueType Type => _type;

    /// <summary>
    /// Whether this value is null.
    /// </summary>
    public bool IsNull => _type == RuntimeValueType.Null;

    /// <summary>
    /// Whether this value is truthy (non-zero number, non-empty string, non-null object, or non-empty array).
    /// </summary>
    public bool IsTruthy => _type switch
    {
        RuntimeValueType.Null => false,
        RuntimeValueType.Integer => _intValue != 0,
        RuntimeValueType.Double => _doubleValue != 0,
        RuntimeValueType.String => !string.IsNullOrEmpty(_stringValue),
        RuntimeValueType.Object => _objectValue != null,
        RuntimeValueType.Boolean => _intValue != 0,
        RuntimeValueType.Array => _objectValue is List<RuntimeValue> list && list.Count > 0,
        _ => false
    };

    /// <summary>
    /// Get value as integer.
    /// </summary>
    public long AsInt() => _type switch
    {
        RuntimeValueType.Null => 0,
        RuntimeValueType.Integer => _intValue,
        RuntimeValueType.Double => (long)_doubleValue,
        RuntimeValueType.String => long.TryParse(_stringValue, out var v) ? v : 0,
        RuntimeValueType.Boolean => _intValue,
        _ => 0
    };

    /// <summary>
    /// Get value as double.
    /// </summary>
    public double AsDouble() => _type switch
    {
        RuntimeValueType.Null => 0,
        RuntimeValueType.Integer => _intValue,
        RuntimeValueType.Double => _doubleValue,
        RuntimeValueType.String => double.TryParse(_stringValue, out var v) ? v : 0,
        RuntimeValueType.Boolean => _intValue,
        _ => 0
    };

    /// <summary>
    /// Get value as string.
    /// </summary>
    public string AsString() => _type switch
    {
        RuntimeValueType.Null => "nulo",
        RuntimeValueType.Integer => _intValue.ToString(),
        RuntimeValueType.Double => _doubleValue.ToString("G"),
        RuntimeValueType.String => _stringValue ?? string.Empty,
        RuntimeValueType.Object => _objectValue?.ToString() ?? "nulo",
        RuntimeValueType.Boolean => _intValue != 0 ? "1" : "0",
        RuntimeValueType.Array => _objectValue is List<RuntimeValue> list
            ? $"[{string.Join(", ", list.Select(v => v.AsString()))}]"
            : "[]",
        _ => string.Empty
    };

    /// <summary>
    /// Get value as boolean.
    /// </summary>
    public bool AsBool() => IsTruthy;

    /// <summary>
    /// Get value as object.
    /// </summary>
    public object? AsObject() => _type switch
    {
        RuntimeValueType.Object => _objectValue,
        RuntimeValueType.Null => null,
        _ => null
    };

    /// <summary>
    /// Get value as a specific type.
    /// </summary>
    public T? AsObject<T>() where T : class => AsObject() as T;

    /// <summary>
    /// Get value as array.
    /// </summary>
    public List<RuntimeValue>? AsArray() => _type switch
    {
        RuntimeValueType.Array => _objectValue as List<RuntimeValue>,
        RuntimeValueType.Null => null,
        _ => null
    };

    /// <summary>
    /// Get the length of the value (for strings and arrays).
    /// </summary>
    public int Length => _type switch
    {
        RuntimeValueType.String => _stringValue?.Length ?? 0,
        RuntimeValueType.Array => (_objectValue as List<RuntimeValue>)?.Count ?? 0,
        _ => 0
    };

    /// <summary>
    /// Get value at index (for arrays).
    /// </summary>
    public RuntimeValue GetIndex(int index)
    {
        if (_type != RuntimeValueType.Array) return Null;
        var list = _objectValue as List<RuntimeValue>;
        if (list == null || index < 0 || index >= list.Count) return Null;
        return list[index];
    }

    /// <summary>
    /// Set value at index (for arrays).
    /// </summary>
    public void SetIndex(int index, RuntimeValue value)
    {
        if (_type != RuntimeValueType.Array) return;
        var list = _objectValue as List<RuntimeValue>;
        if (list == null) return;
        // Extend array if needed
        while (list.Count <= index)
            list.Add(Null);
        list[index] = value;
    }

    /// <summary>
    /// Push a value to the end of the array.
    /// </summary>
    public void ArrayPush(RuntimeValue value)
    {
        if (_type != RuntimeValueType.Array) return;
        var list = _objectValue as List<RuntimeValue>;
        list?.Add(value);
    }

    /// <summary>
    /// Pop a value from the end of the array.
    /// </summary>
    public RuntimeValue ArrayPop()
    {
        if (_type != RuntimeValueType.Array) return Null;
        var list = _objectValue as List<RuntimeValue>;
        if (list == null || list.Count == 0) return Null;
        var value = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        return value;
    }

    /// <summary>
    /// Remove and return the first element of the array.
    /// </summary>
    public RuntimeValue ArrayShift()
    {
        if (_type != RuntimeValueType.Array) return Null;
        var list = _objectValue as List<RuntimeValue>;
        if (list == null || list.Count == 0) return Null;
        var value = list[0];
        list.RemoveAt(0);
        return value;
    }

    /// <summary>
    /// Add a value to the beginning of the array.
    /// </summary>
    public void ArrayUnshift(RuntimeValue value)
    {
        if (_type != RuntimeValueType.Array) return;
        var list = _objectValue as List<RuntimeValue>;
        list?.Insert(0, value);
    }

    /// <summary>
    /// Clear all elements from the array.
    /// </summary>
    public void ArrayClear()
    {
        if (_type != RuntimeValueType.Array) return;
        var list = _objectValue as List<RuntimeValue>;
        list?.Clear();
    }

    /// <summary>
    /// Reverse the array in place.
    /// </summary>
    public void ArrayReverse()
    {
        if (_type != RuntimeValueType.Array) return;
        var list = _objectValue as List<RuntimeValue>;
        list?.Reverse();
    }

    /// <summary>
    /// Convert to the same type as another value (for binary operations).
    /// </summary>
    public RuntimeValue ConvertTo(RuntimeValueType targetType) => targetType switch
    {
        RuntimeValueType.Integer => FromInt(AsInt()),
        RuntimeValueType.Double => FromDouble(AsDouble()),
        RuntimeValueType.String => FromString(AsString()),
        RuntimeValueType.Boolean => FromBool(IsTruthy),
        _ => this
    };

    // Arithmetic operators
    public static RuntimeValue operator +(RuntimeValue a, RuntimeValue b)
    {
        // String concatenation if first operand is string
        if (a._type == RuntimeValueType.String)
            return FromString(a.AsString() + b.AsString());

        // Numeric addition
        if (a._type == RuntimeValueType.Double || b._type == RuntimeValueType.Double)
            return FromDouble(a.AsDouble() + b.AsDouble());

        return FromInt(a.AsInt() + b.AsInt());
    }

    public static RuntimeValue operator -(RuntimeValue a, RuntimeValue b)
    {
        if (a._type == RuntimeValueType.Double || b._type == RuntimeValueType.Double)
            return FromDouble(a.AsDouble() - b.AsDouble());
        return FromInt(a.AsInt() - b.AsInt());
    }

    public static RuntimeValue operator *(RuntimeValue a, RuntimeValue b)
    {
        if (a._type == RuntimeValueType.Double || b._type == RuntimeValueType.Double)
            return FromDouble(a.AsDouble() * b.AsDouble());
        return FromInt(a.AsInt() * b.AsInt());
    }

    public static RuntimeValue operator /(RuntimeValue a, RuntimeValue b)
    {
        var divisor = b.AsDouble();
        if (divisor == 0) return FromDouble(double.PositiveInfinity);
        return FromDouble(a.AsDouble() / divisor);
    }

    public static RuntimeValue operator %(RuntimeValue a, RuntimeValue b)
    {
        var divisor = b.AsInt();
        if (divisor == 0) return Zero;
        return FromInt(a.AsInt() % divisor);
    }

    public static RuntimeValue operator -(RuntimeValue a)
    {
        if (a._type == RuntimeValueType.Double)
            return FromDouble(-a._doubleValue);
        return FromInt(-a.AsInt());
    }

    // Comparison operators
    public static RuntimeValue operator <(RuntimeValue a, RuntimeValue b) =>
        FromBool(Compare(a, b) < 0);

    public static RuntimeValue operator >(RuntimeValue a, RuntimeValue b) =>
        FromBool(Compare(a, b) > 0);

    public static RuntimeValue operator <=(RuntimeValue a, RuntimeValue b) =>
        FromBool(Compare(a, b) <= 0);

    public static RuntimeValue operator >=(RuntimeValue a, RuntimeValue b) =>
        FromBool(Compare(a, b) >= 0);

    public static RuntimeValue operator ==(RuntimeValue a, RuntimeValue b) =>
        FromBool(a.Equals(b));

    public static RuntimeValue operator !=(RuntimeValue a, RuntimeValue b) =>
        FromBool(!a.Equals(b));

    private static int Compare(RuntimeValue a, RuntimeValue b)
    {
        // If first operand is string, compare as strings
        if (a._type == RuntimeValueType.String)
            return string.Compare(a.AsString(), b.AsString(), StringComparison.Ordinal);

        // Compare as numbers
        return a.AsDouble().CompareTo(b.AsDouble());
    }

    // Bitwise operators
    public static RuntimeValue operator &(RuntimeValue a, RuntimeValue b)
    {
        // Text bitwise and (hexadecimal)
        if (a._type == RuntimeValueType.String)
            return FromString(BitwiseTextOp(a.AsString(), b.AsString(), (x, y) => x & y));
        return FromInt(a.AsInt() & b.AsInt());
    }

    public static RuntimeValue operator |(RuntimeValue a, RuntimeValue b)
    {
        if (a._type == RuntimeValueType.String)
            return FromString(BitwiseTextOp(a.AsString(), b.AsString(), (x, y) => x | y));
        return FromInt(a.AsInt() | b.AsInt());
    }

    public static RuntimeValue operator ^(RuntimeValue a, RuntimeValue b)
    {
        if (a._type == RuntimeValueType.String)
            return FromString(BitwiseTextOp(a.AsString(), b.AsString(), (x, y) => x ^ y));
        return FromInt(a.AsInt() ^ b.AsInt());
    }

    public static RuntimeValue operator ~(RuntimeValue a)
    {
        if (a._type == RuntimeValueType.String)
        {
            var result = new char[a._stringValue!.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var c = a._stringValue[i];
                var val = HexCharToInt(c);
                result[i] = IntToHexChar(~val & 0xF);
            }
            return FromString(new string(result));
        }
        return FromInt(~a.AsInt());
    }

    public static RuntimeValue ShiftLeft(RuntimeValue a, RuntimeValue b)
    {
        if (a._type == RuntimeValueType.String)
            return FromString(ShiftHexString(a.AsString(), (int)b.AsInt(), true));
        return FromInt(a.AsInt() << (int)b.AsInt());
    }

    public static RuntimeValue ShiftRight(RuntimeValue a, RuntimeValue b)
    {
        if (a._type == RuntimeValueType.String)
            return FromString(ShiftHexString(a.AsString(), (int)b.AsInt(), false));
        return FromInt(a.AsInt() >> (int)b.AsInt());
    }

    // Logical operators
    public static RuntimeValue LogicalAnd(RuntimeValue a, RuntimeValue b) =>
        FromBool(a.IsTruthy && b.IsTruthy);

    public static RuntimeValue LogicalOr(RuntimeValue a, RuntimeValue b) =>
        FromBool(a.IsTruthy || b.IsTruthy);

    public static RuntimeValue LogicalNot(RuntimeValue a) =>
        FromBool(!a.IsTruthy);

    // Strict equality (same type and value)
    public bool StrictEquals(RuntimeValue other)
    {
        if (_type != other._type) return false;
        return _type switch
        {
            RuntimeValueType.Null => true,
            RuntimeValueType.Integer => _intValue == other._intValue,
            RuntimeValueType.Double => _doubleValue == other._doubleValue,
            RuntimeValueType.String => _stringValue == other._stringValue,
            RuntimeValueType.Object => ReferenceEquals(_objectValue, other._objectValue),
            RuntimeValueType.Boolean => _intValue == other._intValue,
            _ => false
        };
    }

    // Equality (with type coercion)
    public bool Equals(RuntimeValue other)
    {
        // Null check
        if (_type == RuntimeValueType.Null || other._type == RuntimeValueType.Null)
            return _type == other._type;

        // Same type comparison
        if (_type == other._type)
            return StrictEquals(other);

        // String comparison (case-insensitive in IntMUD)
        if (_type == RuntimeValueType.String || other._type == RuntimeValueType.String)
            return string.Equals(AsString(), other.AsString(), StringComparison.OrdinalIgnoreCase);

        // Numeric comparison
        return AsDouble() == other.AsDouble();
    }

    public override bool Equals(object? obj) => obj is RuntimeValue other && Equals(other);

    public override int GetHashCode() => _type switch
    {
        RuntimeValueType.Null => 0,
        RuntimeValueType.Integer => _intValue.GetHashCode(),
        RuntimeValueType.Double => _doubleValue.GetHashCode(),
        RuntimeValueType.String => _stringValue?.GetHashCode() ?? 0,
        RuntimeValueType.Object => _objectValue?.GetHashCode() ?? 0,
        RuntimeValueType.Array => _objectValue?.GetHashCode() ?? 0,
        _ => 0
    };

    public override string ToString() => $"[{_type}] {AsString()}";

    // Helper methods for hex string operations
    private static string BitwiseTextOp(string a, string b, Func<int, int, int> op)
    {
        var maxLen = Math.Max(a.Length, b.Length);
        var result = new char[maxLen];

        for (int i = 0; i < maxLen; i++)
        {
            var aIdx = a.Length - maxLen + i;
            var bIdx = b.Length - maxLen + i;
            var aVal = aIdx >= 0 ? HexCharToInt(a[aIdx]) : 0;
            var bVal = bIdx >= 0 ? HexCharToInt(b[bIdx]) : 0;
            result[i] = IntToHexChar(op(aVal, bVal) & 0xF);
        }

        return new string(result);
    }

    private static int HexCharToInt(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => 0
    };

    private static char IntToHexChar(int v) => v switch
    {
        >= 0 and <= 9 => (char)('0' + v),
        >= 10 and <= 15 => (char)('a' + v - 10),
        _ => '0'
    };

    private static string ShiftHexString(string hex, int bits, bool left)
    {
        if (string.IsNullOrEmpty(hex) || bits == 0) return hex;

        // Convert hex string to big integer representation
        var bytes = new List<int>();
        foreach (var c in hex)
            bytes.Add(HexCharToInt(c));

        if (left)
        {
            // Shift left: add zeros at the end
            var nibbleShift = bits / 4;
            var bitShift = bits % 4;

            for (int i = 0; i < nibbleShift; i++)
                bytes.Add(0);

            if (bitShift > 0)
            {
                int carry = 0;
                for (int i = bytes.Count - 1; i >= 0; i--)
                {
                    var newVal = (bytes[i] << bitShift) | carry;
                    bytes[i] = newVal & 0xF;
                    carry = (newVal >> 4) & 0xF;
                }
                if (carry > 0)
                    bytes.Insert(0, carry);
            }
        }
        else
        {
            // Shift right: remove from the end
            var nibbleShift = bits / 4;
            var bitShift = bits % 4;

            for (int i = 0; i < nibbleShift && bytes.Count > 0; i++)
                bytes.RemoveAt(bytes.Count - 1);

            if (bitShift > 0 && bytes.Count > 0)
            {
                int carry = 0;
                for (int i = 0; i < bytes.Count; i++)
                {
                    var newVal = bytes[i] | (carry << 4);
                    bytes[i] = (newVal >> bitShift) & 0xF;
                    carry = newVal & ((1 << bitShift) - 1);
                }
            }
        }

        // Remove leading zeros
        while (bytes.Count > 1 && bytes[0] == 0)
            bytes.RemoveAt(0);

        return string.Concat(bytes.Select(IntToHexChar));
    }
}
