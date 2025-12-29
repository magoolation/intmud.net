using IntMud.Runtime.Values;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Math functions.
/// </summary>
public class MathFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        "int", "intabs", "intpos", "intdiv", "intmax", "intmin", "intmedia",
        "matsin", "matcos", "mattan", "matasin", "matacos", "matatan",
        "matexp", "matlog", "matlog10", "matpot", "matraiz", "matpi",
        "matrand", "matrandom"
    ];

    private readonly Random _random = new();

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.FromInt(0);

        var funcName = args[0].AsString().ToLowerInvariant();

        return funcName switch
        {
            "int" => ExecuteInt(args),
            "intabs" => ExecuteIntAbs(args),
            "intpos" => ExecuteIntPos(args),
            "intdiv" => ExecuteIntDiv(args),
            "intmax" => ExecuteIntMax(args),
            "intmin" => ExecuteIntMin(args),
            "intmedia" => ExecuteIntMedia(args),
            "matsin" => ExecuteMatSin(args),
            "matcos" => ExecuteMatCos(args),
            "mattan" => ExecuteMatTan(args),
            "matasin" => ExecuteMatAsin(args),
            "matacos" => ExecuteMatAcos(args),
            "matatan" => ExecuteMatAtan(args),
            "matexp" => ExecuteMatExp(args),
            "matlog" => ExecuteMatLog(args),
            "matlog10" => ExecuteMatLog10(args),
            "matpot" => ExecuteMatPot(args),
            "matraiz" => ExecuteMatRaiz(args),
            "matpi" => ExecuteMatPi(),
            "matrand" or "matrandom" => ExecuteMatRand(args),
            _ => RuntimeValue.FromInt(0)
        };
    }

    /// <summary>
    /// int(value) - Convert to integer.
    /// </summary>
    private static RuntimeValue ExecuteInt(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(args[1].AsInt());
    }

    /// <summary>
    /// intabs(value) - Absolute value.
    /// </summary>
    private static RuntimeValue ExecuteIntAbs(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(Math.Abs(args[1].AsInt()));
    }

    /// <summary>
    /// intpos(value) - Positive or zero.
    /// </summary>
    private static RuntimeValue ExecuteIntPos(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var value = args[1].AsInt();
        return RuntimeValue.FromInt(value > 0 ? value : 0);
    }

    /// <summary>
    /// intdiv(a, b) - Integer division.
    /// </summary>
    private static RuntimeValue ExecuteIntDiv(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(0);

        var a = args[1].AsInt();
        var b = args[2].AsInt();

        if (b == 0)
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(a / b);
    }

    /// <summary>
    /// intmax(a, b) - Maximum value.
    /// </summary>
    private static RuntimeValue ExecuteIntMax(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(Math.Max(args[1].AsInt(), args[2].AsInt()));
    }

    /// <summary>
    /// intmin(a, b) - Minimum value.
    /// </summary>
    private static RuntimeValue ExecuteIntMin(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(Math.Min(args[1].AsInt(), args[2].AsInt()));
    }

    /// <summary>
    /// intmedia(values...) - Average value.
    /// </summary>
    private static RuntimeValue ExecuteIntMedia(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        long sum = 0;
        int count = 0;

        for (int i = 1; i < args.Length; i++)
        {
            sum += args[i].AsInt();
            count++;
        }

        return count > 0 ? RuntimeValue.FromInt(sum / count) : RuntimeValue.FromInt(0);
    }

    /// <summary>
    /// matsin(angle) - Sine (radians).
    /// </summary>
    private static RuntimeValue ExecuteMatSin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Sin(args[1].AsDouble()));
    }

    /// <summary>
    /// matcos(angle) - Cosine (radians).
    /// </summary>
    private static RuntimeValue ExecuteMatCos(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Cos(args[1].AsDouble()));
    }

    /// <summary>
    /// mattan(angle) - Tangent (radians).
    /// </summary>
    private static RuntimeValue ExecuteMatTan(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Tan(args[1].AsDouble()));
    }

    /// <summary>
    /// matasin(value) - Arc sine.
    /// </summary>
    private static RuntimeValue ExecuteMatAsin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Asin(args[1].AsDouble()));
    }

    /// <summary>
    /// matacos(value) - Arc cosine.
    /// </summary>
    private static RuntimeValue ExecuteMatAcos(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Acos(args[1].AsDouble()));
    }

    /// <summary>
    /// matatan(value) - Arc tangent.
    /// </summary>
    private static RuntimeValue ExecuteMatAtan(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Atan(args[1].AsDouble()));
    }

    /// <summary>
    /// matexp(value) - e^x.
    /// </summary>
    private static RuntimeValue ExecuteMatExp(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(1);

        return RuntimeValue.FromDouble(Math.Exp(args[1].AsDouble()));
    }

    /// <summary>
    /// matlog(value) - Natural logarithm.
    /// </summary>
    private static RuntimeValue ExecuteMatLog(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        var value = args[1].AsDouble();
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

        var value = args[1].AsDouble();
        if (value <= 0)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(Math.Log10(value));
    }

    /// <summary>
    /// matpot(base, exponent) - Power.
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

        var value = args[1].AsDouble();
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

    /// <summary>
    /// matrand(max) or matrandom(max) - Random number.
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
}
