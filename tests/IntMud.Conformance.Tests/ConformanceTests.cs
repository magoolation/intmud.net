using System.Text;
using IntMud.Compiler.Bytecode;
using IntMud.Compiler.Parsing;

namespace IntMud.Conformance.Tests;

/// <summary>
/// Provides test data for conformance tests by discovering all embedded .int test scripts.
/// </summary>
public class TestScriptDataAttribute : ClassDataAttribute
{
    public TestScriptDataAttribute() : base(typeof(TestScriptData)) { }
}

/// <summary>
/// Enumerates all available .int test scripts as xUnit theory data.
/// </summary>
public class TestScriptData : TheoryData<string>
{
    public TestScriptData()
    {
        foreach (var scriptName in ConformanceRunner.GetAvailableTestScripts())
        {
            Add(scriptName);
        }
    }
}

/// <summary>
/// Conformance tests that verify .int source files can be parsed, compiled, and executed
/// correctly by the IntMUD .NET implementation.
///
/// Each test loads an .int file from the embedded TestScripts directory, runs it through
/// the full pipeline (parse -> compile -> execute), and validates:
///   - Parse phase: The parser can handle the script without errors
///   - Compile phase: The bytecode compiler can compile the parsed AST
///   - Execute phase: The interpreter can execute the compiled bytecode
///   - Golden file: If a .expected file exists, output matches exactly
/// </summary>
public class ConformanceTests
{
    /// <summary>
    /// Verifies that each test script can be parsed without errors.
    /// This is the most basic conformance check - if the parser cannot handle
    /// the syntax of the original test scripts, we have a grammar gap.
    /// </summary>
    [Theory]
    [TestScriptData]
    public void Script_CanBeParsed(string scriptName)
    {
        var result = ConformanceRunner.ParseOnly(scriptName);

        Assert.True(result.ParseSucceeded,
            $"Script '{scriptName}' failed to parse: {result.Error}");
        Assert.NotEmpty(result.ClassNames);
    }

    /// <summary>
    /// Verifies that each test script can be compiled to bytecode after parsing.
    /// This checks that the bytecode compiler can handle all AST constructs
    /// produced by parsing the test scripts.
    /// </summary>
    [Theory]
    [TestScriptData]
    public void Script_CanBeCompiled(string scriptName)
    {
        var result = ConformanceRunner.CompileOnly(scriptName);

        Assert.True(result.ParseSucceeded,
            $"Script '{scriptName}' failed to parse: {result.Error}");
        Assert.True(result.CompileSucceeded,
            $"Script '{scriptName}' failed to compile: {result.Error}");
    }

    /// <summary>
    /// Verifies that each test script can be executed without unhandled errors.
    /// Note: Some scripts may require external resources (files, network, etc.)
    /// and are expected to fail gracefully. This test verifies no crashes occur.
    /// </summary>
    [Theory]
    [TestScriptData]
    public void Script_CanBeExecuted(string scriptName)
    {
        var result = ConformanceRunner.Run(scriptName);

        Assert.True(result.ParseSucceeded,
            $"Script '{scriptName}' failed to parse: {result.Error}");
        Assert.True(result.CompileSucceeded,
            $"Script '{scriptName}' failed to compile: {result.Error}");
        Assert.True(result.ExecuteSucceeded,
            $"Script '{scriptName}' failed to execute: {result.Error}");
    }

    /// <summary>
    /// Verifies that test scripts with golden .expected files produce the correct output.
    /// Only runs for scripts that have a corresponding .expected file.
    /// </summary>
    [Theory]
    [TestScriptData]
    public void Script_OutputMatchesGoldenFile(string scriptName)
    {
        var result = ConformanceRunner.Run(scriptName);

        // Skip scripts that don't have a golden file
        if (result.ExpectedOutput == null)
        {
            // No golden file - skip this test (not a failure)
            return;
        }

        Assert.True(result.ParseSucceeded,
            $"Script '{scriptName}' failed to parse: {result.Error}");
        Assert.True(result.CompileSucceeded,
            $"Script '{scriptName}' failed to compile: {result.Error}");
        Assert.True(result.ExecuteSucceeded,
            $"Script '{scriptName}' failed to execute: {result.Error}");
        Assert.True(result.OutputMatches,
            $"Script '{scriptName}' output does not match golden file.\n" +
            $"Expected:\n{result.ExpectedOutput}\n\n" +
            $"Actual:\n{result.Output}");
    }

