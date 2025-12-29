using IntMud.Runtime.Values;
using System.Text;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Text manipulation functions.
/// </summary>
public class TextFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        "txt", "txt1", "txt2",
        "txtmai", "txtmin", "txtsub", "txtproc", "txtroca",
        "txtlen", "txtvazio", "txtnum", "txttrim",
        "txtsepara", "txtletra", "txtpalav", "txtlinha"
    ];

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.FromString("");

        var funcName = args[0].AsString().ToLowerInvariant();

        return funcName switch
        {
            "txt" => ExecuteTxt(args),
            "txt1" => ExecuteTxt1(args),
            "txt2" => ExecuteTxt2(args),
            "txtmai" => ExecuteTxtMai(args),
            "txtmin" => ExecuteTxtMin(args),
            "txtsub" => ExecuteTxtSub(args),
            "txtproc" => ExecuteTxtProc(args),
            "txtroca" => ExecuteTxtRoca(args),
            "txtlen" => ExecuteTxtLen(args),
            "txtvazio" => ExecuteTxtVazio(args),
            "txtnum" => ExecuteTxtNum(args),
            "txttrim" => ExecuteTxtTrim(args),
            "txtsepara" => ExecuteTxtSepara(args),
            "txtletra" => ExecuteTxtLetra(args),
            "txtpalav" => ExecuteTxtPalav(args),
            "txtlinha" => ExecuteTxtLinha(args),
            _ => RuntimeValue.FromString("")
        };
    }

    /// <summary>
    /// txt(value) - Convert to string.
    /// </summary>
    private static RuntimeValue ExecuteTxt(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(args[1].AsString());
    }

    /// <summary>
    /// txt1(value) - Convert to string with 1 character.
    /// </summary>
    private static RuntimeValue ExecuteTxt1(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var str = args[1].AsString();
        return RuntimeValue.FromString(str.Length > 0 ? str[0].ToString() : "");
    }

    /// <summary>
    /// txt2(value, length) - Convert to string with specified length.
    /// </summary>
    private static RuntimeValue ExecuteTxt2(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var str = args[1].AsString();
        var length = (int)args[2].AsInt();

        if (length <= 0)
            return RuntimeValue.FromString("");

        if (str.Length >= length)
            return RuntimeValue.FromString(str[..length]);

        return RuntimeValue.FromString(str.PadRight(length));
    }

    /// <summary>
    /// txtmai(text) - Convert to uppercase.
    /// </summary>
    private static RuntimeValue ExecuteTxtMai(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(args[1].AsString().ToUpperInvariant());
    }

    /// <summary>
    /// txtmin(text) - Convert to lowercase.
    /// </summary>
    private static RuntimeValue ExecuteTxtMin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(args[1].AsString().ToLowerInvariant());
    }

    /// <summary>
    /// txtsub(text, start, length) - Get substring.
    /// </summary>
    private static RuntimeValue ExecuteTxtSub(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var start = (int)args[2].AsInt();
        var length = args.Length > 3 ? (int)args[3].AsInt() : text.Length - start;

        // IntMUD uses 0-based indexing
        if (start < 0 || start >= text.Length)
            return RuntimeValue.FromString("");

        length = Math.Min(length, text.Length - start);
        if (length <= 0)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(text.Substring(start, length));
    }

    /// <summary>
    /// txtproc(text, search) - Find substring position.
    /// </summary>
    private static RuntimeValue ExecuteTxtProc(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(-1);

        var text = args[1].AsString();
        var search = args[2].AsString();

        return RuntimeValue.FromInt(text.IndexOf(search, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// txtroca(text, search, replace) - Replace substring.
    /// </summary>
    private static RuntimeValue ExecuteTxtRoca(RuntimeValue[] args)
    {
        if (args.Length < 4)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromString("");

        var text = args[1].AsString();
        var search = args[2].AsString();
        var replace = args[3].AsString();

        return RuntimeValue.FromString(text.Replace(search, replace, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// txtlen(text) - Get string length.
    /// </summary>
    private static RuntimeValue ExecuteTxtLen(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(args[1].AsString().Length);
    }

    /// <summary>
    /// txtvazio(text) - Check if string is empty.
    /// </summary>
    private static RuntimeValue ExecuteTxtVazio(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(1);

        return RuntimeValue.FromInt(string.IsNullOrWhiteSpace(args[1].AsString()) ? 1 : 0);
    }

    /// <summary>
    /// txtnum(text) - Check if string is numeric.
    /// </summary>
    private static RuntimeValue ExecuteTxtNum(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var text = args[1].AsString().Trim();
        return RuntimeValue.FromInt(double.TryParse(text, out _) ? 1 : 0);
    }

    /// <summary>
    /// txttrim(text) - Trim whitespace.
    /// </summary>
    private static RuntimeValue ExecuteTxtTrim(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(args[1].AsString().Trim());
    }

    /// <summary>
    /// txtsepara(text, separator, index) - Split and get part.
    /// </summary>
    private static RuntimeValue ExecuteTxtSepara(RuntimeValue[] args)
    {
        if (args.Length < 4)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var separator = args[2].AsString();
        var index = (int)args[3].AsInt();

        var parts = text.Split(separator);
        if (index < 0 || index >= parts.Length)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(parts[index]);
    }

    /// <summary>
    /// txtletra(text, index) - Get character at index.
    /// </summary>
    private static RuntimeValue ExecuteTxtLetra(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var index = (int)args[2].AsInt();

        if (index < 0 || index >= text.Length)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(text[index].ToString());
    }

    /// <summary>
    /// txtpalav(text, index) - Get word at index.
    /// </summary>
    private static RuntimeValue ExecuteTxtPalav(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var index = (int)args[2].AsInt();

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (index < 0 || index >= words.Length)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(words[index]);
    }

    /// <summary>
    /// txtlinha(text, index) - Get line at index.
    /// </summary>
    private static RuntimeValue ExecuteTxtLinha(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var index = (int)args[2].AsInt();

        var lines = text.Split('\n');
        if (index < 0 || index >= lines.Length)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(lines[index].TrimEnd('\r'));
    }
}
