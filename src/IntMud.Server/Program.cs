using System.Text;
using IntMud.Compiler.Bytecode;
using IntMud.Compiler.Parsing;
using IntMud.Hosting;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Parse command line arguments
int? port = null;  // null = no server port
string? mainFile = null;  // Main .int file

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-p":
        case "--port":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
            {
                port = p;
                i++;
            }
            break;
        case "-h":
        case "--help":
            Console.WriteLine("IntMUD Server - .NET Runtime");
            Console.WriteLine();
            Console.WriteLine("Usage: IntMud.Server [file.int] [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  file.int                   Main .int file (e.g., mud/mud.int)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -p, --port <port>          Server port (enables telnet server)");
            Console.WriteLine("  -h, --help                 Show this help message");
            Console.WriteLine();
            Console.WriteLine("The .int file contains configuration and class definitions.");
            Console.WriteLine("Use 'serv' type in scripts to open ports dynamically.");
            return;
        default:
            // If it's not a flag, treat it as the main file
            if (!args[i].StartsWith("-") && mainFile == null)
            {
                mainFile = args[i];
            }
            break;
    }
}

// Determine the main .int file
string sourcePath;
if (mainFile != null)
{
    mainFile = Path.GetFullPath(mainFile);
    if (!File.Exists(mainFile))
    {
        Console.Error.WriteLine($"Error: Main .int file not found: {mainFile}");
        return;
    }
    sourcePath = Path.GetDirectoryName(mainFile)!;
}
else
{
    // Use executable name to find .int file (like original IntMUD)
    var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "intmud");
    mainFile = $"{exeName}.int";

    if (File.Exists(mainFile))
    {
        mainFile = Path.GetFullPath(mainFile);
        sourcePath = Path.GetDirectoryName(mainFile)!;
    }
    else
    {
        // Fall back to current directory
        sourcePath = Directory.GetCurrentDirectory();
        mainFile = null;
    }
}

var isServerMode = port.HasValue;
var title = isServerMode ? "IntMUD Server - .NET Runtime" : "IntMUD Interpreter - .NET Runtime";

Console.WriteLine("=======================================================");
Console.WriteLine($"  {title}");
Console.WriteLine("=======================================================");
Console.WriteLine();

if (mainFile != null)
{
    Console.WriteLine($"Main file: {mainFile}");
}
Console.WriteLine($"Base directory: {sourcePath}");
Console.WriteLine();

// Server mode uses the Generic Host
if (isServerMode)
{
    var builder = new IntMudHostBuilder(args)
        .Configure(options =>
        {
            options.ServerPort = port ?? 0;
            options.SourcePath = sourcePath;
            if (mainFile != null)
            {
                options.MainFile = Path.GetFileName(mainFile);
            }
            options.DebugMode = true;
        })
        .ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddConsole();
        });

    var host = builder.Build();

    Console.WriteLine($"Starting server on port {port}...");
    Console.WriteLine();
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine();

    await host.RunAsync();
}
else
{
    // Interpreter mode - run synchronously on main thread (like original IntMUD)
    Console.WriteLine("Running in interpreter mode (no server).");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine();

    await RunInterpreterModeAsync(sourcePath, mainFile);
}

/// <summary>
/// Run in interpreter mode - synchronous event loop on main thread.
/// This matches the original IntMUD behavior where everything runs in a single thread.
/// </summary>
static async Task RunInterpreterModeAsync(string sourcePath, string? mainFile)
{
    // Load and compile source files
    var compiledUnits = await LoadSourceFilesAsync(sourcePath, mainFile);
    if (compiledUnits.Count == 0)
    {
        Console.WriteLine("No classes loaded. Nothing to execute.");
        return;
    }

    Console.WriteLine($"Loaded {compiledUnits.Count} classes.");
    Console.WriteLine();

    // Create and initialize runtime
    var runtime = new IntMudRuntime(compiledUnits);

    // Wire up I/O - this is critical for the interpreter to work
    runtime.OnOutput += text =>
    {
        Console.Write(text);
        Console.Out.Flush();
    };

    runtime.OnReadKey += () =>
    {
        // Non-blocking key read - must be on main thread for Windows console
        try
        {
            if (Console.IsInputRedirected)
                return null;

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                return KeyToString(key);
            }
        }
        catch (InvalidOperationException)
        {
            // Console not available
        }
        return null;
    };

    runtime.OnTerminate += () =>
    {
        Console.WriteLine("\nRuntime terminated.");
    };

    // Initialize - calls iniclasse on all classes
    Console.WriteLine("Initializing...");
    runtime.Initialize();
    Console.WriteLine($"Created {runtime.Instances.Count} object instances.");
    Console.WriteLine();

    // Setup Ctrl+C handler
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("\nShutting down...");
    };

    // Run the event loop synchronously on main thread
    // This is the same as original IntMUD's while(true) loop
    Console.WriteLine("Event loop started. Press Ctrl+C to exit.");
    Console.WriteLine();

    runtime.RunSync(cts.Token);

    runtime.Dispose();
    Console.WriteLine("Bye!");
}