    /// <summary>
    /// Verifies that the test discovery finds all expected test scripts.
    /// This is a sanity check to ensure embedded resources are correctly configured.
    /// </summary>
    [Fact]
    public void TestDiscovery_FindsAllScripts()
    {
        var scripts = ConformanceRunner.GetAvailableTestScripts().ToList();

        // We should find at least the scripts we copied from testes/
        Assert.NotEmpty(scripts);

        // Verify some known scripts are present
        Assert.Contains("txt", scripts);
        Assert.Contains("vartroca", scripts);
        Assert.Contains("prog", scripts);
    }

    /// <summary>
    /// Individual test for each known test script.
    /// These provide stable test names in the test explorer even if the
    /// data-driven discovery changes.
    /// </summary>
    [Theory]
    [InlineData("chr")]
    [InlineData("clip")]
    [InlineData("datahora")]
    [InlineData("debug")]
    [InlineData("dir")]
    [InlineData("dns")]
    [InlineData("exec")]
    [InlineData("indiceobj")]
    [InlineData("intmud")]
    [InlineData("listaobj")]
    [InlineData("listaobj2")]
    [InlineData("log")]
    [InlineData("nomeobj")]
    [InlineData("prog")]
    [InlineData("prog2")]
    [InlineData("sav")]
    [InlineData("sav2")]
    [InlineData("socket")]
    [InlineData("telatxt")]
    [InlineData("textoobj")]
    [InlineData("textotxt")]
    [InlineData("textotxtproc")]
    [InlineData("textovar")]
    [InlineData("txt")]
    [InlineData("vartroca")]
    public void Script_ParseAndCompile(string scriptName)
    {
        var result = ConformanceRunner.CompileOnly(scriptName);

        Assert.True(result.ParseSucceeded,
            $"Script '{scriptName}' failed to parse: {result.Error}");
        Assert.True(result.CompileSucceeded,
            $"Script '{scriptName}' failed to compile: {result.Error}");
    }

    /// <summary>
    /// Focused test: verifies ListaItem iteration with a minimal IntMUD script.
    /// </summary>
    [Fact]
    public void Diagnostic_ListaItemIteration()
    {
        var source = @"
classe item1
classe item2
classe item3
classe teste
const iniclasse = criar(arg0)

func ini
  criar(""item1"")
  criar(""item2"")
  criar(""item3"")
  listaobj lista
  lista.addfim($item1)
  lista.addfim($item2)
  lista.addfim($item3)
  telatxt tela
  tela.msg(""total="" + lista.total + ""\n"")
  tela.msg(""first="" + lista.objini + ""\n"")
  listaitem i = lista.ini
  tela.msg(""ini.obj="" + i.obj + ""\n"")
  tela.msg(""ini.valid="" + i + ""\n"")
  i.depois
  tela.msg(""after depois.obj="" + i.obj + ""\n"")
  i.depois
  tela.msg(""after depois2.obj="" + i.obj + ""\n"")
  i.depois
  tela.msg(""after depois3.obj="" + i.obj + ""\n"")
  listaitem j = lista.ini
  epara j.depois, j, j.depois
    tela.msg(""loop="" + j.obj + ""\n"")
  efim
  tela.msg(""done\n"")
";
        var result = ConformanceRunner.RunFromSource(source);
        var outPath = Path.Combine(Path.GetTempPath(), "intmud_listaitem_test.txt");
        File.WriteAllText(outPath, $"Success:{result.ExecuteSucceeded}\nError:{result.Error}\nOutput:\n{result.Output}");
        Assert.True(result.ExecuteSucceeded, $"Failed: {result.Error}");
        // Output the results for inspection
        Assert.Contains("total=3", result.Output);
        Assert.Contains("loop=[item2]", result.Output);
        Assert.Contains("loop=[item3]", result.Output);
        Assert.Contains("done", result.Output);
    }

