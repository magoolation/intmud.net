using IntMud.Runtime.Values;
using System.Globalization;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Type conversion functions.
/// </summary>
public class ConversionFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        "real", "hex", "bin", "chr", "asc",
        "verdade", "falso"
    ];

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.FromInt(0);

        var funcName = args[0].AsString().ToLowerInvariant();

        return funcName switch
        {
            "real" => ExecuteReal(args),
            "hex" => ExecuteHex(args),
            "bin" => ExecuteBin(args),
            "chr" => ExecuteChr(args),
            "asc" => ExecuteAsc(args),
            "verdade" => RuntimeValue.FromInt(1),
            "falso" => RuntimeValue.FromInt(0),
            _ => RuntimeValue.FromInt(0)
        };
    }

    /// <summary>
    /// real(value) - Convert to float.
    /// </summary>
    private static RuntimeValue ExecuteReal(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromDouble(0);

        return RuntimeValue.FromDouble(args[1].AsDouble());
    }

    /// <summary>
    /// hex(value) - Convert to hexadecimal string.
    /// </summary>
    private static RuntimeValue ExecuteHex(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("0");

        var value = args[1].AsInt();
        return RuntimeValue.FromString(value.ToString("X"));
    }

    /// <summary>
    /// bin(value) - Convert to binary string.
    /// </summary>
    private static RuntimeValue ExecuteBin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("0");

        var value = args[1].AsInt();
        return RuntimeValue.FromString(Convert.ToString(value, 2));
    }

    /// <summary>
    /// chr(code) - Character from ASCII code.
    /// </summary>
    private static RuntimeValue ExecuteChr(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var code = (int)args[1].AsInt();
        if (code < 0 || code > 0xFFFF)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(((char)code).ToString());
    }

    /// <summary>
    /// asc(text) - ASCII code of first character.
    /// </summary>
    private static RuntimeValue ExecuteAsc(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var text = args[1].AsString();
        if (string.IsNullOrEmpty(text))
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(text[0]);
    }
}
