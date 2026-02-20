namespace IntMud.Core.Text;

/// <summary>
/// String comparer for IntMUD identifiers (variable names, function names, class names).
/// Implements the same comparison rules as the original C++ implementation:
/// - Case insensitive (A-Z = a-z)
/// - Underscore equals space (_ = ' ')
/// - Accented characters are normalized (á = a, ç = c, etc.)
/// </summary>
public sealed class IntMudNameComparer : IEqualityComparer<string>, IComparer<string>
{
    /// <summary>
    /// Default instance for use in dictionaries and collections.
    /// </summary>
    public static readonly IntMudNameComparer Instance = new();

    // Character normalization table (256 entries for Latin-1)
    // Maps each character to its normalized form for comparison
    private static readonly char[] NormalizeTable = BuildNormalizeTable();

    private static char[] BuildNormalizeTable()
    {
        var table = new char[256];

        // Initialize with identity mapping
        for (int i = 0; i < 256; i++)
        {
            table[i] = (char)i;
        }

        // Lowercase letters map to themselves
        for (char c = 'a'; c <= 'z'; c++)
        {
            table[c] = c;
        }

        // Uppercase letters map to lowercase
        for (char c = 'A'; c <= 'Z'; c++)
        {
            table[c] = (char)(c + 32); // A->a, B->b, etc.
        }

        // Numbers map to themselves (already set)

        // Space maps to space; underscore stays as underscore
        // (C++ tabCOMPLETO explicitly keeps '_' = '_', unlike tabNOMES1 which maps to space)
        table['_'] = '_';
        table[' '] = ' ';

        // @ is valid in identifiers
        table['@'] = '@';

        // Accented characters - map to base letter (matching original tabNOMES1)
        // á à â ã -> a
        table[0xE1] = 'a'; // á
        table[0xE0] = 'a'; // à
        table[0xE2] = 'a'; // â
        table[0xE3] = 'a'; // ã
        table[0xC1] = 'a'; // Á
        table[0xC0] = 'a'; // À
        table[0xC2] = 'a'; // Â
        table[0xC3] = 'a'; // Ã

        // é è ê -> e
        table[0xE9] = 'e'; // é
        table[0xE8] = 'e'; // è
        table[0xEA] = 'e'; // ê
        table[0xC9] = 'e'; // É
        table[0xC8] = 'e'; // È
        table[0xCA] = 'e'; // Ê

        // í ì î -> i
        table[0xED] = 'i'; // í
        table[0xEC] = 'i'; // ì
        table[0xEE] = 'i'; // î
        table[0xCD] = 'i'; // Í
        table[0xCC] = 'i'; // Ì
        table[0xCE] = 'i'; // Î

        // ó ò ô õ -> o
        table[0xF3] = 'o'; // ó
        table[0xF2] = 'o'; // ò
        table[0xF4] = 'o'; // ô
        table[0xF5] = 'o'; // õ
        table[0xD3] = 'o'; // Ó
        table[0xD2] = 'o'; // Ò
        table[0xD4] = 'o'; // Ô
        table[0xD5] = 'o'; // Õ

        // ú ù û -> u
        table[0xFA] = 'u'; // ú
        table[0xF9] = 'u'; // ù
        table[0xFB] = 'u'; // û
        table[0xDA] = 'u'; // Ú
        table[0xD9] = 'u'; // Ù
        table[0xDB] = 'u'; // Û

        // ç -> c (special handling - ç is valid as itself in original)
        table[0xE7] = (char)0xE7; // ç stays as ç
        table[0xC7] = (char)0xE7; // Ç -> ç

        return table;
    }

    /// <summary>
    /// Normalize a character for comparison.
    /// </summary>
    public static char NormalizeChar(char c)
    {
        if (c < 256)
        {
            return NormalizeTable[c];
        }
        // For characters outside Latin-1, just lowercase
        return char.ToLowerInvariant(c);
    }

    /// <summary>
    /// Normalize a name for comparison.
    /// </summary>
    public static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var chars = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            chars[i] = NormalizeChar(name[i]);
        }
        return new string(chars);
    }

    /// <inheritdoc/>
    public bool Equals(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Length != y.Length) return false;

        for (int i = 0; i < x.Length; i++)
        {
            if (NormalizeChar(x[i]) != NormalizeChar(y[i]))
                return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public int GetHashCode(string obj)
    {
        if (obj is null) return 0;

        // Use a simple hash combining normalized characters
        int hash = 17;
        foreach (char c in obj)
        {
            hash = hash * 31 + NormalizeChar(c);
        }
        return hash;
    }

    /// <inheritdoc/>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int minLen = Math.Min(x.Length, y.Length);
        for (int i = 0; i < minLen; i++)
        {
            char c1 = NormalizeChar(x[i]);
            char c2 = NormalizeChar(y[i]);
            if (c1 != c2)
                return c1 < c2 ? -1 : 1;
        }

        // If all compared characters are equal, shorter string comes first
        return x.Length.CompareTo(y.Length);
    }
}
