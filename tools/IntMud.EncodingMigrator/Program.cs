using System.Text;

namespace IntMud.EncodingMigrator;

/// <summary>
/// Tool for migrating IntMUD source files from ISO8859-1 to UTF-8.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "detect" => await DetectEncodingAsync(args.Skip(1).ToArray()),
            "migrate" => await MigrateFilesAsync(args.Skip(1).ToArray()),
            "check" => await CheckFilesAsync(args.Skip(1).ToArray()),
            _ => PrintUsage()
        };
    }

    private static int PrintUsage()
    {
        Console.WriteLine("IntMUD Encoding Migrator - ISO8859-1 to UTF-8");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  IntMud.EncodingMigrator detect <path>     - Detect encoding of files");
        Console.WriteLine("  IntMud.EncodingMigrator migrate <path>    - Migrate files to UTF-8");
        Console.WriteLine("  IntMud.EncodingMigrator check <path>      - Check files for encoding issues");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  <path>  Path to a .int file or directory containing .int files");
        return 1;
    }

    private static async Task<int> DetectEncodingAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Path required");
            return 1;
        }

        var path = args[0];
        var files = GetIntFiles(path);

        foreach (var file in files)
        {
            var encoding = await DetectFileEncodingAsync(file);
            Console.WriteLine($"{encoding,-12} {file}");
        }

        return 0;
    }

    private static async Task<int> MigrateFilesAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Path required");
            return 1;
        }

        var path = args[0];
        var files = GetIntFiles(path);
        var migrated = 0;
        var skipped = 0;

        foreach (var file in files)
        {
            var encoding = await DetectFileEncodingAsync(file);

            if (encoding == "UTF-8")
            {
                skipped++;
                continue;
            }

            await MigrateFileAsync(file);
            migrated++;
            Console.WriteLine($"Migrated: {file}");
        }

        Console.WriteLine();
        Console.WriteLine($"Migrated: {migrated} files");
        Console.WriteLine($"Skipped:  {skipped} files (already UTF-8)");

        return 0;
    }

    private static async Task<int> CheckFilesAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Path required");
            return 1;
        }

        var path = args[0];
        var files = GetIntFiles(path);
        var issues = 0;

        foreach (var file in files)
        {
            var fileIssues = await CheckFileAsync(file);
            if (fileIssues.Count > 0)
            {
                issues += fileIssues.Count;
                Console.WriteLine($"{file}:");
                foreach (var issue in fileIssues)
                {
                    Console.WriteLine($"  Line {issue.Line}: {issue.Message}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total issues: {issues}");

        return issues > 0 ? 1 : 0;
    }

    private static IEnumerable<string> GetIntFiles(string path)
    {
        if (File.Exists(path))
        {
            return [path];
        }

        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*.int", SearchOption.AllDirectories);
        }

        Console.Error.WriteLine($"Error: Path not found: {path}");
        return [];
    }

    private static async Task<string> DetectFileEncodingAsync(string file)
    {
        var bytes = await File.ReadAllBytesAsync(file);

        // Check for UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return "UTF-8-BOM";
        }

        // Check if valid UTF-8
        if (IsValidUtf8(bytes))
        {
            // Check if it contains any non-ASCII that is valid UTF-8
            if (bytes.Any(b => b > 127))
            {
                return "UTF-8";
            }
            return "ASCII";
        }

        // Assume ISO8859-1 (Latin-1)
        return "ISO8859-1";
    }

    private static bool IsValidUtf8(byte[] bytes)
    {
        var i = 0;
        while (i < bytes.Length)
        {
            if (bytes[i] <= 0x7F)
            {
                // ASCII
                i++;
            }
            else if ((bytes[i] & 0xE0) == 0xC0)
            {
                // 2-byte sequence
                if (i + 1 >= bytes.Length || (bytes[i + 1] & 0xC0) != 0x80)
                    return false;
                i += 2;
            }
            else if ((bytes[i] & 0xF0) == 0xE0)
            {
                // 3-byte sequence
                if (i + 2 >= bytes.Length || (bytes[i + 1] & 0xC0) != 0x80 || (bytes[i + 2] & 0xC0) != 0x80)
                    return false;
                i += 3;
            }
            else if ((bytes[i] & 0xF8) == 0xF0)
            {
                // 4-byte sequence
                if (i + 3 >= bytes.Length || (bytes[i + 1] & 0xC0) != 0x80 ||
                    (bytes[i + 2] & 0xC0) != 0x80 || (bytes[i + 3] & 0xC0) != 0x80)
                    return false;
                i += 4;
            }
            else
            {
                // Invalid UTF-8 start byte
                return false;
            }
        }
        return true;
    }

    private static async Task MigrateFileAsync(string file)
    {
        // Read as ISO8859-1
        var iso8859 = Encoding.GetEncoding("ISO-8859-1");
        var content = await File.ReadAllTextAsync(file, iso8859);

        // Write as UTF-8 (no BOM)
        await File.WriteAllTextAsync(file, content, new UTF8Encoding(false));
    }

    private static async Task<List<EncodingIssue>> CheckFileAsync(string file)
    {
        var issues = new List<EncodingIssue>();
        var bytes = await File.ReadAllBytesAsync(file);
        var lineNumber = 1;
        var lineStart = 0;

        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == '\n')
            {
                lineNumber++;
                lineStart = i + 1;
            }
            else if (bytes[i] > 127)
            {
                // Check if this is part of a valid UTF-8 sequence
                if (!IsValidUtf8Sequence(bytes, i))
                {
                    var col = i - lineStart + 1;
                    issues.Add(new EncodingIssue(lineNumber, $"Invalid byte 0x{bytes[i]:X2} at column {col}"));
                }
            }
        }

        return issues;
    }

    private static bool IsValidUtf8Sequence(byte[] bytes, int index)
    {
        var b = bytes[index];

        if ((b & 0xE0) == 0xC0)
        {
            return index + 1 < bytes.Length && (bytes[index + 1] & 0xC0) == 0x80;
        }
        if ((b & 0xF0) == 0xE0)
        {
            return index + 2 < bytes.Length &&
                   (bytes[index + 1] & 0xC0) == 0x80 &&
                   (bytes[index + 2] & 0xC0) == 0x80;
        }
        if ((b & 0xF8) == 0xF0)
        {
            return index + 3 < bytes.Length &&
                   (bytes[index + 1] & 0xC0) == 0x80 &&
                   (bytes[index + 2] & 0xC0) == 0x80 &&
                   (bytes[index + 3] & 0xC0) == 0x80;
        }

        return false;
    }

    private record EncodingIssue(int Line, string Message);
}
