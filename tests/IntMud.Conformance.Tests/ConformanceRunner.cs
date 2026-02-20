using System.Reflection;
using System.Text;
using IntMud.Compiler.Bytecode;
using IntMud.Compiler.Parsing;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;

namespace IntMud.Conformance.Tests;

/// <summary>
/// Result of running a conformance test script.
/// </summary>
public class ConformanceResult
{
    /// <summary>
    /// The name of the test script (without extension).
    /// </summary>
    public required string ScriptName { get; init; }

    /// <summary>
    /// Whether the script parsed successfully.
    /// </summary>
    public bool ParseSucceeded { get; set; }

    /// <summary>
    /// Whether the script compiled successfully.
    /// </summary>
    public bool CompileSucceeded { get; set; }

    /// <summary>
    /// Whether the script executed successfully (no unhandled exceptions).
    /// </summary>
    public bool ExecuteSucceeded { get; set; }

    /// <summary>
    /// The captured output from execution.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// The expected output loaded from the .expected golden file (null if no golden file exists).
    /// </summary>
    public string? ExpectedOutput { get; set; }

    /// <summary>
    /// Error message if any phase failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The names of all classes found in the script.
    /// </summary>
    public List<string> ClassNames { get; } = new();

    /// <summary>
    /// Whether the output matches the expected golden file.
    /// </summary>
    public bool OutputMatches =>
        ExpectedOutput == null || NormalizeLineEndings(Output) == NormalizeLineEndings(ExpectedOutput);

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n', '\r');
}

/// <summary>
/// Runs IntMUD conformance tests by parsing, compiling, and executing .int source files,
/// then comparing their output against golden .expected files.
/// </summary>
public static class ConformanceRunner
{
    private static readonly Assembly TestAssembly = typeof(ConformanceRunner).Assembly;

    /// <summary>
    /// Gets all available test script names (without the .int extension) from embedded resources.
    /// </summary>
    public static IEnumerable<string> GetAvailableTestScripts()
    {
        var prefix = "IntMud.Conformance.Tests.TestScripts.";
        var suffix = ".int";

        return TestAssembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                     && n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(n => n[prefix.Length..^suffix.Length])
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads the source code for a test script from embedded resources.
    /// </summary>
    public static string LoadScriptSource(string scriptName)
    {
        var resourceName = $"IntMud.Conformance.Tests.TestScripts.{scriptName}.int";
        using var stream = TestAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

        // Read raw bytes first, then detect encoding (same logic as IntMudSourceParser.ReadSourceFile)
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var bytes = memoryStream.ToArray();

        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8, assume Latin1 (ISO-8859-1) for legacy files
            var latin1 = Encoding.GetEncoding("ISO-8859-1");
            return latin1.GetString(bytes);
        }
    }

