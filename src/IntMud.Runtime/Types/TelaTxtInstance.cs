namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a telatxt (console text) instance.
/// This implements the console functionality from original IntMUD.
/// </summary>
public sealed class TelaTxtInstance
{
    /// <summary>
    /// The owner object that contains this telatxt variable.
    /// </summary>
    public object? Owner { get; set; }

    /// <summary>
    /// The variable name (e.g., "tela").
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Maximum length of the input line (default 1023).
    /// </summary>
    public int Total { get; set; } = 1023;

    /// <summary>
    /// Current input text being edited.
    /// </summary>
    public string Texto { get; set; } = "";

    /// <summary>
    /// Current line position (for scrolling).
    /// </summary>
    public int Linha { get; set; } = 0;

    /// <summary>
    /// Current column position for output.
    /// </summary>
    public int PosX { get; set; } = 0;

    /// <summary>
    /// Protocol type. Returns 1 if console is active, 0 otherwise.
    /// </summary>
    public int Proto => IsActive ? 1 : 0;

    /// <summary>
    /// Whether this console is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Delegate for writing output.
    /// </summary>
    public Action<string>? WriteOutput { get; set; }

    /// <summary>
    /// Delegate for emitting a beep sound.
    /// </summary>
    public Action? Beep { get; set; }

    /// <summary>
    /// Delegate for clearing the screen.
    /// </summary>
    public Action? ClearScreen { get; set; }

    /// <summary>
    /// Write a message to the console.
    /// </summary>
    public void Msg(string text)
    {
        if (!IsActive || WriteOutput == null)
            return;

        // Process special characters (like \n)
        var processed = ProcessText(text);
        WriteOutput(processed);

        // Update PosX based on text
        if (processed.EndsWith('\n'))
            PosX = 0;
        else
            PosX += processed.Length - (processed.LastIndexOf('\n') + 1);
    }

    /// <summary>
    /// Emit a beep sound.
    /// </summary>
    public void EmitBipe()
    {
        if (!IsActive)
            return;

        if (Beep != null)
            Beep();
        else
            Console.Beep();
    }

    /// <summary>
    /// Clear the console screen.
    /// </summary>
    public void Limpa()
    {
        if (!IsActive)
            return;

        if (ClearScreen != null)
            ClearScreen();
        else
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                // Ignore if console operations not supported
            }
        }

        PosX = 0;
        Linha = 0;
    }

    /// <summary>
    /// Process text for special characters.
    /// </summary>
    private static string ProcessText(string text)
    {
        // For now, just handle basic text
        // Original IntMUD has special codes like \c (color), \n (newline), etc.
        return text.Replace("\\n", "\n").Replace("\\t", "\t");
    }

    /// <summary>
    /// Append text to the input line.
    /// </summary>
    public void AppendTexto(string text)
    {
        if (Texto.Length + text.Length <= Total)
        {
            Texto += text;
        }
    }

    /// <summary>
    /// Handle a key press.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <returns>The text to send if Enter was pressed, null otherwise.</returns>
    public string? ProcessKey(string key)
    {
        switch (key.ToUpperInvariant())
        {
            case "ENTER":
                var result = Texto;
                Texto = "";
                return result;

            case "BACKSPACE":
                if (Texto.Length > 0)
                    Texto = Texto[..^1];
                return null;

            case "DELETE":
                // For now, same as backspace
                if (Texto.Length > 0)
                    Texto = Texto[..^1];
                return null;

            case "ESC":
            case "F1":
            case "F2":
            case "F3":
            case "F4":
            case "F5":
            case "F6":
            case "F7":
            case "F8":
            case "F9":
            case "F10":
            case "F11":
            case "F12":
            case "UP":
            case "DOWN":
            case "LEFT":
            case "RIGHT":
            case "HOME":
            case "END":
            case "PAGEUP":
            case "PAGEDOWN":
            case "TAB":
                // Special keys don't add to input
                return null;

            default:
                // Regular character - add to input if it's a single char
                if (key.Length == 1 && Texto.Length < Total)
                {
                    Texto += key;
                }
                return null;
        }
    }
}