    /// <summary>
    /// Focused test: verifies multi-arg addfim/addini.
    /// </summary>
    [Fact]
    public void Diagnostic_MultiArgAddFim()
    {
        var source = @"
classe item1
classe item2
classe item3
classe teste
const iniclasse = criar(arg0)

func ini
  criar(""item1"")
  criar(""item2"")
  criar(""item3"")
  listaobj lista
  lista.addfim($item1, $item2, $item3)
  telatxt tela
  tela.msg(""total="" + lista.total + ""\n"")
  tela.msg(""first="" + lista.objini + ""\n"")
  tela.msg(""last="" + lista.objfim + ""\n"")
  listaitem i = lista.ini
  epara i.depois, i, i.depois
    tela.msg(""loop="" + i.obj + ""\n"")
  efim
  tela.msg(""done\n"")
";
        var result = ConformanceRunner.RunFromSource(source);
        var outPath = Path.Combine(Path.GetTempPath(), "intmud_multiarg_test.txt");
        File.WriteAllText(outPath, $"Success:{result.ExecuteSucceeded}\nError:{result.Error}\nOutput:\n{result.Output}");
        Assert.True(result.ExecuteSucceeded, $"Failed: {result.Error}");
        Assert.Contains("total=3", result.Output);
    }

    /// <summary>
    /// Focused test: mimics exact listaobj2 mostra pattern with function call and args.
    /// </summary>
    [Fact]
    public void Diagnostic_MostraPattern()
    {
        var source = @"
classe item1
classe item2
classe item3
classe teste
const iniclasse = criar(arg0)

func mostra
  telatxt tela
  se 1
    tela.msg(""A\n"")
    tela.msg(""B\n"")
    tela.msg(""C\n"")
  senao
    tela.msg(""NEVER\n"")
  fimse

func ini
  criar(""item1"")
  criar(""item2"")
  criar(""item3"")
  listaobj lista
  mostra(lista, ""empty list"")
  lista.addfim($item1)
  mostra(lista, ""one item"")
  lista.addfim($item2, $item3)
  mostra(lista, ""three items"")
";
        var result = ConformanceRunner.RunFromSource(source);
        var outPath = Path.Combine(Path.GetTempPath(), "intmud_mostra_test.txt");
        var output = result.Output;
        var escaped = output.Replace("\r", "\\r").Replace("\n", "\\n\n");
        File.WriteAllText(outPath, $"Success:{result.ExecuteSucceeded}\nError:{result.Error}\nRaw output ({output.Length} chars):\n{escaped}\n\nOriginal:\n{output}");
        Assert.True(result.ExecuteSucceeded, $"Failed: {result.Error}");
        // mostra is called 3 times, each prints A/B/C in the se (then) branch
        Assert.Contains("A\nB\nC\n", result.Output);
    }

    /// <summary>
    /// Diagnostic test: Dumps output of all scripts to a temp file for analysis.
    /// </summary>
    [Fact]
    public void Diagnostic_DumpAllOutputs()
    {
        var sb = new StringBuilder();
        foreach (var scriptName in ConformanceRunner.GetAvailableTestScripts())
        {
            var result = ConformanceRunner.Run(scriptName);
            sb.AppendLine($"=== {scriptName} ===");
            sb.AppendLine($"Parse:{result.ParseSucceeded} Compile:{result.CompileSucceeded} Execute:{result.ExecuteSucceeded}");
            if (result.Error != null)
                sb.AppendLine($"Error: {result.Error}");
            sb.AppendLine($"Output ({result.Output.Length} chars):");
            if (!string.IsNullOrEmpty(result.Output))
                sb.AppendLine(result.Output);
            sb.AppendLine();
        }
        var outPath = Path.Combine(Path.GetTempPath(), "intmud_conformance_output.txt");
        File.WriteAllText(outPath, sb.ToString());
        // Output the path so we can find it
        Assert.True(true, $"Output written to {outPath}");
    }

