using System.Text;
using IntMud.Compiler.Bytecode;
using IntMud.Compiler.Parsing;
using IntMud.Runtime.Execution;

namespace IntMud.Interpreter;

/// <summary>
/// IntMUD Interpreter - Console mode only.
/// This mimics the original IntMUD behavior with telatxt (text console).
///
/// Usage: IntMud.Interpreter [file.int]
///
/// If no file is specified, uses the executable name to find {name}.int in the current directory.
/// The .int file contains configuration (incluir = path/, telatxt = 1, etc.) followed by class definitions.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.Latin1;
        Console.InputEncoding = Encoding.Latin1;

        if (args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine("IntMUD Interpreter - Console mode");
            Console.WriteLine();
            Console.WriteLine("Usage: IntMud.Interpreter [file.int]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  file.int    Path to the main .int file (e.g., mud/mud.int)");
            Console.WriteLine();
            Console.WriteLine("If no file is specified, looks for {exename}.int in current directory.");
            Console.WriteLine("The .int file contains configuration directives followed by class definitions.");
            Console.WriteLine();
            Console.WriteLine("Configuration directives (before first 'classe'):");
            Console.WriteLine("  incluir = path/    Include .int files from directory");
            Console.WriteLine("  telatxt = 1        Enable console mode");
            Console.WriteLine("  exec = 10000       Max instructions per function call");
            Console.WriteLine("  log = 0            Error logging mode (0=console)");
            Console.WriteLine("  err = 1            Error check level (0-2)");
            Console.WriteLine("  completo = 0       Full access mode (0=restricted)");
            Console.WriteLine();
            Console.WriteLine("Press ESC to exit during execution.");
            return 0;
        }

        // Determine the main .int file
        // If argument provided, use it; otherwise use {exename}.int
        string mainIntFile;
        if (args.Length > 0 && !args[0].StartsWith("-"))
        {
            mainIntFile = args[0];
        }
        else
        {
            // Use executable name to find .int file (like original IntMUD)
            var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "intmud");
            mainIntFile = $"{exeName}.int";
        }

        mainIntFile = Path.GetFullPath(mainIntFile);

        if (!File.Exists(mainIntFile))
        {
            Console.Error.WriteLine($"Error: Main .int file not found: {mainIntFile}");
            return 1;
        }

        // Change to the directory containing the .int file (like original IntMUD)
        var baseDir = Path.GetDirectoryName(mainIntFile)!;
        var baseName = Path.GetFileNameWithoutExtension(mainIntFile);
        Environment.CurrentDirectory = baseDir;

        Console.WriteLine("IntMUD Interpreter - Console Mode");
        Console.WriteLine($"Main file: {mainIntFile}");
        Console.WriteLine($"Base directory: {baseDir}");
        Console.WriteLine();

        try
        {
            var interpreter = new MudInterpreter(mainIntFile, baseDir, baseName);
            await interpreter.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}

/// <summary>
/// MUD interpreter that follows the original IntMUD C++ behavior:
/// 1. Read main .int file for configuration (before first 'classe')
/// 2. Process 'incluir = path/' directives to find source directories
/// 3. Scan directories for all .int files
/// 4. Compile all classes from all files
/// 5. Run the runtime with telatxt console support
/// </summary>
class MudInterpreter
{
    private readonly string _mainFile;
    private readonly string _baseDir;
    private readonly string _baseName;
    private readonly Dictionary<string, CompiledUnit> _compiledUnits = new(StringComparer.OrdinalIgnoreCase);
    private IntMudConfig? _config;
    private IntMudRuntime? _runtime;
    private bool _running;

    public MudInterpreter(string mainFile, string baseDir, string baseName)
    {
        _mainFile = mainFile;
        _baseDir = baseDir;
        _baseName = baseName;
    }

    public async Task RunAsync()
    {
        // Phase 1: Load and parse main .int file to get configuration
        await LoadConfigurationAsync();

        // Phase 2: Load all source files based on configuration
        await LoadSourceFilesAsync();

        if (_compiledUnits.Count == 0)
        {
            Console.WriteLine("No classes loaded. Nothing to run.");
            return;
        }

        Console.WriteLine($"Loaded {_compiledUnits.Count} classes.");
        Console.WriteLine();

        // Check if telatxt is enabled
        if (_config != null && !_config.TelaTxt)
        {
            Console.WriteLine("Warning: telatxt is disabled in configuration.");
            Console.WriteLine("Running in headless mode (no console interaction).");
        }

        Console.WriteLine("Initializing...");

        // Create and start the runtime
        _runtime = new IntMudRuntime(_compiledUnits);
        _runtime.OnOutput += Console.Write;
        _runtime.OnReadKey += ReadKey;
        _runtime.OnTerminate += () => _running = false;

        // Initialize (calls iniclasse on all classes)
        _runtime.Initialize();

        Console.WriteLine($"Created {_runtime.Instances.Count} instances with special types.");
        Console.WriteLine();
        Console.WriteLine("Starting event loop. Press ESC to exit.");
        Console.WriteLine();

        // Start the runtime (this starts the event loop)
        _running = true;
        _runtime.Start();

        // Wait for termination
        while (_running)
        {
            await Task.Delay(100);
        }

        _runtime.Stop();
        Console.WriteLine();
        Console.WriteLine("Interpreter stopped.");
    }

