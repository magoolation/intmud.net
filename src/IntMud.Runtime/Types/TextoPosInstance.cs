namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a textopos (text position cursor) instance.
/// This implements text navigation functionality from original IntMUD.
/// </summary>
public sealed class TextoPosInstance
{
    private TextoTxtInstance? _textoTxt;
    private int _linha;

    /// <summary>
    /// Create an uninitialized text position.
    /// </summary>
    public TextoPosInstance()
    {
        _textoTxt = null;
        _linha = 0;
    }

    /// <summary>
    /// Create a new text position for the given TextoTxt.
    /// </summary>
    public TextoPosInstance(TextoTxtInstance textoTxt, int linha = 0)
    {
        _textoTxt = textoTxt;
        _linha = linha;
    }

    /// <summary>
    /// The associated TextoTxt.
    /// </summary>
    public TextoTxtInstance? TextoTxt => _textoTxt;

    /// <summary>
    /// Current line number (0-based internally, but 1-based in scripts).
    /// </summary>
    public int Linha
    {
        get => _linha;
        set => _linha = Math.Max(0, value);
    }

    /// <summary>
    /// Check if position has content (lin property).
    /// Returns the line number + 1 if valid, 0 if invalid.
    /// </summary>
    public int Lin
    {
        get
        {
            if (_textoTxt == null || _linha < 0 || _linha >= _textoTxt.Linhas)
                return 0;
            return _linha + 1; // 1-based for scripts
        }
    }

    /// <summary>
    /// Get byte position (approximate).
    /// </summary>
    public int Byte
    {
        get
        {
            if (_textoTxt == null)
                return 0;

            int bytes = 0;
            for (int i = 0; i < _linha && i < _textoTxt.Linhas; i++)
            {
                bytes += _textoTxt.GetLine(i).Length + 1;
            }
            return bytes;
        }
    }

    /// <summary>
    /// Get text at current position.
    /// </summary>
    public string Texto()
    {
        if (_textoTxt == null || _linha < 0 || _linha >= _textoTxt.Linhas)
            return "";
        return _textoTxt.GetLine(_linha);
    }

    /// <summary>
    /// Get substring of text at current position.
    /// </summary>
    public string Texto(int start, int length = -1)
    {
        var text = Texto();
        if (start < 0) start = 0;
        if (start >= text.Length) return "";

        if (length < 0 || start + length > text.Length)
            length = text.Length - start;

        return text.Substring(start, length);
    }

    /// <summary>
    /// Get text of current line limited to specified length.
    /// </summary>
    public string TextoLin(int maxLength)
    {
        var text = Texto();
        if (text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength);
    }

    /// <summary>
    /// Move to next line.
    /// </summary>
    public void Depois()
    {
        _linha++;
    }

    /// <summary>
    /// Move to previous line.
    /// </summary>
    public void Antes()
    {
        if (_linha > 0)
            _linha--;
    }

    /// <summary>
    /// Change text at current position.
    /// </summary>
    public void Mudar(string newText)
    {
        if (_textoTxt != null && _linha >= 0 && _linha < _textoTxt.Linhas)
        {
            _textoTxt.SetLine(_linha, newText);
        }
    }

    /// <summary>
    /// Change part of text at current position.
    /// </summary>
    public void Mudar(string newText, int start, int length)
    {
        if (_textoTxt == null || _linha < 0 || _linha >= _textoTxt.Linhas)
            return;

        var currentText = _textoTxt.GetLine(_linha);

        if (start < 0) start = 0;
        if (start > currentText.Length) start = currentText.Length;
        if (length < 0 || start + length > currentText.Length)
            length = currentText.Length - start;

        var result = currentText.Substring(0, start) + newText;
        if (start + length < currentText.Length)
            result += currentText.Substring(start + length);

        _textoTxt.SetLine(_linha, result);
    }

    /// <summary>
    /// Add a line BEFORE current position.
    /// After this, textopos points to the newly added text.
    /// Returns the count of lines added.
    /// </summary>
    public int Add(string text)
    {
        if (_textoTxt == null)
            return 0;

        int targetLine = _linha;
        _textoTxt.InsertLine(_linha, text);
        // InsertLine incremented our position, set it back to point to new text
        _linha = targetLine;
        return 1;
    }

    /// <summary>
    /// Add lines from another TextoPos/TextoTxt BEFORE current position.
    /// After this, textopos points to the first added line.
    /// Returns the count of lines added.
    /// </summary>
    public int Add(TextoPosInstance source, int count)
    {
        if (_textoTxt == null || source.TextoTxt == null)
            return 0;

        int targetLine = _linha;
        int sourcePos = source.Linha;
        int linesAdded = 0;

        for (int i = 0; i < count && sourcePos + i < source.TextoTxt.Linhas; i++)
        {
            var lineContent = source.TextoTxt.GetLine(sourcePos + i);
            _textoTxt.InsertLine(_linha + i, lineContent);
            linesAdded++;
        }

        // Point to the first added line
        _linha = targetLine;
        return linesAdded;
    }

