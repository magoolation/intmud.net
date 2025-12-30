using IntMud.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Parse command line arguments
var port = 4000;
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
            Console.WriteLine("IntMUD Server - .NET Runtime");
            Console.WriteLine();
            Console.WriteLine("Usage: IntMud.Console [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -p, --port <port>      Server port (default: 4000)");
            Console.WriteLine("  -s, --source <path>    Source files path (default: .)");
            Console.WriteLine("  -h, --help             Show this help message");
            return;
    }
}

Console.WriteLine("╔═══════════════════════════════════════════════════╗");
Console.WriteLine("║           IntMUD Server - .NET Runtime            ║");
Console.WriteLine("╚═══════════════════════════════════════════════════╝");
Console.WriteLine();

var builder = new IntMudHostBuilder(args)
    .Configure(options =>
    {
        options.ServerPort = port;
        options.SourcePath = sourcePath;
        options.DebugMode = true;
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    });

var host = builder.Build();

Console.WriteLine($"Starting server on port {port}...");
Console.WriteLine($"Source path: {Path.GetFullPath(sourcePath)}");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop the server.");
Console.WriteLine();

await host.RunAsync();
