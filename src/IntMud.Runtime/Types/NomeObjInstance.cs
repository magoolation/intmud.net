using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a nomeobj (object name index) instance.
/// Maps to C++ TVarNomeObj - resolves text patterns to objects.
/// Used for natural language object matching in MUD:
///   "3 sword" = 3rd item matching "sword"
///   "2.5 sword" = starting from 5th, match 2 "sword" items
/// </summary>
public sealed class NomeObjInstance
{
    // Normalization table matching C++ tabNOMEOBJ for name comparison
    private static readonly char[] NormTable = BuildNormTable();

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    /// <summary>Normalized search name after parsing.</summary>
    public string SearchName { get; private set; } = "";

    /// <summary>Number of items to match (default 1).</summary>
    public int Total { get; private set; } = 1;

    /// <summary>Starting index for matching (1-based).</summary>
    public int StartIndex { get; private set; } = 1;

    /// <summary>Number of matches found after FuncNome.</summary>
    public int MatchCount { get; private set; }

    /// <summary>Current matched object.</summary>
    public BytecodeRuntimeObject? CurrentObject { get; set; }

    /// <summary>
    /// Initialize name matching with a pattern.
    /// Pattern format: "N.start name" or "N name" or just "name"
    /// where N = count of items, start = starting index.
    /// </summary>
    public void Ini(string pattern)
    {
        SearchName = "";
        Total = 1;
        StartIndex = 1;
        MatchCount = 0;
        CurrentObject = null;

        if (string.IsNullOrEmpty(pattern))
            return;

        var trimmed = pattern.Trim();
        if (trimmed.Length == 0)
            return;

        int pos = 0;

        // Check if starts with a number (count prefix)
        if (char.IsDigit(trimmed[0]))
        {
            // Parse the count
            int num = 0;
            while (pos < trimmed.Length && char.IsDigit(trimmed[pos]))
            {
                num = num * 10 + (trimmed[pos] - '0');
                pos++;
            }
            Total = num > 0 ? num : 1;

            // Check for .startIndex
            if (pos < trimmed.Length && trimmed[pos] == '.')
            {
                pos++;
                int start = 0;
                while (pos < trimmed.Length && char.IsDigit(trimmed[pos]))
                {
                    start = start * 10 + (trimmed[pos] - '0');
                    pos++;
                }
                StartIndex = start > 0 ? start : 1;
            }

            // Skip whitespace before name
            while (pos < trimmed.Length && trimmed[pos] == ' ')
                pos++;
        }

        // Rest is the search name
        if (pos < trimmed.Length)
            SearchName = NormalizeName(trimmed.Substring(pos));
    }

    /// <summary>
    /// Match an object's properties against the current search pattern.
    /// C++ FuncNome performs word-based case-insensitive matching.
    /// Returns true if the object matches, tracking match count.
    /// </summary>
    public bool FuncNome(BytecodeRuntimeObject obj)
    {
        if (string.IsNullOrEmpty(SearchName))
            return false;

        // Get the object's display name - check for "nome" field first
        string objName = "";
        var nomeField = obj.GetField("nome");
        if (nomeField.Type == RuntimeValueType.String)
            objName = nomeField.AsString();
        else
            objName = obj.ClassName;

        if (string.IsNullOrEmpty(objName))
            return false;

        // Normalize and check if search name matches
        var normalizedObjName = NormalizeName(objName);

        // Word-based matching: each word in SearchName must appear in objName
        if (!ContainsAllWords(normalizedObjName, SearchName))
            return false;

        // This object matches. Track against count/start.
        MatchCount++;

        if (MatchCount >= StartIndex && MatchCount < StartIndex + Total)
        {
            CurrentObject = obj;
            return true;
        }

        return false;
    }

    /// <summary>Get name of current match.</summary>
    public string Nome()
    {
        return CurrentObject?.ClassName ?? "";
    }

    /// <summary>
    /// Check if all words in search appear in the target name.
    /// Words are delimited by spaces in the normalized form.
    /// </summary>
    private static bool ContainsAllWords(string target, string search)
    {
        // Simple substring match for single-word searches
        if (!search.Contains(' '))
            return target.Contains(search);

        // For multi-word searches, each word must appear
        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (!target.Contains(word))
                return false;
        }
        return true;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var chars = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            chars[i] = c < 256 ? NormTable[c] : char.ToLowerInvariant(c);
        }
        return new string(chars);
    }

    private static char[] BuildNormTable()
    {
        var table = new char[256];

        // Start with identity
        for (int i = 0; i < 256; i++)
            table[i] = (char)i;

        // a-z stay as-is
        for (char c = 'a'; c <= 'z'; c++)
            table[c] = c;

        // A-Z → a-z
        for (char c = 'A'; c <= 'Z'; c++)
            table[c] = (char)(c + 32);

        // Underscore → space (matching C++ tabNOMEOBJ)
        table['_'] = ' ';

        // Accented characters → base letter
        table[0xE1] = 'a'; table[0xE0] = 'a'; table[0xE2] = 'a'; table[0xE3] = 'a';
        table[0xC1] = 'a'; table[0xC0] = 'a'; table[0xC2] = 'a'; table[0xC3] = 'a';
        table[0xE9] = 'e'; table[0xE8] = 'e'; table[0xEA] = 'e';
        table[0xC9] = 'e'; table[0xC8] = 'e'; table[0xCA] = 'e';
        table[0xED] = 'i'; table[0xEC] = 'i'; table[0xEE] = 'i';
        table[0xCD] = 'i'; table[0xCC] = 'i'; table[0xCE] = 'i';
        table[0xF3] = 'o'; table[0xF2] = 'o'; table[0xF4] = 'o'; table[0xF5] = 'o';
        table[0xD3] = 'o'; table[0xD2] = 'o'; table[0xD4] = 'o'; table[0xD5] = 'o';
        table[0xFA] = 'u'; table[0xF9] = 'u'; table[0xFB] = 'u';
        table[0xDA] = 'u'; table[0xD9] = 'u'; table[0xDB] = 'u';
        table[0xE7] = 'c'; table[0xC7] = 'c'; // ç/Ç → c

        return table;
    }

    public override string ToString() => $"[NomeObj: {SearchName}]";
}
