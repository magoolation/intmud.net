using IntMud.Runtime.Values;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Text manipulation functions - Complete implementation matching original IntMUD.
/// </summary>
public class TextFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        // Basic text functions
        "txt", "txt1", "txt2", "txtsub", "txtsublin", "txtfim",
        // Case conversion
        "txtmai", "txtmaiini", "txtmin", "txtmaimin",
        // Search and replace
        "txtproc", "txtprocmai", "txtprocdif",
        "txtproclin", "txtproclinmai", "txtproclindif",
        "txttroca", "txttrocamai", "txttrocadif",
        // Utility functions
        "txtlen", "txtvazio", "txtnum", "txttrim", "txtcor",
        "txtsepara", "txtletra", "txtpalav", "txtlinha",
        "txtrev", "txtesp", "txtrepete",
        // Encoding/decoding
        "txtcod", "txtdec", "txtvis", "txtinvis",
        "txturlcod", "txturldec",
        // Hash functions
        "txtsha1", "txtsha1bin", "txtmd5",
        // Special functions
        "txtnome", "txtfiltro", "txttipovar",
        "txte", "txts", "txtmudamai", "txtcopiamai",
        "txtremove", "txtconv", "txtchr",
        // Count functions
        "intsub", "intsublin", "intchr",
        // Distance functions
        "intdist", "intdistmai", "intdistdif",
        // Name/password functions
        "intnome", "intsenha"
    ];

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.FromString("");

        var funcName = args[0].AsString().ToLowerInvariant();

        return funcName switch
        {
            // Basic text functions
            "txt" => ExecuteTxt(args),
            "txt1" => ExecuteTxt1(args),
            "txt2" => ExecuteTxt2(args),
            "txtsub" => ExecuteTxtSub(args),
            "txtsublin" => ExecuteTxtSubLin(args),
            "txtfim" => ExecuteTxtFim(args),

            // Case conversion
            "txtmai" => ExecuteTxtMai(args),
            "txtmaiini" => ExecuteTxtMaiIni(args),
            "txtmin" => ExecuteTxtMin(args),
            "txtmaimin" => ExecuteTxtMaiMin(args),

            // Search functions
            "txtproc" => ExecuteTxtProc(args),
            "txtprocmai" => ExecuteTxtProcMai(args),
            "txtprocdif" => ExecuteTxtProcDif(args),
            "txtproclin" => ExecuteTxtProcLin(args),
            "txtproclinmai" => ExecuteTxtProcLinMai(args),
            "txtproclindif" => ExecuteTxtProcLinDif(args),

            // Replace functions
            "txttroca" => ExecuteTxtRoca(args),
            "txttrocamai" => ExecuteTxtRocaMai(args),
            "txttrocadif" => ExecuteTxtRocaDif(args),

            // Utility functions
            "txtlen" => ExecuteTxtLen(args),
            "txtvazio" => ExecuteTxtVazio(args),
            "txtnum" => ExecuteTxtNum(args),
            "txttrim" => ExecuteTxtTrim(args),
            "txtcor" => ExecuteTxtCor(args),
            "txtsepara" => ExecuteTxtSepara(args),
            "txtletra" => ExecuteTxtLetra(args),
            "txtpalav" => ExecuteTxtPalav(args),
            "txtlinha" => ExecuteTxtLinha(args),
            "txtrev" => ExecuteTxtRev(args),
            "txtesp" => ExecuteTxtEsp(args),
            "txtrepete" => ExecuteTxtRepete(args),

            // Encoding/decoding
            "txtcod" => ExecuteTxtCod(args),
            "txtdec" => ExecuteTxtDec(args),
            "txtvis" => ExecuteTxtVis(args),
            "txtinvis" => ExecuteTxtInvis(args),
            "txturlcod" => ExecuteTxtUrlCod(args),
            "txturldec" => ExecuteTxtUrlDec(args),

            // Hash functions
            "txtsha1" => ExecuteTxtSha1(args),
            "txtsha1bin" => ExecuteTxtSha1Bin(args),
            "txtmd5" => ExecuteTxtMd5(args),

            // Special functions
            "txtnome" => ExecuteTxtNome(args),
            "txtfiltro" => ExecuteTxtFiltro(args),
            "txttipovar" => ExecuteTxtTipoVar(args),
            "txte" => ExecuteTxtE(args),
            "txts" => ExecuteTxtS(args),
            "txtmudamai" => ExecuteTxtMudaMai(args),
            "txtcopiamai" => ExecuteTxtCopiaMai(args),
            "txtremove" => ExecuteTxtRemove(args),
            "txtconv" => ExecuteTxtConv(args),
            "txtchr" => ExecuteTxtChr(args),

            // Count functions
            "intsub" => ExecuteIntSub(args),
            "intsublin" => ExecuteIntSubLin(args),
            "intchr" => ExecuteIntChr(args),

            // Distance functions
            "intdist" => ExecuteIntDist(args),
            "intdistmai" => ExecuteIntDistMai(args),
            "intdistdif" => ExecuteIntDistDif(args),

            // Name/password functions
            "intnome" => ExecuteIntNome(args),
            "intsenha" => ExecuteIntSenha(args),

            _ => RuntimeValue.FromString("")
        };
    }

    #region Basic Text Functions

    /// <summary>
    /// txt(value, start?, length?) - Get substring by character position.
    /// </summary>
    private static RuntimeValue ExecuteTxt(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var start = args.Length > 2 ? (int)args[2].AsInt() : 0;
        var length = args.Length > 3 ? (int)args[3].AsInt() : text.Length;

        if (start < 0) start = 0;
        if (length <= 0 || start >= text.Length)
            return RuntimeValue.FromString("");

        length = Math.Min(length, text.Length - start);
        return RuntimeValue.FromString(text.Substring(start, length));
    }

    /// <summary>
    /// txt1(value) - Get first word (before first space).
    /// </summary>
    private static RuntimeValue ExecuteTxt1(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString().TrimStart();
        var spaceIndex = text.IndexOf(' ');

        return RuntimeValue.FromString(spaceIndex >= 0 ? text[..spaceIndex] : text);
    }

    /// <summary>
    /// txt2(value) - Get text after first word (after first space).
    /// </summary>
    private static RuntimeValue ExecuteTxt2(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString().TrimStart();
        var spaceIndex = text.IndexOf(' ');

        if (spaceIndex < 0)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(text[(spaceIndex + 1)..].TrimStart());
    }

    /// <summary>
    /// txtsub(text, wordIndex, wordCount?) - Get substring by word position.
    /// </summary>
    private static RuntimeValue ExecuteTxtSub(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var wordIndex = (int)args[2].AsInt();
        var wordCount = args.Length > 3 ? (int)args[3].AsInt() : int.MaxValue;

        if (wordCount <= 0)
            return RuntimeValue.FromString("");

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (wordIndex < 0 || wordIndex >= words.Length)
            return RuntimeValue.FromString("");

        var endIndex = Math.Min(wordIndex + wordCount, words.Length);
        return RuntimeValue.FromString(string.Join(" ", words[wordIndex..endIndex]));
    }

    /// <summary>
    /// txtsublin(text, lineIndex, lineCount?) - Get substring by line position.
    /// </summary>
    private static RuntimeValue ExecuteTxtSubLin(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var lineIndex = (int)args[2].AsInt();
        var lineCount = args.Length > 3 ? (int)args[3].AsInt() : int.MaxValue;

        if (lineCount <= 0)
            return RuntimeValue.FromString("");

        var lines = text.Split('\n');
        if (lineIndex < 0 || lineIndex >= lines.Length)
            return RuntimeValue.FromString("");

        var endIndex = Math.Min(lineIndex + lineCount, lines.Length);
        return RuntimeValue.FromString(string.Join("\n", lines[lineIndex..endIndex]));
    }

    /// <summary>
    /// txtfim(text, length) - Get last N characters.
    /// </summary>
    private static RuntimeValue ExecuteTxtFim(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var length = (int)args[2].AsInt();

        if (length <= 0)
            return RuntimeValue.FromString("");

        if (length >= text.Length)
            return RuntimeValue.FromString(text);

        return RuntimeValue.FromString(text[^length..]);
    }

    #endregion

    #region Case Conversion

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
    /// txtmaiini(text) - Capitalize first letter of each sentence.
    /// </summary>
    private static RuntimeValue ExecuteTxtMaiIni(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        if (string.IsNullOrEmpty(text))
            return RuntimeValue.FromString("");

        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;

        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
                if (c == '.')
                    capitalizeNext = true;
            }
        }

        return RuntimeValue.FromString(sb.ToString());
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
    /// txtmaimin(text) - Title case (capitalize first letter of each word).
    /// </summary>
    private static RuntimeValue ExecuteTxtMaiMin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        if (string.IsNullOrEmpty(text))
            return RuntimeValue.FromString("");

        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;

        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
                if (!char.IsLetterOrDigit(c))
                    capitalizeNext = true;
            }
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    #endregion

    #region Search Functions

    /// <summary>
    /// txtproc(text, search) - Find substring position (case-insensitive).
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
    /// txtprocmai(text, search) - Find substring position (case-sensitive uppercase).
    /// </summary>
    private static RuntimeValue ExecuteTxtProcMai(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(-1);

        var text = args[1].AsString().ToUpperInvariant();
        var search = args[2].AsString().ToUpperInvariant();

        return RuntimeValue.FromInt(text.IndexOf(search, StringComparison.Ordinal));
    }

    /// <summary>
    /// txtprocdif(text, search) - Find substring position (case-sensitive).
    /// </summary>
    private static RuntimeValue ExecuteTxtProcDif(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(-1);

        var text = args[1].AsString();
        var search = args[2].AsString();

        return RuntimeValue.FromInt(text.IndexOf(search, StringComparison.Ordinal));
    }

    /// <summary>
    /// txtproclin(text, search) - Find line containing substring (case-insensitive).
    /// </summary>
    private static RuntimeValue ExecuteTxtProcLin(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(-1);

        var text = args[1].AsString();
        var search = args[2].AsString();
        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(search, StringComparison.OrdinalIgnoreCase))
                return RuntimeValue.FromInt(i);
        }

        return RuntimeValue.FromInt(-1);
    }

    /// <summary>
    /// txtproclinmai(text, search) - Find line containing substring (uppercase comparison).
    /// </summary>
    private static RuntimeValue ExecuteTxtProcLinMai(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(-1);

        var text = args[1].AsString().ToUpperInvariant();
        var search = args[2].AsString().ToUpperInvariant();
        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(search, StringComparison.Ordinal))
                return RuntimeValue.FromInt(i);
        }

        return RuntimeValue.FromInt(-1);
    }

    /// <summary>
    /// txtproclindif(text, search) - Find line containing substring (case-sensitive).
    /// </summary>
    private static RuntimeValue ExecuteTxtProcLinDif(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(-1);

        var text = args[1].AsString();
        var search = args[2].AsString();
        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(search, StringComparison.Ordinal))
                return RuntimeValue.FromInt(i);
        }

        return RuntimeValue.FromInt(-1);
    }

    #endregion

    #region Replace Functions

    /// <summary>
    /// txtroca(text, search, replace) - Replace substring (case-insensitive).
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
    /// txtrocamai(text, search, replace) - Replace substring (uppercase comparison).
    /// </summary>
    private static RuntimeValue ExecuteTxtRocaMai(RuntimeValue[] args)
    {
        if (args.Length < 4)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromString("");

        var text = args[1].AsString();
        var search = args[2].AsString();
        var replace = args[3].AsString();

        // Case-insensitive replace
        return RuntimeValue.FromString(text.Replace(search, replace, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// txtrocadif(text, search, replace) - Replace substring (case-sensitive).
    /// </summary>
    private static RuntimeValue ExecuteTxtRocaDif(RuntimeValue[] args)
    {
        if (args.Length < 4)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromString("");

        var text = args[1].AsString();
        var search = args[2].AsString();
        var replace = args[3].AsString();

        return RuntimeValue.FromString(text.Replace(search, replace, StringComparison.Ordinal));
    }

    #endregion

    #region Utility Functions

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
    /// txtnum(number, format) - Format number as string.
    /// </summary>
    private static RuntimeValue ExecuteTxtNum(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var value = args[1];
        var format = args[2].AsString();

        // Parse format flags
        bool useScientific = format.Contains('e', StringComparison.OrdinalIgnoreCase);
        bool useDotSeparator = format.Contains('.');
        bool useCommaSeparator = format.Contains(',');
        int digits = -1;

        foreach (char c in format)
        {
            if (c >= '0' && c <= '9')
            {
                digits = c - '0';
                break;
            }
        }

        string result;
        if (value.Type == RuntimeValueType.Integer)
        {
            var intVal = value.AsInt();
            if (useScientific)
                result = ((double)intVal).ToString("E", CultureInfo.InvariantCulture);
            else if (digits > 0)
                result = intVal.ToString(CultureInfo.InvariantCulture) + "." + new string('0', digits);
            else
                result = intVal.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var doubleVal = value.AsDouble();
            if (useScientific)
                result = doubleVal.ToString("E", CultureInfo.InvariantCulture);
            else if (digits >= 0)
                result = doubleVal.ToString($"F{digits}", CultureInfo.InvariantCulture);
            else
            {
                result = doubleVal.ToString("F9", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            }
        }

        // Apply thousands separator if requested
        if ((useDotSeparator || useCommaSeparator) && !useScientific)
        {
            var parts = result.Split('.');
            var intPart = parts[0];
            var decPart = parts.Length > 1 ? parts[1] : "";

            // Add thousands separator
            var negative = intPart.StartsWith('-');
            if (negative) intPart = intPart[1..];

            var sb = new StringBuilder();
            int count = 0;
            for (int i = intPart.Length - 1; i >= 0; i--)
            {
                if (count > 0 && count % 3 == 0)
                    sb.Insert(0, useDotSeparator ? '.' : ',');
                sb.Insert(0, intPart[i]);
                count++;
            }

            if (negative) sb.Insert(0, '-');

            if (!string.IsNullOrEmpty(decPart))
            {
                sb.Append(useDotSeparator ? ',' : '.');
                sb.Append(decPart);
            }

            result = sb.ToString();
        }

        return RuntimeValue.FromString(result);
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
    /// txtcor(text) - Remove color codes from text.
    /// </summary>
    private static RuntimeValue ExecuteTxtCor(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var sb = new StringBuilder();

        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            // Skip color codes like {red}, {green}, etc.
            if (c == '{')
            {
                int end = text.IndexOf('}', i);
                if (end > i)
                {
                    i = end + 1;
                    continue;
                }
            }

            // Skip ANSI escape sequences
            if (c == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                int end = i + 2;
                while (end < text.Length && text[end] != 'm')
                    end++;
                if (end < text.Length)
                {
                    i = end + 1;
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }

        return RuntimeValue.FromString(sb.ToString());
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

    /// <summary>
    /// txtrev(text) - Reverse string.
    /// </summary>
    private static RuntimeValue ExecuteTxtRev(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var chars = text.ToCharArray();
        Array.Reverse(chars);
        return RuntimeValue.FromString(new string(chars));
    }

    /// <summary>
    /// txtesp(count) - Generate spaces.
    /// </summary>
    private static RuntimeValue ExecuteTxtEsp(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var count = (int)args[1].AsInt();
        if (count <= 0)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(new string(' ', count));
    }

    /// <summary>
    /// txtrepete(text, count) - Repeat text.
    /// </summary>
    private static RuntimeValue ExecuteTxtRepete(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var count = (int)args[2].AsInt();

        if (count <= 0 || string.IsNullOrEmpty(text))
            return RuntimeValue.FromString("");

        var sb = new StringBuilder(text.Length * count);
        for (int i = 0; i < count; i++)
            sb.Append(text);

        return RuntimeValue.FromString(sb.ToString());
    }

    #endregion

    #region Encoding/Decoding Functions

    /// <summary>
    /// txtcod(text) - Encode special characters.
    /// </summary>
    private static RuntimeValue ExecuteTxtCod(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var sb = new StringBuilder();

        foreach (char c in text)
        {
            if (c == '@' || c == '\\' || c == '"' || c < 32)
            {
                sb.Append('@');
                sb.Append((char)(c + 64));
            }
            else
            {
                sb.Append(c);
            }
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txtdec(text) - Decode special characters.
    /// </summary>
    private static RuntimeValue ExecuteTxtDec(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var sb = new StringBuilder();

        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '@' && i + 1 < text.Length)
            {
                sb.Append((char)(text[i + 1] - 64));
                i += 2;
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txtvis(text) - Convert invisible characters to visible representation.
    /// </summary>
    private static RuntimeValue ExecuteTxtVis(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var sb = new StringBuilder();

        foreach (char c in text)
        {
            switch (c)
            {
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txtinvis(text) - Convert escape sequences to invisible characters.
    /// </summary>
    private static RuntimeValue ExecuteTxtInvis(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var sb = new StringBuilder();

        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                char next = text[i + 1];
                switch (next)
                {
                    case 'n':
                    case 'N':
                        sb.Append('\n');
                        break;
                    case 'r':
                    case 'R':
                        sb.Append('\r');
                        break;
                    case 't':
                    case 'T':
                        sb.Append('\t');
                        break;
                    case 'b':
                    case 'B':
                        sb.Append('\b');
                        break;
                    default:
                        sb.Append(next);
                        break;
                }
                i += 2;
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txturlcod(text) - URL encode text.
    /// </summary>
    private static RuntimeValue ExecuteTxtUrlCod(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        return RuntimeValue.FromString(HttpUtility.UrlEncode(text) ?? "");
    }

    /// <summary>
    /// txturldec(text) - URL decode text.
    /// </summary>
    private static RuntimeValue ExecuteTxtUrlDec(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        return RuntimeValue.FromString(HttpUtility.UrlDecode(text) ?? "");
    }

    #endregion

    #region Hash Functions

    /// <summary>
    /// txtsha1(text) - SHA1 hash as hex string.
    /// </summary>
    private static RuntimeValue ExecuteTxtSha1(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA1.HashData(bytes);
        return RuntimeValue.FromString(Convert.ToHexStringLower(hash));
    }

    /// <summary>
    /// txtsha1bin(text) - SHA1 hash as binary-encoded string.
    /// </summary>
    private static RuntimeValue ExecuteTxtSha1Bin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA1.HashData(bytes);

        // Custom binary encoding as in original IntMUD
        var sb = new StringBuilder();
        for (int i = 0; i < 20; i += 4)
        {
            uint value = (uint)(hash[i] << 24 | hash[i + 1] << 16 | hash[i + 2] << 8 | hash[i + 3]);
            for (int j = 0; j < 5; j++)
            {
                sb.Append((char)((value & 0x3F) + 0x21));
                value >>= 6;
            }
        }
        sb.Append((char)((hash[0] & 3) + (hash[4] & 3) * 4 + (hash[8] & 3) * 16 + 0x21));
        sb.Append((char)((hash[12] & 3) + (hash[16] & 3) * 4 + 0x21));

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txtmd5(text) - MD5 hash as hex string.
    /// </summary>
    private static RuntimeValue ExecuteTxtMd5(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = MD5.HashData(bytes);
        return RuntimeValue.FromString(Convert.ToHexStringLower(hash));
    }

    #endregion

    #region Special Functions

    /// <summary>
    /// txtnome(text) - Normalize name (remove special chars, lowercase).
    /// </summary>
    private static RuntimeValue ExecuteTxtNome(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var sb = new StringBuilder();

        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txtfiltro(text) - Filter non-printable characters.
    /// </summary>
    private static RuntimeValue ExecuteTxtFiltro(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var text = args[1].AsString();
        var sb = new StringBuilder();

        foreach (char c in text)
        {
            if (c >= 32 && c < 127 || c == '\n' || c == '\r' || c == '\t')
                sb.Append(c);
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txttipovar(value) - Get type name of value.
    /// </summary>
    private static RuntimeValue ExecuteTxtTipoVar(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("nulo");

        var value = args[1];
        return RuntimeValue.FromString(value.Type switch
        {
            RuntimeValueType.Null => "nulo",
            RuntimeValueType.Integer => "int",
            RuntimeValueType.Double => "real",
            RuntimeValueType.String => "txt",
            RuntimeValueType.Object => value.AsObject() != null ? "ref" : "nulo",
            RuntimeValueType.Array => "vetor",
            _ => ""
        });
    }

    /// <summary>
    /// txte(text) - Replace underscores with spaces.
    /// </summary>
    private static RuntimeValue ExecuteTxtE(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(args[1].AsString().Replace('_', ' '));
    }

    /// <summary>
    /// txts(text) - Replace spaces with underscores.
    /// </summary>
    private static RuntimeValue ExecuteTxtS(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(args[1].AsString().Replace(' ', '_'));
    }

    /// <summary>
    /// txtmudamai(text, positions) - Toggle case at specific positions.
    /// </summary>
    private static RuntimeValue ExecuteTxtMudaMai(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromString("");

        var text = args[1].AsString();
        var positions = args[2].AsString();
        var chars = text.ToCharArray();

        foreach (char p in positions)
        {
            int pos = p - '0';
            if (pos >= 0 && pos < chars.Length)
            {
                chars[pos] = char.IsUpper(chars[pos])
                    ? char.ToLowerInvariant(chars[pos])
                    : char.ToUpperInvariant(chars[pos]);
            }
        }

        return RuntimeValue.FromString(new string(chars));
    }

    /// <summary>
    /// txtcopiamai(text, reference) - Copy case pattern from reference.
    /// </summary>
    private static RuntimeValue ExecuteTxtCopiaMai(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromString("");

        var text = args[1].AsString();
        var reference = args[2].AsString();
        var chars = text.ToCharArray();

        for (int i = 0; i < chars.Length && i < reference.Length; i++)
        {
            if (char.IsUpper(reference[i]))
                chars[i] = char.ToUpperInvariant(chars[i]);
            else if (char.IsLower(reference[i]))
                chars[i] = char.ToLowerInvariant(chars[i]);
        }

        return RuntimeValue.FromString(new string(chars));
    }

    /// <summary>
    /// txtremove(text, chars) - Remove specified characters.
    /// </summary>
    private static RuntimeValue ExecuteTxtRemove(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromString("");

        var text = args[1].AsString();
        var charsToRemove = args[2].AsString();
        var sb = new StringBuilder();

        foreach (char c in text)
        {
            if (!charsToRemove.Contains(c))
                sb.Append(c);
        }

        return RuntimeValue.FromString(sb.ToString());
    }

    /// <summary>
    /// txtconv(text, fromEncoding, toEncoding) - Convert text encoding.
    /// </summary>
    private static RuntimeValue ExecuteTxtConv(RuntimeValue[] args)
    {
        if (args.Length < 4)
            return args.Length >= 2 ? args[1] : RuntimeValue.FromString("");

        var text = args[1].AsString();
        var fromEnc = args[2].AsString().ToLowerInvariant();
        var toEnc = args[3].AsString().ToLowerInvariant();

        try
        {
            var sourceEncoding = GetEncoding(fromEnc);
            var targetEncoding = GetEncoding(toEnc);

            var bytes = sourceEncoding.GetBytes(text);
            return RuntimeValue.FromString(targetEncoding.GetString(bytes));
        }
        catch
        {
            return RuntimeValue.FromString(text);
        }
    }

    private static Encoding GetEncoding(string name)
    {
        return name switch
        {
            "utf8" or "utf-8" => Encoding.UTF8,
            "latin1" or "iso-8859-1" => Encoding.Latin1,
            "ascii" => Encoding.ASCII,
            "utf16" or "utf-16" or "unicode" => Encoding.Unicode,
            _ => Encoding.UTF8
        };
    }

    /// <summary>
    /// txtchr(code) - Character from code.
    /// </summary>
    private static RuntimeValue ExecuteTxtChr(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromString("");

        var code = (int)args[1].AsInt();
        if (code < 0 || code > 0xFFFF)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(((char)code).ToString());
    }

    #endregion

    #region Count Functions

    /// <summary>
    /// intsub(text) - Count words in text.
    /// </summary>
    private static RuntimeValue ExecuteIntSub(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var text = args[1].AsString();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return RuntimeValue.FromInt(words.Length);
    }

    /// <summary>
    /// intsublin(text) - Count lines in text.
    /// </summary>
    private static RuntimeValue ExecuteIntSubLin(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var text = args[1].AsString();
        if (string.IsNullOrEmpty(text))
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(text.Split('\n').Length);
    }

    /// <summary>
    /// intchr(text) - Get ASCII code of first character.
    /// </summary>
    private static RuntimeValue ExecuteIntChr(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var text = args[1].AsString();
        if (string.IsNullOrEmpty(text))
            return RuntimeValue.FromInt(0);

        return RuntimeValue.FromInt(text[0]);
    }

    #endregion

    #region Distance Functions

    /// <summary>
    /// intdist(text1, text2) - Levenshtein distance (case-insensitive).
    /// </summary>
    private static RuntimeValue ExecuteIntDist(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(0);

        var text1 = args[1].AsString().ToLowerInvariant();
        var text2 = args[2].AsString().ToLowerInvariant();

        return RuntimeValue.FromInt(LevenshteinDistance(text1, text2));
    }

    /// <summary>
    /// intdistmai(text1, text2) - Levenshtein distance (uppercase comparison).
    /// </summary>
    private static RuntimeValue ExecuteIntDistMai(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(0);

        var text1 = args[1].AsString().ToUpperInvariant();
        var text2 = args[2].AsString().ToUpperInvariant();

        return RuntimeValue.FromInt(LevenshteinDistance(text1, text2));
    }

    /// <summary>
    /// intdistdif(text1, text2) - Levenshtein distance (case-sensitive).
    /// </summary>
    private static RuntimeValue ExecuteIntDistDif(RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.FromInt(0);

        var text1 = args[1].AsString();
        var text2 = args[2].AsString();

        return RuntimeValue.FromInt(LevenshteinDistance(text1, text2));
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t))
            return s.Length;

        int[,] d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++)
            d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }

    #endregion

    #region Name/Password Functions

    /// <summary>
    /// intnome(text) - Check if text is valid name.
    /// </summary>
    private static RuntimeValue ExecuteIntNome(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var text = args[1].AsString();
        if (string.IsNullOrWhiteSpace(text))
            return RuntimeValue.FromInt(0);

        // Valid name: starts with letter, contains only letters/digits
        if (!char.IsLetter(text[0]))
            return RuntimeValue.FromInt(0);

        foreach (char c in text)
        {
            if (!char.IsLetterOrDigit(c))
                return RuntimeValue.FromInt(0);
        }

        return RuntimeValue.FromInt(1);
    }

    /// <summary>
    /// intsenha(text) - Check password strength.
    /// </summary>
    private static RuntimeValue ExecuteIntSenha(RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.FromInt(0);

        var text = args[1].AsString();
        if (text.Length < 4)
            return RuntimeValue.FromInt(0);

        int score = 0;
        bool hasLower = false, hasUpper = false, hasDigit = false, hasSpecial = false;

        foreach (char c in text)
        {
            if (char.IsLower(c)) hasLower = true;
            else if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSpecial = true;
        }

        if (hasLower) score++;
        if (hasUpper) score++;
        if (hasDigit) score++;
        if (hasSpecial) score++;
        if (text.Length >= 8) score++;

        return RuntimeValue.FromInt(score);
    }

    #endregion
}
