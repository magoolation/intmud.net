using IntMud.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Parse command line arguments
int? port = null;  // null = interpreter mode (no server)
var sourcePath = ".";

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
        case "-s":
        case "--source":
            if (i + 1 < args.Length)
            {
                sourcePath = args[i + 1];
                i++;
            }
            break;
        case "-h":
        case "--help":
            Console.WriteLine("IntMUD - .NET Runtime");
            Console.WriteLine();
            Console.WriteLine("Usage: IntMud.Console [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -p, --port <port>      Enable server on specified port");
            Console.WriteLine("  -s, --source <path>    Source files path (default: .)");
            Console.WriteLine("  -h, --help             Show this help message");
            Console.WriteLine();
            Console.WriteLine("Without --port, runs as interpreter only (no telnet server).");
            Console.WriteLine("Use 'serv' type in scripts to open ports dynamically.");
            return;
    }
}

var isServerMode = port.HasValue;
var title = isServerMode ? "IntMUD Server - .NET Runtime" : "IntMUD Interpreter - .NET Runtime";

Console.WriteLine("╔═══════════════════════════════════════════════════╗");
Console.WriteLine($"║{title,51}║");
Console.WriteLine("╚═══════════════════════════════════════════════════╝");
Console.WriteLine();

var builder = new IntMudHostBuilder(args)
    .Configure(options =>
    {
        options.ServerPort = port ?? 0;  // 0 = no server
        options.SourcePath = sourcePath;
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
Console.WriteLine($"Source path: {Path.GetFullPath(sourcePath)}");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await host.RunAsync();
