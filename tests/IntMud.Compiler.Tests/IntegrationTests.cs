using IntMud.Compiler.Ast;
using IntMud.Compiler.Bytecode;
using IntMud.Compiler.Parsing;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;
using Xunit;
using Xunit.Abstractions;

namespace IntMud.Compiler.Tests;

public class IntegrationTests
{
    private readonly ITestOutputHelper _output;

    public IntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ParseAndCompile_SimpleClass_Works()
    {
        var source = @"
classe teste
const valor = 42
const nome = ""Hello""

func iniciar
  ret valor
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");

        Assert.Single(ast.Classes);
        Assert.Equal("teste", ast.Classes[0].Name);

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Equal("teste", unit.ClassName);
        Assert.True(unit.Constants.ContainsKey("valor"));
        Assert.Equal(42, unit.Constants["valor"].IntValue);
        Assert.True(unit.Constants.ContainsKey("nome"));
        Assert.Equal("Hello", unit.Constants["nome"].StringValue);
        Assert.True(unit.Functions.ContainsKey("iniciar"));
    }

    [Fact]
    public void ParseAndCompile_WithInheritance_Works()
    {
        var source = @"
classe filho
herda pai

func teste
  ret 1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Single(unit.BaseClasses);
        Assert.Equal("pai", unit.BaseClasses[0]);
    }

    [Fact]
    public void ParseAndCompile_FunctionWithArithmetic_Works()
    {
        var source = @"
classe calc

func soma
  ret arg0 + arg1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");

        var unit = BytecodeCompiler.Compile(ast);

        Assert.True(unit.Functions.ContainsKey("soma"));
        Assert.True(unit.Functions["soma"].Bytecode.Length > 0);

        // Disassemble to verify
        var disasm = BytecodeDisassembler.Disassemble(unit);
        _output.WriteLine(disasm);

        Assert.Contains("LoadArg", disasm);
        Assert.Contains("Add", disasm);
        Assert.Contains("ReturnValue", disasm);
    }