static async Task<Dictionary<string, CompiledUnit>> LoadSourceFilesAsync(string sourcePath, string? mainFile)
{
    var compiledUnits = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase);

    if (!Directory.Exists(sourcePath))
    {
        Console.WriteLine($"Source path does not exist: {sourcePath}");
        return compiledUnits;
    }

    // Determine the main config file name
    string mainConfigFile;
    if (!string.IsNullOrEmpty(mainFile))
    {
        mainConfigFile = mainFile;
    }
    else
    {
        var dirName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        mainConfigFile = Path.Combine(sourcePath, $"{dirName}.int");
    }

    // Try to load the config file
    var configParser = new IntMudConfigParser();
    var includeDirs = new List<string>();

    if (File.Exists(mainConfigFile))
    {
        try
        {
            var configContent = await File.ReadAllTextAsync(mainConfigFile, Encoding.Latin1);
            var config = configParser.Parse(configContent, mainConfigFile);
            Console.WriteLine($"Config: telatxt={config.TelaTxt}, includes={config.Includes.Count}");

            // Resolve include directories
            foreach (var include in config.Includes)
            {
                var includePath = Path.Combine(sourcePath, include.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(includePath))
                {
                    includeDirs.Add(includePath);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error loading config: {ex.Message}");
        }
    }

    // Collect files to compile
    var allFiles = new List<string>();

    // If includes are specified, load from those directories
    if (includeDirs.Count > 0)
    {
        foreach (var dir in includeDirs)
        {
            var files = Directory.GetFiles(dir, "*.int", SearchOption.AllDirectories);
            allFiles.AddRange(files);
        }
    }

    // ALWAYS add the main config file first (it may contain class definitions)
    // This ensures the main file is loaded with priority
    if (!string.IsNullOrEmpty(mainConfigFile) && File.Exists(mainConfigFile))
    {
        // Insert at beginning so it has priority
        allFiles.Insert(0, mainConfigFile);
    }

    // If no includes and no main file classes, we have nothing to load
    if (allFiles.Count == 0)
    {
        Console.WriteLine("No source files found to compile.");
        return compiledUnits;
    }

    Console.WriteLine($"Found {allFiles.Count} source files.");

    var parser = new IntMudSourceParser();
    var compiledCount = 0;
    var errorCount = 0;

    foreach (var file in allFiles)
    {
        try
        {
            var source = await File.ReadAllTextAsync(file, Encoding.Latin1);
            var ast = parser.Parse(source, file);

            if (ast.Classes.Count > 0)
            {
                // Compile ALL classes in the file (IntMUD supports multiple classes per file)
                var units = BytecodeCompiler.CompileAll(ast);

                foreach (var unit in units)
                {
                    // Check for class name conflicts
                    if (compiledUnits.ContainsKey(unit.ClassName))
                    {
                        Console.WriteLine($"[WARNING] Class '{unit.ClassName}' from {Path.GetFileName(file)} conflicts with existing class. Skipping.");
                        continue;
                    }

                    compiledUnits[unit.ClassName] = unit;
                    compiledCount++;
                }
            }
        }
        catch (Exception ex)
        {
            errorCount++;
            Console.WriteLine($"Error compiling {Path.GetFileName(file)}: {ex.Message}");
        }
    }

    if (errorCount > 0)
    {
        Console.WriteLine($"Warning: {errorCount} files had compilation errors.");
    }

    return compiledUnits;
}

static string KeyToString(ConsoleKeyInfo key)
{
    return key.Key switch
    {
        ConsoleKey.Enter => "ENTER",
        ConsoleKey.Escape => "ESC",
        ConsoleKey.UpArrow => "UP",
        ConsoleKey.DownArrow => "DOWN",
        ConsoleKey.LeftArrow => "LEFT",
        ConsoleKey.RightArrow => "RIGHT",
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
        ConsoleKey.Backspace => "BACKSPACE",
        ConsoleKey.Delete => "DELETE",
        ConsoleKey.Home => "HOME",
        ConsoleKey.End => "END",
        ConsoleKey.PageUp => "PAGEUP",
        ConsoleKey.PageDown => "PAGEDOWN",
        ConsoleKey.Tab => "TAB",
        _ => key.KeyChar != '\0' ? key.KeyChar.ToString() : key.Key.ToString()
    };
}