    private async Task LoadConfigurationAsync()
    {
        Console.WriteLine($"Reading configuration from: {Path.GetFileName(_mainFile)}");

        var content = await File.ReadAllTextAsync(_mainFile, Encoding.Latin1);

        // Parse configuration (lines before first 'classe' definition)
        var configParser = new IntMudConfigParser();
        _config = configParser.Parse(content, _mainFile);

        Console.WriteLine($"  telatxt = {(_config.TelaTxt ? "1" : "0")}");
        Console.WriteLine($"  exec = {_config.ExecLimit}");
        Console.WriteLine($"  log = {_config.LogMode}");
        Console.WriteLine($"  err = {_config.ErrorMode}");
        Console.WriteLine($"  completo = {(_config.FullAccess ? "1" : "0")}");

        if (_config.Includes.Count > 0)
        {
            Console.WriteLine($"  incluir: {string.Join(", ", _config.Includes)}");
        }
    }

    private async Task LoadSourceFilesAsync()
    {
        // Build list of directories to scan based on 'incluir' directives
        var includeDirs = new List<string>();

        if (_config != null)
        {
            foreach (var include in _config.Includes)
            {
                // Normalize path separators
                var normalizedInclude = include.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
                var includePath = Path.Combine(_baseDir, normalizedInclude);

                if (Directory.Exists(includePath))
                {
                    includeDirs.Add(includePath);
                    Console.WriteLine($"  Include directory: {normalizedInclude}");
                }
                else
                {
                    Console.WriteLine($"  Warning: Include directory not found: {normalizedInclude}");
                }
            }
        }

        // Collect all .int files from include directories
        var allFiles = new List<string>();

        foreach (var dir in includeDirs)
        {
            try
            {
                var files = Directory.GetFiles(dir, "*.int", SearchOption.AllDirectories);
                allFiles.AddRange(files);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Error scanning {dir}: {ex.Message}");
            }
        }

        // Also add .int files from the base directory (excluding the main config file)
        try
        {
            var rootFiles = Directory.GetFiles(_baseDir, "*.int", SearchOption.TopDirectoryOnly);
            foreach (var file in rootFiles)
            {
                if (!string.Equals(file, _mainFile, StringComparison.OrdinalIgnoreCase) &&
                    !allFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                {
                    allFiles.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Error scanning base directory: {ex.Message}");
        }

        // Add the main file itself (it may contain class definitions after config)
        if (!allFiles.Contains(_mainFile, StringComparer.OrdinalIgnoreCase))
        {
            allFiles.Insert(0, _mainFile);
        }

        Console.WriteLine($"Found {allFiles.Count} source files.");
        Console.WriteLine();

        // Compile all files
        var parser = new IntMudSourceParser();
        var errorCount = 0;
        var classCount = 0;

        foreach (var file in allFiles)
        {
            try
            {
                var source = await File.ReadAllTextAsync(file, Encoding.Latin1);
                var ast = parser.Parse(source, file);

                if (ast.Classes.Count > 0)
                {
                    try
                    {
                        var unit = BytecodeCompiler.Compile(ast);
                        _compiledUnits[unit.ClassName] = unit;
                        classCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        var relativePath = GetRelativePath(file);
                        Console.WriteLine($"Error compiling {relativePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                var relativePath = GetRelativePath(file);
                Console.WriteLine($"Error parsing {relativePath}: {ex.Message}");
            }
        }

        if (errorCount > 0)
        {
            Console.WriteLine($"Warning: {errorCount} compilation errors.");
        }
    }

    private string GetRelativePath(string fullPath)
    {
        if (fullPath.StartsWith(_baseDir, StringComparison.OrdinalIgnoreCase))
        {
            var relative = fullPath[_baseDir.Length..].TrimStart(Path.DirectorySeparatorChar);
            return relative;
        }
        return fullPath;
    }

    private string? ReadKey()
    {
        if (!Console.KeyAvailable)
            return null;

        try
        {
            var key = Console.ReadKey(intercept: true);

            // Handle special keys (matching original IntMUD key names)
            return key.Key switch
            {
                ConsoleKey.Escape => "ESC",
                ConsoleKey.Enter => "ENTER",
                ConsoleKey.Backspace => "BACKSPACE",
                ConsoleKey.Tab => "TAB",
                ConsoleKey.UpArrow => "UP",
                ConsoleKey.DownArrow => "DOWN",
                ConsoleKey.LeftArrow => "LEFT",
                ConsoleKey.RightArrow => "RIGHT",
                ConsoleKey.Home => "HOME",
                ConsoleKey.End => "END",
                ConsoleKey.PageUp => "PAGEUP",
                ConsoleKey.PageDown => "PAGEDOWN",
                ConsoleKey.Insert => "INSERT",
                ConsoleKey.Delete => "DELETE",
                ConsoleKey.F1 => "F1",
                ConsoleKey.F2 => "F2",
                ConsoleKey.F3 => "F3",
                ConsoleKey.F4 => "F4",
                ConsoleKey.F5 => "F5",
                ConsoleKey.F6 => "F6",
                ConsoleKey.F7 => "F7",
                ConsoleKey.F8 => "F8",
                ConsoleKey.F9 => "F9",
                ConsoleKey.F10 => "F10",
                ConsoleKey.F11 => "F11",
                ConsoleKey.F12 => "F12",
                _ => key.KeyChar != '\0' ? key.KeyChar.ToString() : null
            };
        }
        catch
        {
            return null;
        }
    }
}
