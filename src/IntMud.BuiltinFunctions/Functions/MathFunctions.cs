using IntMud.Runtime.Values;
using System.Text;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Math functions - Complete implementation matching original IntMUD.
/// </summary>
public class MathFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        // Integer functions
        "int", "intabs", "intpos", "intdiv", "intmax", "intmin", "intmedia",
        // Ceiling/floor
        "matcima", "matbaixo",
        // Trigonometry
        "matsin", "matcos", "mattan", "matasin", "matacos", "matatan",
        // Math
        "matexp", "matlog", "matlog10", "matpot", "mathpow", "matraiz", "matpi",
        // Angle conversion
        "matrad", "matdeg",
        // Random
        "matrand", "matrandom", "rand",
        // Bit functions
        "intbit", "intbith", "intbiti",
        "txtbit", "txtbith", "txthex"
    ];

    private readonly Random _random = new();

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.FromInt(0);

        var funcName = args[0].AsString().ToLowerInvariant();

        return funcName switch
        {
            // Integer functions
            "int" => ExecuteInt(args),
            "intabs" => ExecuteIntAbs(args),
            "intpos" => ExecuteIntPos(args),
            "intdiv" => ExecuteIntDiv(args),
            "intmax" => ExecuteIntMax(args),
            "intmin" => ExecuteIntMin(args),
            "intmedia" => ExecuteIntMedia(args),

            // Ceiling/floor
            "matcima" => ExecuteMatCima(args),
            "matbaixo" => ExecuteMatBaixo(args),

            // Trigonometry
            "matsin" => ExecuteMatSin(args),
            "matcos" => ExecuteMatCos(args),
            "mattan" => ExecuteMatTan(args),
            "matasin" => ExecuteMatAsin(args),
            "matacos" => ExecuteMatAcos(args),
            "matatan" => ExecuteMatAtan(args),

            // Math
            "matexp" => ExecuteMatExp(args),
            "matlog" => ExecuteMatLog(args),
            "matlog10" => ExecuteMatLog10(args),
            "matpot" or "mathpow" => ExecuteMatPot(args),
            "matraiz" => ExecuteMatRaiz(args),
            "matpi" => ExecuteMatPi(),

            // Angle conversion
            "matrad" => ExecuteMatRad(args),
            "matdeg" => ExecuteMatDeg(args),

            // Random
            "matrand" or "matrandom" => ExecuteMatRand(args),
            "rand" => ExecuteRand(args),

            // Bit functions
            "intbit" => ExecuteIntBit(args),
            "intbith" => ExecuteIntBitH(args),
            "intbiti" => ExecuteIntBitI(args),
            "txtbit" => ExecuteTxtBit(args),
            "txtbith" => ExecuteTxtBitH(args),
            "txthex" => ExecuteTxtHex(args),

            _ => RuntimeValue.FromInt(0)
        };
    }

    #region Integer Functions

    /// <summary>
    /// int(value) - Convert to integer (round).
    /// </summary>
    private static RuntimeValue ExecuteInt(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromInt((long)Math.Round(value));
    }

    /// <summary>
    /// intabs(value) - Absolute value.
    /// </summary>
    private static RuntimeValue ExecuteIntAbs(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Abs(value));
    }

    /// <summary>
    /// intpos(value) - Positive or zero.
    /// </summary>
    private static RuntimeValue ExecuteIntPos(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(value < 0 ? 0 : value);
    }

    /// <summary>
    /// intdiv(value) - Truncate to integer.
    /// </summary>
    private static RuntimeValue ExecuteIntDiv(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromInt((long)Math.Truncate(value));
    }

    /// <summary>
    /// intmax(values...) - Maximum value.
    /// </summary>
    private static RuntimeValue ExecuteIntMax(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromInt(0);

        double max = args[1].AsDouble();
        for (int i = 2; i < args.Length; i++)
        {
            double val = args[i].AsDouble();
            if (val > max)
                max = val;
        }

        return RuntimeValue.FromDouble(max);
    }

    /// <summary>
    /// intmin(values...) - Minimum value.
    /// </summary>
    private static RuntimeValue ExecuteIntMin(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromInt(0);

        double min = args[1].AsDouble();
        for (int i = 2; i < args.Length; i++)
        {
            double val = args[i].AsDouble();
            if (val < min)
                min = val;
        }

        return RuntimeValue.FromDouble(min);
    }

    /// <summary>
    /// intmedia(values...) - Average value.
    /// </summary>
    private static RuntimeValue ExecuteIntMedia(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        double sum = 0;
        int count = 0;

        for (int i = 1; i < args.Length; i++)
        {
            sum += args[i].AsDouble();
            count++;
        }

        return count > 0 ? RuntimeValue.FromDouble(sum / count) : RuntimeValue.FromInt(0);
    }

    #endregion

    #region Ceiling/Floor

    /// <summary>
    /// matcima(value) - Ceiling (round up).
    /// </summary>
    private static RuntimeValue ExecuteMatCima(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Ceiling(value));
    }

    /// <summary>
    /// matbaixo(value) - Floor (round down).
    /// </summary>
    private static RuntimeValue ExecuteMatBaixo(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Floor(value));
    }

    #endregion

    #region Trigonometry

    /// <summary>
    /// matsin(angle) - Sine (radians).
    /// </summary>
    private static RuntimeValue ExecuteMatSin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Sin(value));
    }

    /// <summary>
    /// matcos(angle) - Cosine (radians).
    /// </summary>
    private static RuntimeValue ExecuteMatCos(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Cos(value));
    }

    /// <summary>
    /// mattan(angle) - Tangent (radians).
    /// </summary>
    private static RuntimeValue ExecuteMatTan(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Tan(value));
    }

    /// <summary>
    /// matasin(value) - Arc sine.
    /// </summary>
    private static RuntimeValue ExecuteMatAsin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Asin(value));
    }

    /// <summary>
    /// matacos(value) - Arc cosine.
    /// </summary>
    private static RuntimeValue ExecuteMatAcos(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Acos(value));
    }

    /// <summary>
    /// matatan(value) - Arc tangent.
    /// </summary>
    private static RuntimeValue ExecuteMatAtan(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Atan(value));
    }

    #endregion

    #region Math Functions

    /// <summary>
    /// matexp(value) - e^x.
    /// </summary>
    private static RuntimeValue ExecuteMatExp(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(1);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(Math.Exp(value));
    }

    /// <summary>
    /// matlog(value) - Natural logarithm.
    /// </summary>
    private static RuntimeValue ExecuteMatLog(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        if (value <= 0)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Log(value));
    }

    /// <summary>
    /// matlog10(value) - Base-10 logarithm.
    /// </summary>
    private static RuntimeValue ExecuteMatLog10(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        if (value <= 0)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Log10(value));
    }

    /// <summary>
    /// matpot(base, exponent) or mathpow(base, exponent) - Power.
    /// </summary>
    private static RuntimeValue ExecuteMatPot(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromDouble(1);

        return RuntimeValue.FromDouble(Math.Pow(args[1].AsDouble(), args[2].AsDouble()));
    }

    /// <summary>
    /// matraiz(value) - Square root.
    /// </summary>
    private static RuntimeValue ExecuteMatRaiz(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        if (value < 0)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Sqrt(value));
    }

    /// <summary>
    /// matpi - Pi constant.
    /// </summary>
    private static RuntimeValue ExecuteMatPi()
    {
        return RuntimeValue.FromDouble(Math.PI);
    }

    #endregion

    #region Angle Conversion

    /// <summary>
    /// matrad(degrees) - Degrees to radians.
    /// </summary>
    private static RuntimeValue ExecuteMatRad(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(value / 180.0 * Math.PI);
    }

    /// <summary>
    /// matdeg(radians) - Radians to degrees.
    /// </summary>
    private static RuntimeValue ExecuteMatDeg(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = GetSumOfValues(args, 1);
        return RuntimeValue.FromDouble(value / Math.PI * 180.0);
    }

    #endregion

    #region Random Functions

    /// <summary>
    /// matrand(max) or matrandom(max) - Random number 0 to max-1.
    /// </summary>
    private RuntimeValue ExecuteMatRand(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(_random.Next());

        var max = (int)args[1].AsInt();
        if (max <= 0)
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(_random.Next(max));
    }

    /// <summary>
    /// rand(max) or rand(min, max) - Random number with optional range.
    /// If text is passed, shuffles the text.
    /// </summary>
    private RuntimeValue ExecuteRand(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(_random.Next());

        // If first arg is text, shuffle it
        if (args[1].Type == RuntimeValueType.String)
        {
            var text = args[1].AsString();
            var chars = text.ToCharArray();
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return RuntimeValue.FromString(new string(chars));
        }

        // Single argument - random 0 to max-1
        if (args.Length == 2)
        {
            var max = (int)args[1].AsInt();
            if (max <= 0)
                return RuntimeValue.FromInt(0);
            return RuntimeValue.FromInt(_random.Next(max));
        }

        // Two arguments - random min to max (inclusive)
        var min = (int)args[1].AsInt();
        var max2 = (int)args[2].AsInt();
        if (min > max2)
            (min, max2) = (max2, min);
        return RuntimeValue.FromInt(_random.Next(min, max2 + 1));
    }

    #endregion

    #region Bit Functions

    /// <summary>
    /// intbit(strings...) - Parse bit numbers from strings and OR them together.
    /// "0 3 5" -> bit 0 | bit 3 | bit 5 = 1 | 8 | 32 = 41
    /// </summary>
    private static RuntimeValue ExecuteIntBit(RuntimeValue[] args)
    {
        int result = 0;

        for (int i = 1; i < args.Length; i++)
        {
            var text = args[i].AsString();
            int pos = 0;

            while (pos < text.Length)
            {
                // Skip non-digits
                while (pos < text.Length && (text[pos] < '0' || text[pos] > '9'))
                    pos++;

                if (pos >= text.Length)
                    break;

                // Parse number
                int value = 0;
                while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9')
                {
                    value = value * 10 + (text[pos] - '0');
                    if (value >= 32)
                        break;
                    pos++;
                }

                // Set bit if valid
                if (value < 32)
                    result |= 1 << value;
            }
        }

        return RuntimeValue.FromInt(result);
    }

    /// <summary>
    /// intbith(hexStrings...) - Parse hex strings and convert to bit mask.
    /// </summary>
    private static RuntimeValue ExecuteIntBitH(RuntimeValue[] args)
    {
        var sb = new StringBuilder();

        for (int i = 1; i < args.Length; i++)
        {
            var text = args[i].AsString();
            int pos = 0;

            while (pos < text.Length)
            {
                // Skip non-digits
                while (pos < text.Length && (text[pos] < '0' || text[pos] > '9'))
                    pos++;

                if (pos >= text.Length)
                    break;

                // Parse number
                int value = 0;
                while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9')
                {
                    value = value * 10 + (text[pos] - '0');
                    if (value >= 32767)
                        break;
                    pos++;
                }

                // Build hex result
                if (value < 32767)
                {
                    int nibblePos = value / 4;
                    int bit = value % 4;

                    // Ensure StringBuilder is long enough
                    while (sb.Length <= nibblePos)
                        sb.Insert(0, '0');

                    int idx = sb.Length - 1 - nibblePos;
                    char c = sb[idx];
                    int nibble = c >= '0' && c <= '9' ? c - '0' :
                                 c >= 'A' && c <= 'F' ? c - 'A' + 10 :
                                 c >= 'a' && c <= 'f' ? c - 'a' + 10 : 0;
                    nibble |= 1 << bit;
                    sb[idx] = (char)(nibble < 10 ? nibble + '0' : nibble - 10 + 'A');
                }
            }
        }

        // Convert hex string to int
        if (sb.Length == 0)
            return RuntimeValue.FromInt(0);

        try
        {
            return RuntimeValue.FromInt(Convert.ToInt64(sb.ToString(), 16));
        }
        catch
        {
            return RuntimeValue.FromInt(0);
        }
    }

    /// <summary>
    /// intbiti(bitfield, bitNumber) - Check if bit is set.
    /// </summary>
    private static RuntimeValue ExecuteIntBitI(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(0);

        var bitfield = (int)args[1].AsInt();
        var bitNumber = (int)args[2].AsInt();

        if (bitNumber < 0 || bitNumber >= 32)
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt((bitfield & (1 << bitNumber)) != 0 ? 1 : 0);
    }

    /// <summary>
    /// txtbit(number, separator?) - Convert bit mask to list of bit positions.
    /// 41 -> "0 3 5"
    /// </summary>
    private static RuntimeValue ExecuteTxtBit(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        int num = (int)args[1].AsInt();
        string separator = args.Length > 2 ? args[2].AsString() : " ";

        var parts = new List<string>();
        for (int bit = 0; bit < 32 && num != 0; bit++)
        {
            if ((num & 1) != 0)
                parts.Add(bit.ToString());
            num >>= 1;
        }

        return RuntimeValue.FromString(string.Join(separator, parts));
    }

    /// <summary>
    /// txtbith(hexString, separator?) - Convert hex to list of bit positions.
    /// </summary>
    private static RuntimeValue ExecuteTxtBitH(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var hexStr = args[1].AsString();
        string separator = args.Length > 2 ? args[2].AsString() : " ";

        var parts = new List<string>();
        int bit = 0;

        // Process from right to left
        for (int i = hexStr.Length - 1; i >= 0; i--)
        {
            char c = hexStr[i];
            int nibble;
            if (c >= '0' && c <= '9')
                nibble = c - '0';
            else if (c >= 'A' && c <= 'F')
                nibble = c - 'A' + 10;
            else if (c >= 'a' && c <= 'f')
                nibble = c - 'a' + 10;
            else
                continue;

            for (int b = 0; b < 4; b++, bit++)
            {
                if ((nibble & (1 << b)) != 0)
                    parts.Add(bit.ToString());
            }
        }

        return RuntimeValue.FromString(string.Join(separator, parts));
    }

    /// <summary>
    /// txthex(hexString, minLength) - Pad hex string to minimum length.
    /// </summary>
    private static RuntimeValue ExecuteTxtHex(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var hexStr = args[1].AsString();
        int minLength = (int)args[2].AsInt();

        if (minLength <= 0)
            return RuntimeValue.FromString("");

        if (hexStr.Length >= minLength)
            return RuntimeValue.FromString(hexStr[^minLength..]);

        return RuntimeValue.FromString(hexStr.PadLeft(minLength, '0'));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Sum all values starting from index.
    /// </summary>
    private static double GetSumOfValues(RuntimeValue[] args, int startIndex)
    {
        double sum = 0;
        for (int i = startIndex; i < args.Length; i++)
            sum += args[i].AsDouble();
        return sum;
    }

    #endregion
}