    /// <summary>
    /// Add a line BEFORE current position and move position past it.
    /// Returns the count of lines added.
    /// </summary>
    public int AddPos(string text)
    {
        if (_textoTxt == null)
            return 0;

        _textoTxt.InsertLine(_linha, text);
        // InsertLine already incremented our position past the new text
        return 1;
    }

    /// <summary>
    /// Add lines from another TextoPos/TextoTxt and move position past them.
    /// Returns the count of lines added.
    /// </summary>
    public int AddPos(TextoPosInstance source, int count)
    {
        if (_textoTxt == null || source.TextoTxt == null)
            return 0;

        int sourcePos = source.Linha;
        int linesAdded = 0;

        for (int i = 0; i < count && sourcePos + i < source.TextoTxt.Linhas; i++)
        {
            _textoTxt.InsertLine(_linha + i, source.TextoTxt.GetLine(sourcePos + i));
            linesAdded++;
        }

        // Position is already past the added lines due to InsertLine increments
        return linesAdded;
    }

    /// <summary>
    /// Remove current line.
    /// </summary>
    public void Remove()
    {
        if (_textoTxt != null && _linha >= 0 && _linha < _textoTxt.Linhas)
        {
            _textoTxt.RemoveLine(_linha);
        }
    }

    /// <summary>
    /// Join current line with PREVIOUS line.
    /// After joining, textopos points to the previous line.
    /// Returns true if joined, false otherwise.
    /// </summary>
    public bool Juntar()
    {
        // Cannot join if at first line or invalid
        if (_textoTxt == null || _linha <= 0 || _linha >= _textoTxt.Linhas)
            return false;

        var previousLine = _textoTxt.GetLine(_linha - 1);
        var currentLine = _textoTxt.GetLine(_linha);
        _textoTxt.SetLine(_linha - 1, previousLine + currentLine);
        _textoTxt.RemoveLine(_linha);
        _linha--; // Move position to the previous line (where the joined content now is)
        return true;
    }

    /// <summary>
    /// Search for text in current line.
    /// </summary>
    /// <summary>
    /// Search for text across multiple lines starting from current position.
    /// Case-sensitive search.
    /// </summary>
    public int TxtProc(string search, int startChar = 0, int numLines = -1)
    {
        return TxtProcInternal(search, startChar, numLines, StringComparison.Ordinal);
    }

    /// <summary>
    /// Case-insensitive search for text across multiple lines.
    /// </summary>
    public int TxtProcMai(string search, int startChar = 0, int numLines = -1)
    {
        return TxtProcInternal(search, startChar, numLines, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Internal search implementation that handles multi-line search.
    /// In IntMUD, each line is conceptually preceded by a newline, so searching
    /// for "\nA\n" will match "A" on its own line.
    /// </summary>
    private int TxtProcInternal(string search, int startChar, int numLines, StringComparison comparison)
    {
        if (_textoTxt == null || string.IsNullOrEmpty(search))
            return -1;

        // Handle special case when search starts with '\n'
        bool startsWithNewline = search.StartsWith("\n");
        if (startsWithNewline && search.Length == 1)
            return -1; // Can't search for just newline

        // Build the text to search in (all lines from current position)
        // Prepend newline because in IntMUD each line is conceptually preceded by \n
        var sb = new System.Text.StringBuilder();
        sb.Append('\n'); // Implicit newline before first line

        int linesToSearch = numLines > 0 ? numLines : (_textoTxt.Linhas - _linha);

        for (int i = 0; i < linesToSearch && (_linha + i) < _textoTxt.Linhas; i++)
        {
            if (i > 0)
                sb.Append('\n');
            sb.Append(_textoTxt.GetLine(_linha + i));
        }
        // Add trailing newline for searches that end with \n
        sb.Append('\n');

        var fullText = sb.ToString();

        // Adjust start position (account for the prepended newline)
        int searchStart = startChar + 1; // +1 for the prepended newline
        if (startsWithNewline && startChar == 0)
            searchStart = 0; // Start from the prepended newline
        if (searchStart < 0) searchStart = 0;
        if (searchStart >= fullText.Length) return -1;

        // Perform the search
        int result = fullText.IndexOf(search, searchStart, comparison);

        if (result < 0)
            return -1;

        // Adjust result to account for the prepended newline
        result--; // Compensate for the prepended newline

        // If started with newline, skip it in the result
        if (startsWithNewline)
            result++; // Point to the char after the matched \n

        return result;
    }

    /// <summary>
    /// Invalidate this position (called when TextoTxt is cleared).
    /// </summary>
    internal void Invalidate()
    {
        _linha = 0;
    }

    /// <summary>
    /// Associate with a different TextoTxt.
    /// </summary>
    public void MudarTxt(TextoTxtInstance? newTextoTxt)
    {
        if (_textoTxt != null)
            _textoTxt.UnregisterPosition(this);

        _textoTxt = newTextoTxt;
        _linha = 0;
    }

    public override string ToString() => $"[TextoPos: line {_linha + 1}]";
}