    /// <summary>
    /// Diagnostic: disassemble bytecode of the mostra function to understand the se/senao bug.
    /// </summary>
    [Fact]
    public void Diagnostic_DisassembleMostra()
    {
        var source = @"
classe teste
const iniclasse = criar(arg0)

func mostra
  telatxt tela
  se 1
    tela.msg(""A\n"")
    tela.msg(""B\n"")
    tela.msg(""C\n"")
  senao
    tela.msg(""NEVER\n"")
  fimse

func ini
  mostra()
";
        var parser = new IntMudSourceParser();
        var normalizedSource = IntMudSourceParser.NormalizeEncoding(source);
        var ast = parser.Parse(normalizedSource, "inline.int");
        var units = BytecodeCompiler.CompileAll(ast);

        var sb = new StringBuilder();
        foreach (var unit in units)
        {
            sb.AppendLine($"=== Class: {unit.ClassName} ===");
            sb.AppendLine($"String pool ({unit.StringPool.Count}):");
            for (int i = 0; i < unit.StringPool.Count; i++)
                sb.AppendLine($"  [{i}] = \"{unit.StringPool[i].Replace("\n", "\\n").Replace("\r", "\\r")}\"");
            sb.AppendLine();

            foreach (var (funcName, func) in unit.Functions)
            {
                sb.AppendLine($"--- Function: {funcName} (bytecode: {func.Bytecode.Length} bytes) ---");
                DisassembleBytecode(sb, func.Bytecode, unit.StringPool);
                sb.AppendLine();
            }
        }

        var result = ConformanceRunner.RunFromSource(source);
        sb.AppendLine($"\n=== OUTPUT ===");
        sb.AppendLine($"Success: {result.ExecuteSucceeded}");
        sb.AppendLine($"Error: {result.Error}");
        sb.AppendLine($"Output: [{result.Output}]");

        var outPath = Path.Combine(Path.GetTempPath(), "intmud_disasm.txt");
        File.WriteAllText(outPath, sb.ToString());
        Assert.True(result.ExecuteSucceeded, $"Failed: {result.Error}");
        Assert.Contains("A\nB\nC\n", result.Output);
    }

    /// <summary>
    /// Diagnostic: trace the actual listaobj2 mostra pattern with epara loop.
    /// </summary>
    [Fact]
    public void Diagnostic_MostraListaobj2()
    {
        var source = @"
classe banana
classe limao
classe teste
const iniclasse = criar(arg0)

func mostra
  telatxt tela
  tela.msg(arg1 + ""\n"")
  se arg0.total == 0
    tela.msg(""  Lista vazia\n"")
  senao
    tela.msg(""  Lista: "" + arg0.objini)
    listaitem i = arg0.ini
    epara i.depois, i, i.depois
      tela.msg("", "" + i.obj)
    efim
    tela.msg(""\n"")
  fimse

func ini
  criar(""banana"")
  criar(""limao"")
  listaobj lista
  lista.addfim($banana, $limao)
  mostra(lista, ""test"")
";
        var result = ConformanceRunner.RunFromSource(source);
        Assert.True(result.ExecuteSucceeded, $"Failed: {result.Error}");
        Assert.Contains(", [limao]", result.Output, StringComparison.Ordinal);
    }

