using System.Text;
using IntMud.Compiler.Bytecode;
using IntMud.Compiler.Parsing;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Types;
using IntMud.Runtime.Values;

namespace IntMud.Interpreter;

/// <summary>
/// IntMUD Interpreter - Console mode only.
/// This mimics the original IntMUD behavior with telatxt (text console).
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.Latin1;
        Console.InputEncoding = Encoding.Latin1;

        // Parse command line
        var sourcePath = args.Length > 0 ? args[0] : ".";

        if (args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine("IntMUD Interpreter - Console mode");
            Console.WriteLine();
            Console.WriteLine("Usage: IntMud.Interpreter <source-path>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <source-path>    Path to the MUD source directory (default: .)");
            Console.WriteLine();
            Console.WriteLine("This runs the MUD in console/interpreter mode (telatxt).");
            Console.WriteLine("Press ESC to exit, F10 to reload.");
            return 0;
        }

        sourcePath = Path.GetFullPath(sourcePath);

        if (!Directory.Exists(sourcePath))
        {
            Console.Error.WriteLine($"Error: Source path not found: {sourcePath}");
            return 1;
        }

        Console.WriteLine("IntMUD Interpreter - Console Mode");
        Console.WriteLine($"Source: {sourcePath}");
        Console.WriteLine();

        try
        {
            var interpreter = new MudInterpreter(sourcePath);
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
/// Simple MUD interpreter that runs in console mode.
/// </summary>
class MudInterpreter
{
    private readonly string _sourcePath;
    private readonly Dictionary<string, CompiledUnit> _compiledUnits = new(StringComparer.OrdinalIgnoreCase);
    private IntMudRuntime? _runtime;
    private bool _running;

    public MudInterpreter(string sourcePath)
    {
        _sourcePath = sourcePath;
    }

    public async Task RunAsync()
    {
        // Load and compile all source files
        await LoadSourceFilesAsync();

        if (_compiledUnits.Count == 0)
        {
            Console.WriteLine("No classes loaded. Nothing to run.");
            return;
        }

        Console.WriteLine($"Loaded {_compiledUnits.Count} classes.");
        Console.WriteLine();
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

    private string? ReadKey()
    {
        if (!Console.KeyAvailable)
            return null;

        try
        {
            var key = Console.ReadKey(intercept: true);

            // Handle special keys
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

    private async Task LoadSourceFilesAsync()
    {
        // Find the main config file (same name as directory)
        var dirName = Path.GetFileName(_sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var mainConfigFile = Path.Combine(_sourcePath, $"{dirName}.int");

        var includeDirs = new List<string>();

        // Try to parse config file for includes
        if (File.Exists(mainConfigFile))
        {
            try
            {
                var configContent = await File.ReadAllTextAsync(mainConfigFile, Encoding.Latin1);
                var configParser = new IntMudConfigParser();
                var config = configParser.Parse(configContent, mainConfigFile);

                Console.WriteLine($"Config: {Path.GetFileName(mainConfigFile)}");
                Console.WriteLine($"  Includes: {config.Includes.Count}");

                foreach (var include in config.Includes)
                {
                    var includePath = Path.Combine(_sourcePath, include.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(includePath))
                    {
                        includeDirs.Add(includePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not parse config file: {ex.Message}");
            }
        }

        // If no includes, scan all subdirectories
        if (includeDirs.Count == 0)
        {
            includeDirs.Add(_sourcePath);
        }

        // Collect all .int files
        var allFiles = new List<string>();
        foreach (var dir in includeDirs)
        {
            var files = Directory.GetFiles(dir, "*.int", SearchOption.AllDirectories);
            allFiles.AddRange(files);
        }

        // Add root files (excluding config)
        var rootFiles = Directory.GetFiles(_sourcePath, "*.int", SearchOption.TopDirectoryOnly);
        foreach (var file in rootFiles)
        {
            if (!string.Equals(file, mainConfigFile, StringComparison.OrdinalIgnoreCase) &&
                !allFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                allFiles.Add(file);
            }
        }

        Console.WriteLine($"Found {allFiles.Count} source files.");

        // Compile all files
        var parser = new IntMudSourceParser();
        var errorCount = 0;

        foreach (var file in allFiles)
        {
            try
            {
                var source = await File.ReadAllTextAsync(file, Encoding.Latin1);
                var ast = parser.Parse(source, file);

                if (ast.Classes.Count > 0)
                {
                    var unit = BytecodeCompiler.Compile(ast);
                    _compiledUnits[unit.ClassName] = unit;
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                var relativePath = file.Replace(_sourcePath + Path.DirectorySeparatorChar, "");
                Console.WriteLine($"Error in {relativePath}: {ex.Message}");
            }
        }

        if (errorCount > 0)
        {
            Console.WriteLine($"Warning: {errorCount} files had errors.");
        }
    }
}
