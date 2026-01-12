using IntMud.Hosting;
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

var builder = new IntMudHostBuilder(args)
    .Configure(options =>
    {
        options.ServerPort = port ?? 0;  // 0 = no server
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

if (isServerMode)
{
    Console.WriteLine($"Starting server on port {port}...");
}
else
{
    Console.WriteLine("Running in interpreter mode (no server).");
    Console.WriteLine("Use 'serv' type in scripts to open ports dynamically.");
}
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await host.RunAsync();