    /// <summary>
    /// Loads the expected output for a test script from embedded resources.
    /// Returns null if no .expected file exists.
    /// </summary>
    public static string? LoadExpectedOutput(string scriptName)
    {
        var resourceName = $"IntMud.Conformance.Tests.TestScripts.{scriptName}.expected";
        using var stream = TestAssembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Runs a single conformance test by name.
    /// Parses the .int source, compiles it to bytecode, executes it, and captures output.
    /// </summary>
    public static ConformanceResult Run(string scriptName)
    {
        var result = new ConformanceResult { ScriptName = scriptName };

        try
        {
            // Load source and expected output
            var source = LoadScriptSource(scriptName);
            result.ExpectedOutput = LoadExpectedOutput(scriptName);

            // Phase 1: Parse
            var parser = new IntMudSourceParser();
            var normalizedSource = IntMudSourceParser.NormalizeEncoding(source);
            var ast = parser.Parse(normalizedSource, $"{scriptName}.int");
            result.ParseSucceeded = true;

            // Collect class names from the AST
            foreach (var cls in ast.Classes)
            {
                result.ClassNames.Add(cls.Name);
            }

            // Phase 2: Compile all classes to bytecode
            var units = BytecodeCompiler.CompileAll(ast);
            result.CompileSucceeded = true;

            // Phase 3: Execute using IntMudRuntime for proper initialization
            // Build a dictionary of all compiled units for cross-class references
            var loadedUnits = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase);
            foreach (var unit in units)
            {
                loadedUnits[unit.ClassName] = unit;
            }

            // Use IntMudRuntime which properly calls iniclasse on all classes
            // with the class name as arg0 (matching C++ behavior)
            var runtime = new IntMudRuntime(loadedUnits);

            // Capture output
            var outputBuilder = new StringBuilder();
            runtime.OnOutput += text => outputBuilder.Append(text);

            try
            {
                // Initialize calls iniclasse on all classes, creating objects
                runtime.Initialize();
            }
            catch (TerminateException)
            {
                // Normal termination via 'terminar' instruction - this is expected
            }

            result.Output = outputBuilder.ToString();
            result.ExecuteSucceeded = true;
        }
        catch (ParseException ex)
        {
            result.Error = $"Parse error: {ex.Message}";
        }
        catch (CompilerException ex)
        {
            result.ParseSucceeded = true; // Parse succeeded but compile failed
            result.Error = $"Compile error: {ex.Message}";
        }
        catch (RuntimeException ex)
        {
            result.ParseSucceeded = true;
            result.CompileSucceeded = true;
            result.Error = $"Runtime error: {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Error = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Runs only the parse phase for a test script.
    /// Useful for verifying parser coverage without requiring full execution.
    /// </summary>
    public static ConformanceResult ParseOnly(string scriptName)
    {
        var result = new ConformanceResult { ScriptName = scriptName };

        try
        {
            var source = LoadScriptSource(scriptName);
            var parser = new IntMudSourceParser();
            var normalizedSource = IntMudSourceParser.NormalizeEncoding(source);
            var ast = parser.Parse(normalizedSource, $"{scriptName}.int");
            result.ParseSucceeded = true;

            foreach (var cls in ast.Classes)
            {
                result.ClassNames.Add(cls.Name);
            }
        }
        catch (ParseException ex)
        {
            result.Error = $"Parse error: {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Error = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Runs parse and compile phases for a test script.
    /// Useful for verifying compiler coverage without requiring full execution.
    /// </summary>
    public static ConformanceResult CompileOnly(string scriptName)
    {
        var result = new ConformanceResult { ScriptName = scriptName };

        try
        {
            var source = LoadScriptSource(scriptName);
            var parser = new IntMudSourceParser();
            var normalizedSource = IntMudSourceParser.NormalizeEncoding(source);
            var ast = parser.Parse(normalizedSource, $"{scriptName}.int");
            result.ParseSucceeded = true;

            foreach (var cls in ast.Classes)
            {
                result.ClassNames.Add(cls.Name);
            }

            var units = BytecodeCompiler.CompileAll(ast);
            result.CompileSucceeded = true;
        }
        catch (ParseException ex)
        {
            result.Error = $"Parse error: {ex.Message}";
        }
        catch (CompilerException ex)
        {
            result.ParseSucceeded = true;
            result.Error = $"Compile error: {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Error = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Runs a conformance test from inline source code (for focused testing).
    /// </summary>
    public static ConformanceResult RunFromSource(string source)
    {
        var result = new ConformanceResult { ScriptName = "inline" };

        try
        {
            var parser = new IntMudSourceParser();
            var normalizedSource = IntMudSourceParser.NormalizeEncoding(source);
            var ast = parser.Parse(normalizedSource, "inline.int");
            result.ParseSucceeded = true;

            foreach (var cls in ast.Classes)
                result.ClassNames.Add(cls.Name);

            var units = BytecodeCompiler.CompileAll(ast);
            result.CompileSucceeded = true;

            var loadedUnits = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase);
            foreach (var unit in units)
                loadedUnits[unit.ClassName] = unit;

            var runtime = new IntMudRuntime(loadedUnits);
            var outputBuilder = new StringBuilder();
            runtime.OnOutput += text => outputBuilder.Append(text);

            try
            {
                runtime.Initialize();
            }
            catch (TerminateException) { }

            result.Output = outputBuilder.ToString();
            result.ExecuteSucceeded = true;
        }
        catch (ParseException ex)
        {
            result.Error = $"Parse error: {ex.Message}";
        }
        catch (CompilerException ex)
        {
            result.ParseSucceeded = true;
            result.Error = $"Compile error: {ex.Message}";
        }
        catch (RuntimeException ex)
        {
            result.ParseSucceeded = true;
            result.CompileSucceeded = true;
            result.Error = $"Runtime error: {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Error = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }
}
