namespace IntMud.Compiler.Parsing;

/// <summary>
/// Parser for IntMUD configuration files (.int files that are not class definitions).
/// These files contain directives like "incluir = adm/" and settings like "telatxt = 1".
/// </summary>
public sealed class IntMudConfigParser
{
    /// <summary>
    /// Parse an IntMUD configuration file.
    /// </summary>
    /// <param name="content">The file content</param>
    /// <param name="fileName">The file name for error reporting</param>
    /// <returns>The parsed configuration</returns>
    public IntMudConfig Parse(string content, string fileName = "config")
    {
        var config = new IntMudConfig { FileName = fileName };
        var lines = content.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            // Look for key = value pairs
            var equalPos = line.IndexOf('=');
            if (equalPos <= 0)
                continue;

            var key = line[..equalPos].Trim().ToLowerInvariant();
            var value = line[(equalPos + 1)..].Trim();

            // Remove inline comments
            var commentPos = value.IndexOf('#');
            if (commentPos >= 0)
                value = value[..commentPos].Trim();

            ParseDirective(config, key, value);
        }

        return config;
    }

    private static void ParseDirective(IntMudConfig config, string key, string value)
    {
        switch (key)
        {
            case "incluir":
                config.Includes.Add(value);
                break;
            case "exec":
                if (int.TryParse(value, out var exec))
                    config.ExecLimit = exec;
                break;
            case "telatxt":
                config.TelaTxt = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            case "log":
                if (int.TryParse(value, out var log))
                    config.LogMode = log;
                break;
            case "err":
                if (int.TryParse(value, out var err))
                    config.ErrorMode = err;
                break;
            case "completo":
                config.FullAccess = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            default:
                // Store unknown directives for potential use
                config.OtherSettings[key] = value;
                break;
        }
    }
}

/// <summary>
/// Represents the configuration from an IntMUD configuration file.
/// </summary>
public sealed class IntMudConfig
{
    /// <summary>
    /// The configuration file name.
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// List of directories/files to include (from "incluir = ..." directives).
    /// </summary>
    public List<string> Includes { get; } = new();

    /// <summary>
    /// Maximum instructions a function can execute before control returns (exec = ...).
    /// </summary>
    public int ExecLimit { get; set; } = 10000;

    /// <summary>
    /// Whether to open a text window/console (telatxt = 1).
    /// </summary>
    public bool TelaTxt { get; set; }

    /// <summary>
    /// Where to present error messages (log = ...).
    /// 0 = console, other values may indicate file logging.
    /// </summary>
    public int LogMode { get; set; }

    /// <summary>
    /// Error checking mode for instruction blocks (err = ...).
    /// 0 = ignore, 1 = allow only FimSe without Se, 2 = check all.
    /// </summary>
    public int ErrorMode { get; set; } = 1;

    /// <summary>
    /// Whether the program runs without restrictions (completo = 1).
    /// </summary>
    public bool FullAccess { get; set; }

    /// <summary>
    /// Other settings not explicitly handled.
    /// </summary>
    public Dictionary<string, string> OtherSettings { get; } = new(StringComparer.OrdinalIgnoreCase);
}
