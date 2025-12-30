namespace IntMud.Hosting;

/// <summary>
/// ANSI escape codes for terminal colors and formatting.
/// </summary>
public static class AnsiColors
{
    // Reset
    public const string Reset = "\x1b[0m";

    // Regular colors
    public const string Black = "\x1b[30m";
    public const string Red = "\x1b[31m";
    public const string Green = "\x1b[32m";
    public const string Yellow = "\x1b[33m";
    public const string Blue = "\x1b[34m";
    public const string Magenta = "\x1b[35m";
    public const string Cyan = "\x1b[36m";
    public const string White = "\x1b[37m";

    // Bright/Bold colors
    public const string BrightBlack = "\x1b[90m";
    public const string BrightRed = "\x1b[91m";
    public const string BrightGreen = "\x1b[92m";
    public const string BrightYellow = "\x1b[93m";
    public const string BrightBlue = "\x1b[94m";
    public const string BrightMagenta = "\x1b[95m";
    public const string BrightCyan = "\x1b[96m";
    public const string BrightWhite = "\x1b[97m";

    // Background colors
    public const string BgBlack = "\x1b[40m";
    public const string BgRed = "\x1b[41m";
    public const string BgGreen = "\x1b[42m";
    public const string BgYellow = "\x1b[43m";
    public const string BgBlue = "\x1b[44m";
    public const string BgMagenta = "\x1b[45m";
    public const string BgCyan = "\x1b[46m";
    public const string BgWhite = "\x1b[47m";

    // Formatting
    public const string Bold = "\x1b[1m";
    public const string Dim = "\x1b[2m";
    public const string Italic = "\x1b[3m";
    public const string Underline = "\x1b[4m";
    public const string Blink = "\x1b[5m";
    public const string Reverse = "\x1b[7m";
    public const string Hidden = "\x1b[8m";
    public const string Strikethrough = "\x1b[9m";

    // Clear screen
    public const string ClearScreen = "\x1b[2J\x1b[H";
    public const string ClearLine = "\x1b[2K";

    /// <summary>
    /// Color a string with the specified color.
    /// </summary>
    public static string Colorize(string text, string color)
    {
        return $"{color}{text}{Reset}";
    }

    /// <summary>
    /// Parse color codes in text (e.g., {red}, {green}, {reset}).
    /// </summary>
    public static string ParseColorCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            // Reset
            .Replace("{reset}", Reset, StringComparison.OrdinalIgnoreCase)
            .Replace("{r}", Reset, StringComparison.OrdinalIgnoreCase)

            // Regular colors
            .Replace("{black}", Black, StringComparison.OrdinalIgnoreCase)
            .Replace("{red}", Red, StringComparison.OrdinalIgnoreCase)
            .Replace("{green}", Green, StringComparison.OrdinalIgnoreCase)
            .Replace("{yellow}", Yellow, StringComparison.OrdinalIgnoreCase)
            .Replace("{blue}", Blue, StringComparison.OrdinalIgnoreCase)
            .Replace("{magenta}", Magenta, StringComparison.OrdinalIgnoreCase)
            .Replace("{cyan}", Cyan, StringComparison.OrdinalIgnoreCase)
            .Replace("{white}", White, StringComparison.OrdinalIgnoreCase)

            // Bright colors
            .Replace("{brightblack}", BrightBlack, StringComparison.OrdinalIgnoreCase)
            .Replace("{brightred}", BrightRed, StringComparison.OrdinalIgnoreCase)
            .Replace("{brightgreen}", BrightGreen, StringComparison.OrdinalIgnoreCase)
            .Replace("{brightyellow}", BrightYellow, StringComparison.OrdinalIgnoreCase)
            .Replace("{brightblue}", BrightBlue, StringComparison.OrdinalIgnoreCase)
            .Replace("{brightmagenta}", BrightMagenta, StringComparison.OrdinalIgnoreCase)
            .Replace("{brightcyan}", BrightCyan, StringComparison.OrdinalIgnoreCase)
            .Replace("{brightwhite}", BrightWhite, StringComparison.OrdinalIgnoreCase)

            // Short color codes
            .Replace("{k}", Black, StringComparison.OrdinalIgnoreCase)
            .Replace("{R}", Red, StringComparison.OrdinalIgnoreCase)
            .Replace("{G}", Green, StringComparison.OrdinalIgnoreCase)
            .Replace("{Y}", Yellow, StringComparison.OrdinalIgnoreCase)
            .Replace("{B}", Blue, StringComparison.OrdinalIgnoreCase)
            .Replace("{M}", Magenta, StringComparison.OrdinalIgnoreCase)
            .Replace("{C}", Cyan, StringComparison.OrdinalIgnoreCase)
            .Replace("{W}", White, StringComparison.OrdinalIgnoreCase)

            // Formatting
            .Replace("{bold}", Bold, StringComparison.OrdinalIgnoreCase)
            .Replace("{b}", Bold, StringComparison.OrdinalIgnoreCase)
            .Replace("{dim}", Dim, StringComparison.OrdinalIgnoreCase)
            .Replace("{italic}", Italic, StringComparison.OrdinalIgnoreCase)
            .Replace("{i}", Italic, StringComparison.OrdinalIgnoreCase)
            .Replace("{underline}", Underline, StringComparison.OrdinalIgnoreCase)
            .Replace("{u}", Underline, StringComparison.OrdinalIgnoreCase)
            .Replace("{blink}", Blink, StringComparison.OrdinalIgnoreCase)
            .Replace("{reverse}", Reverse, StringComparison.OrdinalIgnoreCase)

            // Clear
            .Replace("{clear}", ClearScreen, StringComparison.OrdinalIgnoreCase)
            .Replace("{cls}", ClearScreen, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Strip all ANSI codes from text.
    /// </summary>
    public static string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
    }

    /// <summary>
    /// Strip color code placeholders from text.
    /// </summary>
    public static string StripColorCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return System.Text.RegularExpressions.Regex.Replace(text, @"\{[a-zA-Z]+\}", "");
    }
}