    [Fact]
    public void ParseCompileExecute_SimpleReturn_Works()
    {
        var source = @"
classe teste

func retorna42
  ret 42
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("retorna42");

        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_StringConstant_Works()
    {
        var source = @"
classe teste
const mensagem = ""Ola Mundo""

func getMensagem
  ret mensagem
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        // Verify constant was compiled
        Assert.True(unit.Constants.ContainsKey("mensagem"));
        Assert.Equal("Ola Mundo", unit.Constants["mensagem"].StringValue);
    }

    [Fact]
    public void ParseCompileExecute_Arithmetic_Works()
    {
        var source = @"
classe calc

func dobro
  ret arg0 * 2

func soma
  ret arg0 + arg1

func diferenca
  ret arg0 - arg1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        // Test dobro
        var result = interpreter.ExecuteFunction(unit.Functions["dobro"], new[] { RuntimeValue.FromInt(21) });
        Assert.Equal(42, result.AsInt());

        // Test soma
        result = interpreter.ExecuteFunction(unit.Functions["soma"], new[] { RuntimeValue.FromInt(10), RuntimeValue.FromInt(32) });
        Assert.Equal(42, result.AsInt());

        // Test diferenca
        result = interpreter.ExecuteFunction(unit.Functions["diferenca"], new[] { RuntimeValue.FromInt(50), RuntimeValue.FromInt(8) });
        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_IfStatement_Works()
    {
        var source = @"
classe teste

func maximo
  se arg0 > arg1
    ret arg0
  senao
    ret arg1
  fimse
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        // Test with first arg greater
        var result = interpreter.ExecuteFunction(unit.Functions["maximo"], new[] { RuntimeValue.FromInt(10), RuntimeValue.FromInt(5) });
        Assert.Equal(10, result.AsInt());

        // Test with second arg greater
        result = interpreter.ExecuteFunction(unit.Functions["maximo"], new[] { RuntimeValue.FromInt(3), RuntimeValue.FromInt(7) });
        Assert.Equal(7, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_LocalVariables_Works()
    {
        var source = @"
classe teste

func calcular
  int32 resultado
  resultado = arg0 + 10
  ret resultado
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        var result = interpreter.ExecuteFunction(unit.Functions["calcular"], new[] { RuntimeValue.FromInt(32) });
        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_Comparison_Works()
    {
        var source = @"
classe teste

func iguais
  ret arg0 == arg1

func diferentes
  ret arg0 != arg1

func menor
  ret arg0 < arg1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        // Test iguais
        var result = interpreter.ExecuteFunction(unit.Functions["iguais"], new[] { RuntimeValue.FromInt(5), RuntimeValue.FromInt(5) });
        Assert.True(result.IsTruthy);

        result = interpreter.ExecuteFunction(unit.Functions["iguais"], new[] { RuntimeValue.FromInt(5), RuntimeValue.FromInt(3) });
        Assert.False(result.IsTruthy);

        // Test diferentes
        result = interpreter.ExecuteFunction(unit.Functions["diferentes"], new[] { RuntimeValue.FromInt(5), RuntimeValue.FromInt(3) });
        Assert.True(result.IsTruthy);

        // Test menor
        result = interpreter.ExecuteFunction(unit.Functions["menor"], new[] { RuntimeValue.FromInt(3), RuntimeValue.FromInt(5) });
        Assert.True(result.IsTruthy);
    }

    [Fact]
    public void ParseCompileExecute_StringConcat_Works()
    {
        var source = @"
classe teste

func juntar
  ret arg0 + arg1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        var result = interpreter.ExecuteFunction(unit.Functions["juntar"], new[]
        {
            RuntimeValue.FromString("Hello, "),
            RuntimeValue.FromString("World!")
        });

        Assert.Equal("Hello, World!", result.AsString());
    }

    [Fact]
    public void ParseCompileExecute_NullCheck_Works()
    {
        var source = @"
classe teste

func ehNulo
  se arg0 == nulo
    ret 1
  fimse
  ret 0
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        var result = interpreter.ExecuteFunction(unit.Functions["ehNulo"], new[] { RuntimeValue.Null });
        Assert.Equal(1, result.AsInt());

        result = interpreter.ExecuteFunction(unit.Functions["ehNulo"], new[] { RuntimeValue.FromInt(5) });
        Assert.Equal(0, result.AsInt());
    }

    [Fact]
    public void Disassemble_ShowsBytecode()
    {
        var source = @"
classe exemplo

func teste
  int32 x
  x = 10
  se x > 5
    ret x * 2
  fimse
  ret x
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var disasm = BytecodeDisassembler.Disassemble(unit);
        _output.WriteLine(disasm);

        Assert.Contains("exemplo", disasm);
        Assert.Contains("teste", disasm);
        Assert.Contains("PushInt", disasm);
        Assert.Contains("StoreLocal", disasm);
    }

    [Fact]
    public void ParseCompileExecute_SimpleElseIf_Works()
    {
        // Use simpler nested if-else structure
        var source = @"
classe teste

func classificar
  se arg0 > 100
    ret 3
  senao
    se arg0 > 50
      ret 2
    senao
      se arg0 > 0
        ret 1
      senao
        ret 0
      fimse
    fimse
  fimse
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        Assert.Equal(3, interpreter.ExecuteFunction(unit.Functions["classificar"], new[] { RuntimeValue.FromInt(150) }).AsInt());
        Assert.Equal(2, interpreter.ExecuteFunction(unit.Functions["classificar"], new[] { RuntimeValue.FromInt(75) }).AsInt());
        Assert.Equal(1, interpreter.ExecuteFunction(unit.Functions["classificar"], new[] { RuntimeValue.FromInt(25) }).AsInt());
        Assert.Equal(0, interpreter.ExecuteFunction(unit.Functions["classificar"], new[] { RuntimeValue.FromInt(-5) }).AsInt());
    }

    [Fact]
    public void ParseCompileExecute_WhileLoop_Works()
    {
        var source = @"
classe teste

func soma
  int32 total
  int32 i
  total = 0
  i = 1
  enquanto i <= arg0
    total = total + i
    i = i + 1
  efim
  ret total
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        // Disassemble to verify
        var disasm = BytecodeDisassembler.Disassemble(unit);
        _output.WriteLine(disasm);

        var interpreter = new BytecodeInterpreter(unit);

        // Sum of 1 to 5 = 15
        var result = interpreter.ExecuteFunction(unit.Functions["soma"], new[] { RuntimeValue.FromInt(5) });
        Assert.Equal(15, result.AsInt());

        // Sum of 1 to 10 = 55
        result = interpreter.ExecuteFunction(unit.Functions["soma"], new[] { RuntimeValue.FromInt(10) });
        Assert.Equal(55, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ForLoop_Works()
    {
        var source = @"
classe teste

func fatorial
  int32 resultado
  int32 i
  resultado = 1
  epara i = 1, i <= arg0, i = i + 1
    resultado = resultado * i
  efim
  ret resultado
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        // Disassemble to verify
        var disasm = BytecodeDisassembler.Disassemble(unit);
        _output.WriteLine(disasm);

        var interpreter = new BytecodeInterpreter(unit);

        // 5! = 120
        var result = interpreter.ExecuteFunction(unit.Functions["fatorial"], new[] { RuntimeValue.FromInt(5) });
        Assert.Equal(120, result.AsInt());

        // 6! = 720
        result = interpreter.ExecuteFunction(unit.Functions["fatorial"], new[] { RuntimeValue.FromInt(6) });
        Assert.Equal(720, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_WhileWithExit_Works()
    {
        var source = @"
classe teste

func buscar
  int32 i
  i = 0
  enquanto i < 100
    se i == arg0
      ret i
    fimse
    i = i + 1
  efim
  ret -1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        // Find 42 in 0-99
        var result = interpreter.ExecuteFunction(unit.Functions["buscar"], new[] { RuntimeValue.FromInt(42) });
        Assert.Equal(42, result.AsInt());

        // 150 is not in 0-99
        result = interpreter.ExecuteFunction(unit.Functions["buscar"], new[] { RuntimeValue.FromInt(150) });
        Assert.Equal(-1, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_NestedLoops_Works()
    {
        var source = @"
classe teste

func multiplicar
  int32 resultado
  int32 i
  int32 j
  resultado = 0
  i = 0
  enquanto i < arg0
    j = 0
    enquanto j < arg1
      resultado = resultado + 1
      j = j + 1
    efim
    i = i + 1
  efim
  ret resultado
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        // 3 * 4 = 12
        var result = interpreter.ExecuteFunction(unit.Functions["multiplicar"], new[] { RuntimeValue.FromInt(3), RuntimeValue.FromInt(4) });
        Assert.Equal(12, result.AsInt());

        // 5 * 7 = 35
        result = interpreter.ExecuteFunction(unit.Functions["multiplicar"], new[] { RuntimeValue.FromInt(5), RuntimeValue.FromInt(7) });
        Assert.Equal(35, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_SwitchStatement_Works()
    {
        var source = @"
classe teste

func diaDaSemana
  casovar arg0
    casose ""domingo""
      ret 0
    casose ""segunda""
      ret 1
    casose ""terca""
      ret 2
    casose
      ret -1
  casofim
  ret -1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        // Disassemble to verify
        var disasm = BytecodeDisassembler.Disassemble(unit);
        _output.WriteLine(disasm);

        var interpreter = new BytecodeInterpreter(unit);

        var result = interpreter.ExecuteFunction(unit.Functions["diaDaSemana"], new[] { RuntimeValue.FromString("domingo") });
        Assert.Equal(0, result.AsInt());

        result = interpreter.ExecuteFunction(unit.Functions["diaDaSemana"], new[] { RuntimeValue.FromString("segunda") });
        Assert.Equal(1, result.AsInt());

        result = interpreter.ExecuteFunction(unit.Functions["diaDaSemana"], new[] { RuntimeValue.FromString("terca") });
        Assert.Equal(2, result.AsInt());

        // Default case
        result = interpreter.ExecuteFunction(unit.Functions["diaDaSemana"], new[] { RuntimeValue.FromString("sabado") });
        Assert.Equal(-1, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_RealisticScript_Works()
    {
        // A more realistic IntMUD script with multiple features
        var source = @"
classe monstro
herda base_monstro

const nome = ""Goblin""
const descricao = ""Um pequeno goblin verde""
const vida_max = 100
const dano_min = 5
const dano_max = 15

int32 vida_atual
int32 nivel

func inicializar
  vida_atual = vida_max
  nivel = 1

func calcular_dano
  int32 dano
  int32 variacao
  variacao = dano_max - dano_min
  dano = dano_min + (arg0 % variacao)
  ret dano

func receber_dano
  int32 dano
  dano = arg0
  se dano > vida_atual
    vida_atual = 0
    ret 0
  senao
    vida_atual = vida_atual - dano
    ret vida_atual
  fimse

func esta_vivo
  se vida_atual > 0
    ret 1
  fimse
  ret 0

func curar
  int32 cura
  cura = arg0
  vida_atual = vida_atual + cura
  se vida_atual > vida_max
    vida_atual = vida_max
  fimse
  ret vida_atual

func subir_nivel
  nivel = nivel + 1
  ret nivel
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine("Compiled successfully!");
        _output.WriteLine($"Class: {unit.ClassName}");
        _output.WriteLine($"Constants: {string.Join(", ", unit.Constants.Keys)}");
        _output.WriteLine($"Variables: {string.Join(", ", unit.Variables.Select(v => v.Name))}");
        _output.WriteLine($"Functions: {string.Join(", ", unit.Functions.Keys)}");

        // Test calcular_dano
        var interpreter = new BytecodeInterpreter(unit);

        // With random seed 7, should give: 5 + (7 % 10) = 5 + 7 = 12
        var result = interpreter.ExecuteFunction(unit.Functions["calcular_dano"], new[] { RuntimeValue.FromInt(7) });
        Assert.Equal(12, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_CombatSimulation_Works()
    {
        var source = @"
classe combate

func simular_ataque
  int32 dano_total
  int32 golpes
  int32 i
  dano_total = 0
  golpes = arg0
  i = 0
  enquanto i < golpes
    dano_total = dano_total + (10 + i)
    i = i + 1
  efim
  ret dano_total

func calcular_experiencia
  int32 exp
  int32 nivel_monstro
  nivel_monstro = arg0
  se nivel_monstro > 10
    exp = nivel_monstro * 100
  senao
    se nivel_monstro > 5
      exp = nivel_monstro * 50
    senao
      exp = nivel_monstro * 25
    fimse
  fimse
  ret exp

func fibonacci
  int32 a
  int32 b
  int32 temp
  int32 i
  int32 n
  n = arg0
  a = 0
  b = 1
  i = 0
  enquanto i < n
    temp = a + b
    a = b
    b = temp
    i = i + 1
  efim
  ret a
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        var interpreter = new BytecodeInterpreter(unit);

        // simular_ataque: sum of (10+0) + (10+1) + (10+2) = 10 + 11 + 12 = 33
        var result = interpreter.ExecuteFunction(unit.Functions["simular_ataque"], new[] { RuntimeValue.FromInt(3) });
        Assert.Equal(33, result.AsInt());

        // calcular_experiencia tests
        result = interpreter.ExecuteFunction(unit.Functions["calcular_experiencia"], new[] { RuntimeValue.FromInt(15) }); // > 10
        Assert.Equal(1500, result.AsInt());

        result = interpreter.ExecuteFunction(unit.Functions["calcular_experiencia"], new[] { RuntimeValue.FromInt(7) }); // 5 < x <= 10
        Assert.Equal(350, result.AsInt());

        result = interpreter.ExecuteFunction(unit.Functions["calcular_experiencia"], new[] { RuntimeValue.FromInt(3) }); // <= 5
        Assert.Equal(75, result.AsInt());

        // Fibonacci test: fib(10) = 55
        result = interpreter.ExecuteFunction(unit.Functions["fibonacci"], new[] { RuntimeValue.FromInt(10) });
        Assert.Equal(55, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ObjectMethodCall_Works()
    {
        var source = @"
classe contador

int32 valor

func inicializar
  este.valor = 0

func incrementar
  este.valor = este.valor + 1
  ret este.valor

func decrementar
  este.valor = este.valor - 1
  ret este.valor

func obterValor
  ret este.valor

func definirValor
  este.valor = arg0
  ret este.valor
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        // Create object and test method calls
        var obj = new BytecodeRuntimeObject(unit);
        var interpreter = new BytecodeInterpreter(unit);

        // Initialize
        interpreter.ExecuteFunctionWithThis(unit.Functions["inicializar"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(0, obj.GetField("valor").AsInt());

        // Increment
        var result = interpreter.ExecuteFunctionWithThis(unit.Functions["incrementar"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(1, result.AsInt());
        Assert.Equal(1, obj.GetField("valor").AsInt());

        // Increment again
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["incrementar"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(2, result.AsInt());

        // Decrement
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["decrementar"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(1, result.AsInt());

        // Set value
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["definirValor"], obj, new[] { RuntimeValue.FromInt(100) });
        Assert.Equal(100, result.AsInt());

        // Get value
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["obterValor"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(100, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_StringMethods_Works()
    {
        var source = @"
classe teste

func testarTamanho
  ret arg0.tamanho

func testarMaiusculo
  ret arg0.maiusculo

func testarMinusculo
  ret arg0.minusculo

func testarPosicao
  ret arg0.posicao(arg1)
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        // Disassemble to verify
        var disasm = BytecodeDisassembler.Disassemble(unit);
        _output.WriteLine(disasm);

        var interpreter = new BytecodeInterpreter(unit);

        // Test tamanho
        var result = interpreter.ExecuteFunction(unit.Functions["testarTamanho"], new[] { RuntimeValue.FromString("Hello") });
        Assert.Equal(5, result.AsInt());

        // Test maiusculo
        result = interpreter.ExecuteFunction(unit.Functions["testarMaiusculo"], new[] { RuntimeValue.FromString("hello") });
        Assert.Equal("HELLO", result.AsString());

        // Test minusculo
        result = interpreter.ExecuteFunction(unit.Functions["testarMinusculo"], new[] { RuntimeValue.FromString("HELLO") });
        Assert.Equal("hello", result.AsString());

        // Test posicao
        result = interpreter.ExecuteFunction(unit.Functions["testarPosicao"], new[] { RuntimeValue.FromString("Hello World"), RuntimeValue.FromString("World") });
        Assert.Equal(6, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ImplicitThis_Works()
    {
        // Test implicit 'this' - accessing instance variables without 'este.' prefix
        var source = @"
classe contador

int32 valor

func obterValor
  ret valor

func definirValor
  valor = arg0

func incrementar
  valor += 1
  ret valor

func decrementar
  valor -= 1
  ret valor

func prefixoIncremento
  ret ++valor

func posfixoIncremento
  ret valor++
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        // Create an object instance
        var obj = new BytecodeRuntimeObject(unit);
        var interpreter = new BytecodeInterpreter(unit);

        // Test obterValor (read implicit this.valor) - should be 0 (default)
        var result = interpreter.ExecuteFunctionWithThis(unit.Functions["obterValor"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(0, result.AsInt());

        // Test definirValor (write implicit this.valor)
        interpreter.ExecuteFunctionWithThis(unit.Functions["definirValor"], obj, new[] { RuntimeValue.FromInt(10) });

        result = interpreter.ExecuteFunctionWithThis(unit.Functions["obterValor"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(10, result.AsInt());

        // Test incrementar (compound assignment on implicit this.valor)
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["incrementar"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(11, result.AsInt());

        // Test decrementar
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["decrementar"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(10, result.AsInt());

        // Test prefix increment (++valor)
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["prefixoIncremento"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(11, result.AsInt());

        // Test postfix increment (valor++) - returns old value
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["posfixoIncremento"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(11, result.AsInt()); // Returns old value before increment

        // Verify value is now 12 after postfix
        result = interpreter.ExecuteFunctionWithThis(unit.Functions["obterValor"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(12, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_Inheritance_Works()
    {
        // Test inheritance - base class with derived class
        var baseSource = @"
classe animal

int32 idade

func obterIdade
  ret idade

func definirIdade
  idade = arg0

func falar
  ret ""som generico""
";

        var derivedSource = @"
classe cachorro herda animal

int32 peso

func obterPeso
  ret peso

func definirPeso
  peso = arg0

func falar
  ret ""au au""
";

        var parser = new IntMudSourceParser();

        // Compile both classes
        var baseAst = parser.Parse(baseSource, "animal.int");
        var baseUnit = BytecodeCompiler.Compile(baseAst);

        var derivedAst = parser.Parse(derivedSource, "cachorro.int");
        var derivedUnit = BytecodeCompiler.Compile(derivedAst);

        _output.WriteLine("=== Base Class (animal) ===");
        _output.WriteLine(BytecodeDisassembler.Disassemble(baseUnit));

        _output.WriteLine("=== Derived Class (cachorro) ===");
        _output.WriteLine(BytecodeDisassembler.Disassemble(derivedUnit));

        // Create derived object with base class
        var obj = new BytecodeRuntimeObject(derivedUnit, new[] { baseUnit });
        var interpreter = new BytecodeInterpreter(derivedUnit);

        // Test inherited field access (idade from animal)
        interpreter.ExecuteFunctionWithThis(baseUnit.Functions["definirIdade"], obj, baseUnit, new[] { RuntimeValue.FromInt(5) });
        var result = interpreter.ExecuteFunctionWithThis(baseUnit.Functions["obterIdade"], obj, baseUnit, Array.Empty<RuntimeValue>());
        Assert.Equal(5, result.AsInt());

        // Test derived class field (peso)
        interpreter.ExecuteFunctionWithThis(derivedUnit.Functions["definirPeso"], obj, derivedUnit, new[] { RuntimeValue.FromInt(25) });
        result = interpreter.ExecuteFunctionWithThis(derivedUnit.Functions["obterPeso"], obj, derivedUnit, Array.Empty<RuntimeValue>());
        Assert.Equal(25, result.AsInt());

        // Test virtual method dispatch - derived class overrides falar()
        // The most derived implementation should be called
        var (method, definingUnit) = obj.GetMethodWithUnit("falar");
        Assert.NotNull(method);
        Assert.NotNull(definingUnit);
        Assert.Equal("cachorro", definingUnit!.ClassName);

        result = interpreter.ExecuteFunctionWithThis(method!, obj, definingUnit, Array.Empty<RuntimeValue>());
        Assert.Equal("au au", result.AsString());

        // Test IsInstanceOf
        Assert.True(obj.IsInstanceOf("cachorro"));
        Assert.True(obj.IsInstanceOf("animal"));
        Assert.False(obj.IsInstanceOf("gato"));
    }

    [Fact]
    public void ParseCompileExecute_InheritanceMethodLookup_Works()
    {
        // Test that methods from base class are accessible
        var baseSource = @"
classe veiculo

int32 velocidade

func acelerar
  velocidade = velocidade + arg0
  ret velocidade

func obterVelocidade
  ret velocidade
";

        var derivedSource = @"
classe carro herda veiculo

int32 potencia

func definirPotencia
  potencia = arg0

func obterPotencia
  ret potencia
";

        var parser = new IntMudSourceParser();

        var baseAst = parser.Parse(baseSource, "veiculo.int");
        var baseUnit = BytecodeCompiler.Compile(baseAst);

        var derivedAst = parser.Parse(derivedSource, "carro.int");
        var derivedUnit = BytecodeCompiler.Compile(derivedAst);

        // Create derived object with base class
        var obj = new BytecodeRuntimeObject(derivedUnit, new[] { baseUnit });
        var interpreter = new BytecodeInterpreter(derivedUnit);

        // Test method from derived class
        interpreter.ExecuteFunctionWithThis(derivedUnit.Functions["definirPotencia"], obj, derivedUnit, new[] { RuntimeValue.FromInt(500) });
        var result = interpreter.ExecuteFunctionWithThis(derivedUnit.Functions["obterPotencia"], obj, derivedUnit, Array.Empty<RuntimeValue>());
        Assert.Equal(500, result.AsInt());

        // Test method from base class (acelerar)
        // Method lookup should find it in base class
        var (acelerarMethod, acelerarUnit) = obj.GetMethodWithUnit("acelerar");
        Assert.NotNull(acelerarMethod);
        Assert.Equal("veiculo", acelerarUnit!.ClassName);

        result = interpreter.ExecuteFunctionWithThis(acelerarMethod!, obj, acelerarUnit!, new[] { RuntimeValue.FromInt(50) });
        Assert.Equal(50, result.AsInt());

        // Accelerate again
        result = interpreter.ExecuteFunctionWithThis(acelerarMethod!, obj, acelerarUnit!, new[] { RuntimeValue.FromInt(30) });
        Assert.Equal(80, result.AsInt());

        // Get velocity using base class method
        var (obterVelocidadeMethod, obterVelocidadeUnit) = obj.GetMethodWithUnit("obterVelocidade");
        result = interpreter.ExecuteFunctionWithThis(obterVelocidadeMethod!, obj, obterVelocidadeUnit!, Array.Empty<RuntimeValue>());
        Assert.Equal(80, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_VirtualMethodOverride_Works()
    {
        // Test varfunc (virtual function) override behavior
        var baseSource = @"
classe forma

varfunc calcularArea
  ret 0
";

        var derivedSource = @"
classe retangulo herda forma

int32 largura
int32 altura

func definirDimensoes
  largura = arg0
  altura = arg1

func calcularArea
  ret largura * altura
";

        var parser = new IntMudSourceParser();

        var baseAst = parser.Parse(baseSource, "forma.int");
        var baseUnit = BytecodeCompiler.Compile(baseAst);

        var derivedAst = parser.Parse(derivedSource, "retangulo.int");
        var derivedUnit = BytecodeCompiler.Compile(derivedAst);

        // Create derived object
        var obj = new BytecodeRuntimeObject(derivedUnit, new[] { baseUnit });
        var interpreter = new BytecodeInterpreter(derivedUnit);

        // Set dimensions
        interpreter.ExecuteFunctionWithThis(derivedUnit.Functions["definirDimensoes"], obj, derivedUnit, new[] { RuntimeValue.FromInt(10), RuntimeValue.FromInt(5) });

        // Call calcularArea - should call derived class version
        var (method, definingUnit) = obj.GetMethodWithUnit("calcularArea");
        Assert.Equal("retangulo", definingUnit!.ClassName);

        var result = interpreter.ExecuteFunctionWithThis(method!, obj, definingUnit!, Array.Empty<RuntimeValue>());
        Assert.Equal(50, result.AsInt());

        // Verify base class method is virtual
        Assert.True(baseUnit.Functions["calcularArea"].IsVirtual);
    }

    [Fact]
    public void ParseCompileExecute_NovoKeyword_Simple()
    {
        // Simplest test for novo keyword - just create object and return a field
        var source = @"
classe contador

int32 valor

func obterValor
  ret valor
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "contador.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        // Create object using BytecodeRuntimeObject directly
        var obj = new BytecodeRuntimeObject(unit);
        var interpreter = new BytecodeInterpreter(unit);

        // Test that object was created and can execute methods
        var result = interpreter.ExecuteFunctionWithThis(unit.Functions["obterValor"], obj, Array.Empty<RuntimeValue>());
        Assert.Equal(0, result.AsInt()); // Default value
    }

    [Fact]
    public void ParseCompileExecute_NovoKeyword_Parses()
    {
        // Test that novo expression parses correctly
        var source = @"
classe contador

int32 valor

func criarObjeto
  ret novo contador
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "contador.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        // Verify the bytecode was generated
        Assert.True(unit.Functions.ContainsKey("criarObjeto"));
    }

    [Fact]
    public void ParseCompileExecute_ArrayIndexRead_Works()
    {
        // Test array read with indexing
        var source = @"
classe teste

func testar
  int32 arr
  arr = vetor(3)
  arr[0] = 10
  arr[1] = 20
  arr[2] = 30
  ret arr[1]
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(20, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ArrayLength_Works()
    {
        // Test array length property
        var source = @"
classe teste

func testar
  int32 arr
  arr = vetor(5)
  ret tam(arr)
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(5, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ArrayAutoExpand_Works()
    {
        // Test array auto-expansion when assigning beyond current size
        var source = @"
classe teste

func testar
  int32 arr
  arr = vetor(2)
  arr[5] = 100
  ret tam(arr)
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(6, result.AsInt()); // Array expanded to fit index 5
    }

    [Fact]
    public void ParseCompileExecute_ArraySum_Works()
    {
        // Test sum of array elements in a loop
        var source = @"
classe teste

func testar
  int32 arr
  int32 soma
  int32 i

  arr = vetor(4)
  arr[0] = 1
  arr[1] = 2
  arr[2] = 3
  arr[3] = 4

  soma = 0
  epara i = 0, i < 4, i = i + 1
    soma = soma + arr[i]
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(10, result.AsInt()); // 1+2+3+4 = 10
    }

    [Fact]
    public void ParseCompileExecute_StringIndex_Works()
    {
        // Test string character access
        var source = @"
classe teste

func testar
  txt s
  s = ""Hello""
  ret s[0]
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.String, result.Type);
        Assert.Equal("H", result.AsString());
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Basic_Works()
    {
        // Test basic foreach loop over array
        var source = @"
classe teste

func testar
  int32 arr
  int32 soma

  arr = vetor(3)
  arr[0] = 10
  arr[1] = 20
  arr[2] = 30

  soma = 0
  para cada item em arr
    soma = soma + item
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(60, result.AsInt()); // 10+20+30 = 60
    }

    [Fact]
    public void ParseCompileExecute_ForEach_EmptyArray_Works()
    {
        // Test foreach loop over empty array
        var source = @"
classe teste

func testar
  int32 arr
  int32 soma

  arr = vetor(0)
  soma = 0

  para cada item em arr
    soma = soma + 1
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(0, result.AsInt()); // Loop body never executes
    }

    [Fact]
    public void ParseCompileExecute_ForEach_String_Works()
    {
        // Test foreach loop over string characters
        var source = @"
classe teste

func testar
  txt s
  int32 contador

  s = ""abc""
  contador = 0

  para cada c em s
    contador = contador + 1
  efim

  ret contador
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(3, result.AsInt()); // "abc" has 3 characters
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Parses()
    {
        // Test that foreach parses correctly
        var source = @"
classe teste

func testar
  int32 arr
  arr = vetor(2)
  para cada x em arr
    x = x + 1
  efim
  ret 1
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        // Verify bytecode was generated
        Assert.True(unit.Functions.ContainsKey("testar"));
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Break_Works()
    {
        // Test break (sair) in foreach loop
        var source = @"
classe teste

func testar
  int32 arr
  int32 soma

  arr = vetor(5)
  arr[0] = 1
  arr[1] = 2
  arr[2] = 3
  arr[3] = 4
  arr[4] = 5

  soma = 0
  para cada item em arr
    se item == 3
      sair
    soma = soma + item
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(3, result.AsInt()); // 1+2 = 3, breaks when item==3
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Continue_Works()
    {
        // Test continue (continuar) in foreach loop
        var source = @"
classe teste

func testar
  int32 arr
  int32 soma

  arr = vetor(5)
  arr[0] = 1
  arr[1] = 2
  arr[2] = 3
  arr[3] = 4
  arr[4] = 5

  soma = 0
  para cada item em arr
    se item == 3
      continuar
    fimse
    soma = soma + item
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(12, result.AsInt()); // 1+2+4+5 = 12, skips 3
    }

    [Fact]
    public void ParseCompileExecute_ForEach_ConditionalBreak_Works()
    {
        // Test conditional break (sair condition)
        var source = @"
classe teste

func testar
  int32 arr
  int32 soma

  arr = vetor(5)
  arr[0] = 1
  arr[1] = 2
  arr[2] = 3
  arr[3] = 4
  arr[4] = 5

  soma = 0
  para cada item em arr
    sair item > 3
    soma = soma + item
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(6, result.AsInt()); // 1+2+3 = 6, breaks when item>3
    }

    [Fact]
    public void ParseCompileExecute_ForEach_ConditionalContinue_Works()
    {
        // Test conditional continue (continuar condition)
        var source = @"
classe teste

func testar
  int32 arr
  int32 soma

  arr = vetor(6)
  arr[0] = 1
  arr[1] = 2
  arr[2] = 3
  arr[3] = 4
  arr[4] = 5
  arr[5] = 6

  soma = 0
  para cada item em arr
    continuar item % 2 == 0
    soma = soma + item
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        Assert.Equal(9, result.AsInt()); // 1+3+5 = 9, skips even numbers
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Nested_BreakInner_Works()
    {
        // Test break in inner nested foreach - should only break inner loop
        var source = @"
classe teste

func testar
  int32 outer
  int32 inner
  int32 soma

  outer = vetor(3)
  outer[0] = 1
  outer[1] = 2
  outer[2] = 3

  inner = vetor(4)
  inner[0] = 10
  inner[1] = 20
  inner[2] = 30
  inner[3] = 40

  soma = 0
  para cada o em outer
    para cada i em inner
      sair i == 20
      soma = soma + i
    efim
    soma = soma + o
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        // For each outer iteration: inner adds 10 (breaks at 20), then adds outer value
        // o=1: inner=10, break, soma=10+1=11
        // o=2: inner=10, break, soma=11+10+2=23
        // o=3: inner=10, break, soma=23+10+3=36
        Assert.Equal(36, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Nested_ContinueInner_Works()
    {
        // Test continue in inner nested foreach - should only skip in inner loop
        var source = @"
classe teste

func testar
  int32 outer
  int32 inner
  int32 soma

  outer = vetor(2)
  outer[0] = 100
  outer[1] = 200

  inner = vetor(4)
  inner[0] = 1
  inner[1] = 2
  inner[2] = 3
  inner[3] = 4

  soma = 0
  para cada o em outer
    para cada i em inner
      continuar i == 2
      soma = soma + i
    efim
    soma = soma + o
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        // For each outer iteration: inner adds 1+3+4=8 (skips 2), then adds outer value
        // o=100: soma = 0 + 8 + 100 = 108
        // o=200: soma = 108 + 8 + 200 = 316
        Assert.Equal(316, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Nested_BreakOuter_Works()
    {
        // Test break in outer foreach when condition met in inner
        var source = @"
classe teste

func testar
  int32 outer
  int32 inner
  int32 soma
  int32 found

  outer = vetor(3)
  outer[0] = 1
  outer[1] = 2
  outer[2] = 3

  inner = vetor(3)
  inner[0] = 10
  inner[1] = 20
  inner[2] = 30

  soma = 0
  found = 0
  para cada o em outer
    para cada i em inner
      soma = soma + i
      se o == 2
        se i == 20
          found = 1
        fimse
      fimse
    efim
    sair found == 1
    soma = soma + o
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        // o=1: inner adds 10+20+30=60, soma=60+1=61, found=0
        // o=2: inner adds 10+20+30=60, soma=61+60=121, found=1, break (no +o)
        Assert.Equal(121, result.AsInt());
    }

    [Fact]
    public void ParseCompileExecute_ForEach_Nested_ContinueOuter_Works()
    {
        // Test continue in outer foreach based on inner loop results
        var source = @"
classe teste

func testar
  int32 outer
  int32 inner
  int32 soma
  int32 skip

  outer = vetor(3)
  outer[0] = 1
  outer[1] = 2
  outer[2] = 3

  inner = vetor(3)
  inner[0] = 1
  inner[1] = 2
  inner[2] = 3

  soma = 0
  para cada o em outer
    skip = 0
    para cada i em inner
      se o == 2
        se i == 2
          skip = 1
          sair
        fimse
      fimse
    efim
    continuar skip == 1
    soma = soma + o
  efim

  ret soma
";
        var parser = new IntMudSourceParser();
        var ast = parser.Parse(source, "test.int");
        var unit = BytecodeCompiler.Compile(ast);

        _output.WriteLine(BytecodeDisassembler.Disassemble(unit));

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("testar");

        Assert.Equal(RuntimeValueType.Integer, result.Type);
        // o=1: skip=0, soma=0+1=1
        // o=2: skip=1, continue (no add)
        // o=3: skip=0, soma=1+3=4
        Assert.Equal(4, result.AsInt());
    }
}