    private static void DisassembleBytecode(StringBuilder sb, byte[] bytecode, List<string> stringPool)
    {
        int ip = 0;
        while (ip < bytecode.Length)
        {
            int offset = ip;
            var op = (BytecodeOp)bytecode[ip++];

            switch (op)
            {
                case BytecodeOp.Nop:
                    sb.AppendLine($"  {offset:D4}: Nop");
                    break;
                case BytecodeOp.Pop:
                    sb.AppendLine($"  {offset:D4}: Pop");
                    break;
                case BytecodeOp.Dup:
                    sb.AppendLine($"  {offset:D4}: Dup");
                    break;
                case BytecodeOp.PushNull:
                    sb.AppendLine($"  {offset:D4}: PushNull");
                    break;
                case BytecodeOp.PushInt:
                    var intVal = BitConverter.ToInt32(bytecode, ip); ip += 4;
                    sb.AppendLine($"  {offset:D4}: PushInt {intVal}");
                    break;
                case BytecodeOp.PushDouble:
                    var dblVal = BitConverter.ToDouble(bytecode, ip); ip += 8;
                    sb.AppendLine($"  {offset:D4}: PushDouble {dblVal}");
                    break;
                case BytecodeOp.PushString:
                    var strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    var str = strIdx < stringPool.Count ? stringPool[strIdx].Replace("\n", "\\n") : "???";
                    sb.AppendLine($"  {offset:D4}: PushString [{strIdx}] \"{str}\"");
                    break;
                case BytecodeOp.PushTrue:
                    sb.AppendLine($"  {offset:D4}: PushTrue");
                    break;
                case BytecodeOp.PushFalse:
                    sb.AppendLine($"  {offset:D4}: PushFalse");
                    break;
                case BytecodeOp.LoadLocal:
                    var localIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    sb.AppendLine($"  {offset:D4}: LoadLocal {localIdx}");
                    break;
                case BytecodeOp.StoreLocal:
                    localIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    sb.AppendLine($"  {offset:D4}: StoreLocal {localIdx}");
                    break;
                case BytecodeOp.LoadField:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: LoadField [{strIdx}] \"{str}\"");
                    break;
                case BytecodeOp.StoreField:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: StoreField [{strIdx}] \"{str}\"");
                    break;
                case BytecodeOp.LoadArg:
                    var argIdx = bytecode[ip++];
                    sb.AppendLine($"  {offset:D4}: LoadArg {argIdx}");
                    break;
                case BytecodeOp.LoadArgCount:
                    sb.AppendLine($"  {offset:D4}: LoadArgCount");
                    break;
                case BytecodeOp.LoadThis:
                    sb.AppendLine($"  {offset:D4}: LoadThis");
                    break;
                case BytecodeOp.Jump:
                    var jumpOff = BitConverter.ToInt16(bytecode, ip); ip += 2;
                    sb.AppendLine($"  {offset:D4}: Jump {jumpOff:+#;-#;0} (-> {ip + jumpOff:D4})");
                    break;
                case BytecodeOp.JumpIfTrue:
                    jumpOff = BitConverter.ToInt16(bytecode, ip); ip += 2;
                    sb.AppendLine($"  {offset:D4}: JumpIfTrue {jumpOff:+#;-#;0} (-> {ip + jumpOff:D4})");
                    break;
                case BytecodeOp.JumpIfFalse:
                    jumpOff = BitConverter.ToInt16(bytecode, ip); ip += 2;
                    sb.AppendLine($"  {offset:D4}: JumpIfFalse {jumpOff:+#;-#;0} (-> {ip + jumpOff:D4})");
                    break;
                case BytecodeOp.Call:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    var callArgCount = bytecode[ip++];
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: Call [{strIdx}] \"{str}\" args={callArgCount}");
                    break;
                case BytecodeOp.CallMethod:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    callArgCount = bytecode[ip++];
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: CallMethod [{strIdx}] \"{str}\" args={callArgCount}");
                    break;
                case BytecodeOp.Return:
                    sb.AppendLine($"  {offset:D4}: Return");
                    break;
                case BytecodeOp.ReturnValue:
                    sb.AppendLine($"  {offset:D4}: ReturnValue");
                    break;
                case BytecodeOp.InitSpecialType:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: InitSpecialType [{strIdx}] \"{str}\"");
                    break;
                case BytecodeOp.Line:
                    var lineNum = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    sb.AppendLine($"  {offset:D4}: Line {lineNum}");
                    break;
                case BytecodeOp.Add:
                    sb.AppendLine($"  {offset:D4}: Add");
                    break;
                case BytecodeOp.Concat:
                    sb.AppendLine($"  {offset:D4}: Concat");
                    break;
                case BytecodeOp.Terminate:
                    sb.AppendLine($"  {offset:D4}: Terminate");
                    break;
                case BytecodeOp.LoadGlobal:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: LoadGlobal [{strIdx}] \"{str}\"");
                    break;
                case BytecodeOp.StoreGlobal:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: StoreGlobal [{strIdx}] \"{str}\"");
                    break;
                case BytecodeOp.LoadClass:
                    strIdx = BitConverter.ToUInt16(bytecode, ip); ip += 2;
                    str = strIdx < stringPool.Count ? stringPool[strIdx] : "???";
                    sb.AppendLine($"  {offset:D4}: LoadClass [{strIdx}] \"{str}\"");
                    break;
                case BytecodeOp.LoadClassDynamic:
                    sb.AppendLine($"  {offset:D4}: LoadClassDynamic");
                    break;
                default:
                    sb.AppendLine($"  {offset:D4}: ??? op={op} ({(byte)op})");
                    break;
            }
        }
    }
}
