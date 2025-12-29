using System.Runtime.InteropServices;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for inttempo (timer) variables.
/// Stores a timestamp for elapsed time calculations.
/// </summary>
public sealed class IntTempoHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.IntTempo;
    public override string TypeName => "inttempo";
    public override VariableType RuntimeType => VariableType.Int;

    // Store: start time (8 bytes) + interval (4 bytes) + enabled (1 byte)
    public override int GetSize(ReadOnlySpan<byte> instruction) => 13;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        // Initialize with current time
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        MemoryMarshal.Write(memory, in now);
        memory[8..12].Clear(); // interval = 0
        memory[12] = 0; // disabled
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        return IsEnabled(memory) && HasElapsed(memory);
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        return GetElapsedMilliseconds(memory);
    }

    public override double GetDouble(ReadOnlySpan<byte> memory)
    {
        return GetElapsedMilliseconds(memory);
    }

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var elapsed = GetElapsedMilliseconds(memory);
        var interval = GetInterval(memory);
        return $"{elapsed}ms/{interval}ms";
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        SetInterval(memory, value);
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        SetInterval(memory, (int)value);
    }

    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source[..13].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetElapsedMilliseconds(left).CompareTo(GetElapsedMilliseconds(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetElapsedMilliseconds(left) == GetElapsedMilliseconds(right);
    }

    private static long GetStartTime(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<long>(memory);
    }

    private static void SetStartTime(Span<byte> memory, long value)
    {
        MemoryMarshal.Write(memory, in value);
    }

    private static int GetInterval(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory[8..]);
    }

    private static void SetInterval(Span<byte> memory, int value)
    {
        MemoryMarshal.Write(memory[8..], in value);
    }

    private static bool IsEnabled(ReadOnlySpan<byte> memory)
    {
        return memory[12] != 0;
    }

    private static void SetEnabled(Span<byte> memory, bool value)
    {
        memory[12] = value ? (byte)1 : (byte)0;
    }

    private static int GetElapsedMilliseconds(ReadOnlySpan<byte> memory)
    {
        var start = GetStartTime(memory);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return (int)(now - start);
    }

    private static bool HasElapsed(ReadOnlySpan<byte> memory)
    {
        var interval = GetInterval(memory);
        if (interval <= 0)
            return false;
        return GetElapsedMilliseconds(memory) >= interval;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "iniciar":
            case "start":
                SetStartTime(memory, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                SetEnabled(memory, true);
                return true;

            case "parar":
            case "stop":
                SetEnabled(memory, false);
                return true;

            case "reiniciar":
            case "reset":
                SetStartTime(memory, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return true;

            case "intervalo":
            case "interval":
                if (context.ArgumentCount > 0)
                    SetInterval(memory, context.GetIntArgument(0));
                else
                    context.SetReturnInt(GetInterval(memory));
                return true;

            case "passado":
            case "elapsed":
                context.SetReturnInt(GetElapsedMilliseconds(memory));
                return true;

            case "ativo":
            case "active":
                context.SetReturnBool(IsEnabled(memory));
                return true;

            case "pronto":
            case "ready":
                context.SetReturnBool(HasElapsed(memory));
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for intexec (execution counter) variables.
/// Counts down and triggers when reaches zero.
/// </summary>
public sealed class IntExecHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.IntExec;
    public override string TypeName => "intexec";
    public override VariableType RuntimeType => VariableType.Int;

    // Store: current count (4 bytes) + initial count (4 bytes)
    public override int GetSize(ReadOnlySpan<byte> instruction) => 8;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        return GetCount(memory) == 0;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        return GetCount(memory);
    }

    public override double GetDouble(ReadOnlySpan<byte> memory)
    {
        return GetCount(memory);
    }

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        return GetCount(memory).ToString();
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        SetCount(memory, value);
        SetInitialCount(memory, value);
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        SetInt(memory, (int)value);
    }

    public override void SetText(Span<byte> memory, string value)
    {
        if (int.TryParse(value, out var v))
            SetInt(memory, v);
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source[..8].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetCount(left).CompareTo(GetCount(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetCount(left) == GetCount(right);
    }

    private static int GetCount(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory);
    }

    private static void SetCount(Span<byte> memory, int value)
    {
        MemoryMarshal.Write(memory, in value);
    }

    private static int GetInitialCount(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory[4..]);
    }

    private static void SetInitialCount(Span<byte> memory, int value)
    {
        MemoryMarshal.Write(memory[4..], in value);
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "decrementar":
            case "decrement":
                var count = GetCount(memory);
                if (count > 0)
                    SetCount(memory, count - 1);
                context.SetReturnBool(GetCount(memory) == 0);
                return true;

            case "reiniciar":
            case "reset":
                SetCount(memory, GetInitialCount(memory));
                return true;

            case "valor":
            case "value":
                context.SetReturnInt(GetCount(memory));
                return true;

            case "pronto":
            case "ready":
                context.SetReturnBool(GetCount(memory) == 0);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for datahora (date/time) variables.
/// </summary>
public sealed class DataHoraHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.DataHora;
    public override string TypeName => "datahora";
    public override VariableType RuntimeType => VariableType.Int;

    // Store: Unix timestamp in milliseconds (8 bytes)
    public override int GetSize(ReadOnlySpan<byte> instruction) => 8;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        // Initialize with current time
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        MemoryMarshal.Write(memory, in now);
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        return GetTimestamp(memory) != 0;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        // Return Unix timestamp in seconds
        return (int)(GetTimestamp(memory) / 1000);
    }

    public override double GetDouble(ReadOnlySpan<byte> memory)
    {
        return GetTimestamp(memory);
    }

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var dt = GetDateTime(memory);
        return dt.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        // Set from Unix timestamp in seconds
        SetTimestamp(memory, (long)value * 1000);
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        SetTimestamp(memory, (long)value);
    }

    public override void SetText(Span<byte> memory, string value)
    {
        if (DateTime.TryParse(value, out var dt))
        {
            SetTimestamp(memory, new DateTimeOffset(dt).ToUnixTimeMilliseconds());
        }
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source[..8].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetTimestamp(left).CompareTo(GetTimestamp(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetTimestamp(left) == GetTimestamp(right);
    }

    private static long GetTimestamp(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<long>(memory);
    }

    private static void SetTimestamp(Span<byte> memory, long value)
    {
        MemoryMarshal.Write(memory, in value);
    }

    private static DateTime GetDateTime(ReadOnlySpan<byte> memory)
    {
        var timestamp = GetTimestamp(memory);
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var dt = GetDateTime(memory);

        switch (functionName.ToLowerInvariant())
        {
            case "agora":
            case "now":
                SetTimestamp(memory, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return true;

            case "ano":
            case "year":
                context.SetReturnInt(dt.Year);
                return true;

            case "mes":
            case "month":
                context.SetReturnInt(dt.Month);
                return true;

            case "dia":
            case "day":
                context.SetReturnInt(dt.Day);
                return true;

            case "hora":
            case "hour":
                context.SetReturnInt(dt.Hour);
                return true;

            case "minuto":
            case "minute":
                context.SetReturnInt(dt.Minute);
                return true;

            case "segundo":
            case "second":
                context.SetReturnInt(dt.Second);
                return true;

            case "diasemana":
            case "dayofweek":
                context.SetReturnInt((int)dt.DayOfWeek);
                return true;

            case "diaano":
            case "dayofyear":
                context.SetReturnInt(dt.DayOfYear);
                return true;

            case "formato":
            case "format":
                var format = context.GetStringArgument(0);
                context.SetReturnString(dt.ToString(format));
                return true;

            case "adicionar":
            case "add":
                var unit = context.GetStringArgument(0).ToLowerInvariant();
                var amount = context.GetIntArgument(1);
                var newDt = unit switch
                {
                    "ano" or "year" or "anos" or "years" => dt.AddYears(amount),
                    "mes" or "month" or "meses" or "months" => dt.AddMonths(amount),
                    "dia" or "day" or "dias" or "days" => dt.AddDays(amount),
                    "hora" or "hour" or "horas" or "hours" => dt.AddHours(amount),
                    "minuto" or "minute" or "minutos" or "minutes" => dt.AddMinutes(amount),
                    "segundo" or "second" or "segundos" or "seconds" => dt.AddSeconds(amount),
                    _ => dt
                };
                SetTimestamp(memory, new DateTimeOffset(newDt).ToUnixTimeMilliseconds());
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for intinc (incrementing counter) variables.
/// </summary>
public sealed class IntIncHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.IntInc;
    public override string TypeName => "intinc";
    public override VariableType RuntimeType => VariableType.Int;

    // Store: current value (4 bytes) + increment (4 bytes) + max (4 bytes)
    public override int GetSize(ReadOnlySpan<byte> instruction) => 12;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
        MemoryMarshal.Write(memory[4..], 1); // Default increment = 1
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var max = GetMax(memory);
        return max == 0 || GetValue(memory) < max;
    }

    public override int GetInt(ReadOnlySpan<byte> memory) => GetValue(memory);

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetValue(memory);

    public override string GetText(ReadOnlySpan<byte> memory) => GetValue(memory).ToString();

    public override void SetInt(Span<byte> memory, int value) => SetValue(memory, value);

    public override void SetDouble(Span<byte> memory, double value) => SetValue(memory, (int)value);

    public override void SetText(Span<byte> memory, string value)
    {
        if (int.TryParse(value, out var v))
            SetValue(memory, v);
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source[..12].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetValue(left).CompareTo(GetValue(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetValue(left) == GetValue(right);
    }

    private static int GetValue(ReadOnlySpan<byte> memory) => MemoryMarshal.Read<int>(memory);
    private static void SetValue(Span<byte> memory, int value) => MemoryMarshal.Write(memory, in value);
    private static int GetIncrement(ReadOnlySpan<byte> memory) => MemoryMarshal.Read<int>(memory[4..]);
    private static int GetMax(ReadOnlySpan<byte> memory) => MemoryMarshal.Read<int>(memory[8..]);

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "incrementar":
            case "increment":
                var val = GetValue(memory) + GetIncrement(memory);
                var max = GetMax(memory);
                if (max > 0 && val > max)
                    val = max;
                SetValue(memory, val);
                context.SetReturnInt(val);
                return true;

            case "reiniciar":
            case "reset":
                SetValue(memory, 0);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for intdec (decrementing counter) variables.
/// </summary>
public sealed class IntDecHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.IntDec;
    public override string TypeName => "intdec";
    public override VariableType RuntimeType => VariableType.Int;

    // Store: current value (4 bytes) + decrement (4 bytes) + min (4 bytes)
    public override int GetSize(ReadOnlySpan<byte> instruction) => 12;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
        MemoryMarshal.Write(memory[4..], 1); // Default decrement = 1
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        return GetValue(memory) > GetMin(memory);
    }

    public override int GetInt(ReadOnlySpan<byte> memory) => GetValue(memory);

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetValue(memory);

    public override string GetText(ReadOnlySpan<byte> memory) => GetValue(memory).ToString();

    public override void SetInt(Span<byte> memory, int value) => SetValue(memory, value);

    public override void SetDouble(Span<byte> memory, double value) => SetValue(memory, (int)value);

    public override void SetText(Span<byte> memory, string value)
    {
        if (int.TryParse(value, out var v))
            SetValue(memory, v);
    }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source[..12].CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetValue(left).CompareTo(GetValue(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetValue(left) == GetValue(right);
    }

    private static int GetValue(ReadOnlySpan<byte> memory) => MemoryMarshal.Read<int>(memory);
    private static void SetValue(Span<byte> memory, int value) => MemoryMarshal.Write(memory, in value);
    private static int GetDecrement(ReadOnlySpan<byte> memory) => MemoryMarshal.Read<int>(memory[4..]);
    private static int GetMin(ReadOnlySpan<byte> memory) => MemoryMarshal.Read<int>(memory[8..]);

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "decrementar":
            case "decrement":
                var val = GetValue(memory) - GetDecrement(memory);
                var min = GetMin(memory);
                if (val < min)
                    val = min;
                SetValue(memory, val);
                context.SetReturnInt(val);
                return true;

            case "reiniciar":
            case "reset":
                SetValue(memory, context.GetIntArgument(0));
                return true;

            default:
                return false;
        }
    }
}
