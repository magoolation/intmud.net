using IntMud.BuiltinFunctions;
using IntMud.Core.Instructions;

namespace IntMud.Audit;

/// <summary>
/// Audit tool that compares the C++ IntMUD implementation against the .NET port,
/// reporting gaps in built-in functions, variable types, and expression operators.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("=============================================================");
        Console.WriteLine("  IntMUD C++ vs .NET Audit Report");
        Console.WriteLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("=============================================================");
        Console.WriteLine();

        int totalGaps = 0;

        totalGaps += RunBuiltinFunctionAudit();
        Console.WriteLine();

        totalGaps += RunVariableTypeAudit();
        Console.WriteLine();

        totalGaps += RunExpressionOperatorAudit();
        Console.WriteLine();

        Console.WriteLine("=============================================================");
        Console.WriteLine($"  TOTAL GAPS: {totalGaps}");
        Console.WriteLine("=============================================================");

        return totalGaps > 0 ? 1 : 0;
    }

    // =========================================================================
    // A) Builtin Function Audit
    // =========================================================================

    private static int RunBuiltinFunctionAudit()
    {
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine("  A) BUILTIN FUNCTION AUDIT");
        Console.WriteLine("-------------------------------------------------------------");

        // The 102 C++ ListaFunc[] functions from the original source.
        var cppFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "apagar", "arg0", "arg1", "arg2", "arg3", "arg4",
            "arg5", "arg6", "arg7", "arg8", "arg9", "args",
            "criar", "este", "int", "intabs", "intbit", "intbith",
            "intchr", "intdist", "intdistdif", "intdistmai", "intdiv",
            "intmax", "intmin", "intnome", "intpos", "intsenha",
            "intsub", "intsublin", "inttotal",
            "matacos", "matasin", "matatan", "matbaixo", "matcima",
            "matcos", "matdeg", "matexp", "matlog", "matpi",
            "matpow", "matrad", "matraiz", "matsin", "mattan",
            "objantes", "objdepois", "rand", "ref", "txt", "txt1", "txt2",
            "txtbit", "txtbith", "txtchr", "txtcod", "txtconv",
            "txtcopiamai", "txtcor", "txtdec", "txte", "txtesp",
            "txtfiltro", "txtfim", "txthex", "txtinvis", "txtmai",
            "txtmaiini", "txtmaimin", "txtmd5", "txtmin", "txtmudamai",
            "txtnome", "txtnum", "txtproc", "txtprocdif", "txtproclin",
            "txtproclindif", "txtproclinmai", "txtprocmai", "txtremove",
            "txtrepete", "txtrev", "txts", "txtsepara", "txtsha1",
            "txtsha1bin", "txtsub", "txtsublin", "txttipovar",
            "txttroca", "txttrocadif", "txttrocamai", "txturlcod",
            "txturldec", "txtvis", "vartroca", "vartrocacod"
        };

        // Get all names registered in the .NET BuiltinFunctionRegistry.
        var registry = BuiltinFunctionRegistry.CreateDefault();
        var dotnetFunctions = new HashSet<string>(
            registry.GetFunctionNames(),
            StringComparer.OrdinalIgnoreCase);

        // Compare.
        var missingInDotNet = cppFunctions
            .Where(f => !dotnetFunctions.Contains(f))
            .OrderBy(f => f)
            .ToList();

        var extraInDotNet = dotnetFunctions
            .Where(f => !cppFunctions.Contains(f))
            .OrderBy(f => f)
            .ToList();

        int gaps = missingInDotNet.Count;

        Console.WriteLine($"  C++ functions:    {cppFunctions.Count}");
        Console.WriteLine($"  .NET functions:   {dotnetFunctions.Count}");
        Console.WriteLine();

        if (missingInDotNet.Count > 0)
        {
            Console.WriteLine($"  MISSING in .NET ({missingInDotNet.Count}):");
            foreach (var name in missingInDotNet)
                Console.WriteLine($"    - {name}");
        }
        else
        {
            Console.WriteLine("  All C++ functions present in .NET.");
        }

        Console.WriteLine();

        if (extraInDotNet.Count > 0)
        {
            Console.WriteLine($"  EXTRA in .NET ({extraInDotNet.Count}):");
            foreach (var name in extraInDotNet)
                Console.WriteLine($"    + {name}");
        }
        else
        {
            Console.WriteLine("  No extra .NET functions beyond C++ set.");
        }

        Console.WriteLine();
        Console.WriteLine($"  Builtin function gaps: {gaps}");

        return gaps;
    }

    // =========================================================================
    // B) Variable Type Audit
    // =========================================================================

    private static int RunVariableTypeAudit()
    {
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine("  B) VARIABLE TYPE AUDIT");
        Console.WriteLine("-------------------------------------------------------------");

        // Expected C++ types and their key methods from the analysis.
        // Type name -> list of important methods/properties.
        var cppTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["inttempo"] = ["valor", "ativo", "parar", "reiniciar"],
            ["intexec"] = ["valor", "exec"],
            ["intinc"] = ["valor", "min", "max", "passo"],
            ["telatxt"] = ["imprimir", "limpar", "cor", "posicao", "tecla"],
            ["serv"] = ["porta", "conectar", "desconectar", "ativo", "socket"],
            ["socket"] = ["enviar", "receber", "fechar", "ativo", "ip", "porta"],
            ["arqtxt"] = ["abrir", "fechar", "ler", "escrever", "nome"],
            ["arqsav"] = ["abrir", "fechar", "ler", "escrever", "apagar", "nome"],
            ["arqdir"] = ["pasta", "listar", "existe"],
            ["arqlog"] = ["abrir", "fechar", "escrever", "nome"],
            ["arqmem"] = ["ler", "escrever", "tamanho"],
            ["arqprog"] = ["nome", "compilar", "executar"],
            ["arqexec"] = ["executar", "args", "ativo", "pid"],
            ["listaobj"] = ["adicionar", "remover", "limpar", "total", "obter", "indice"],
            ["listaitem"] = ["adicionar", "remover", "limpar", "total", "obter", "indice", "ordenar"],
            ["indiceobj"] = ["adicionar", "remover", "limpar", "total", "obter", "existe"],
            ["indiceitem"] = ["adicionar", "remover", "limpar", "total", "obter", "existe"],
            ["textotxt"] = ["texto", "total", "limpar", "adicionar"],
            ["textopos"] = ["texto", "posicao", "total"],
            ["textovar"] = ["nome", "valor", "tipo", "total"],
            ["textoobj"] = ["nome", "classe", "total"],
            ["nomeobj"] = ["nome", "classe", "total", "obter"],
            ["datahora"] = ["ano", "mes", "dia", "hora", "minuto", "segundo", "agora"],
            ["debug"] = ["ativo", "erro", "variavel", "pilha"],
            ["prog"] = ["nome", "compilar", "executar", "ativo"],
        };

        // Discover which types have Instance classes in IntMud.Runtime.Types
        // by checking the assembly for classes ending in "Instance".
        var runtimeAssembly = typeof(IntMud.Runtime.Types.IntTempoInstance).Assembly;
        var instanceTypes = runtimeAssembly
            .GetTypes()
            .Where(t => t.IsClass
                && !t.IsAbstract
                && t.Name.EndsWith("Instance")
                && t.Namespace == "IntMud.Runtime.Types")
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build a map from C++ type name to expected .NET instance class name.
        // Convention: PascalCase type name + "Instance"
        // e.g. "inttempo" -> "IntTempoInstance", "arqtxt" -> "ArqTxtInstance"
        var typeNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["inttempo"] = "IntTempoInstance",
            ["intexec"] = "IntExecInstance",
            ["intinc"] = "IntIncInstance",
            ["telatxt"] = "TelaTxtInstance",
            ["serv"] = "ServInstance",
            ["socket"] = "SocketInstance",
            ["arqtxt"] = "ArqTxtInstance",
            ["arqsav"] = "ArqSavInstance",
            ["arqdir"] = "ArqDirInstance",
            ["arqlog"] = "ArqLogInstance",
            ["arqmem"] = "ArqMemInstance",
            ["arqprog"] = "ArqProgInstance",
            ["arqexec"] = "ArqExecInstance",
            ["listaobj"] = "ListaObjInstance",
            ["listaitem"] = "ListaItemInstance",
            ["indiceobj"] = "IndiceObjInstance",
            ["indiceitem"] = "IndiceItemInstance",
            ["textotxt"] = "TextoTxtInstance",
            ["textopos"] = "TextoPosInstance",
            ["textovar"] = "TextoVarInstance",
            ["textoobj"] = "TextoObjInstance",
            ["nomeobj"] = "NomeObjInstance",
            ["datahora"] = "DataHoraInstance",
            ["debug"] = "DebugInstance",
            ["prog"] = "ProgInstance",
        };

        int totalGaps = 0;

        var missingTypes = new List<string>();
        var presentTypes = new List<string>();
        var missingMethods = new Dictionary<string, List<string>>();

        foreach (var (cppType, expectedMethods) in cppTypes.OrderBy(kv => kv.Key))
        {
            if (!typeNameMap.TryGetValue(cppType, out var expectedClassName))
            {
                missingTypes.Add(cppType);
                totalGaps++;
                continue;
            }

            if (!instanceTypes.Contains(expectedClassName))
            {
                missingTypes.Add($"{cppType} (expected {expectedClassName})");
                totalGaps++;
                continue;
            }

            presentTypes.Add(cppType);

            // Check for methods/properties on the instance class.
            var instanceType = runtimeAssembly.GetTypes()
                .First(t => t.Name.Equals(expectedClassName, StringComparison.OrdinalIgnoreCase));

            var members = instanceType
                .GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(m => m.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = expectedMethods
                .Where(m => !members.Contains(m))
                .ToList();

            if (missing.Count > 0)
            {
                missingMethods[cppType] = missing;
                totalGaps += missing.Count;
            }
        }

        Console.WriteLine($"  C++ types:     {cppTypes.Count}");
        Console.WriteLine($"  .NET types:    {presentTypes.Count}");
        Console.WriteLine();

        if (missingTypes.Count > 0)
        {
            Console.WriteLine($"  MISSING types ({missingTypes.Count}):");
            foreach (var t in missingTypes)
                Console.WriteLine($"    - {t}");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("  All C++ types have Instance classes in .NET.");
            Console.WriteLine();
        }

        if (missingMethods.Count > 0)
        {
            Console.WriteLine($"  MISSING methods per type ({missingMethods.Values.Sum(v => v.Count)} total):");
            foreach (var (typeName, methods) in missingMethods.OrderBy(kv => kv.Key))
            {
                Console.WriteLine($"    {typeName}:");
                foreach (var method in methods)
                    Console.WriteLine($"      - {method}");
            }
        }
        else
        {
            Console.WriteLine("  All expected methods present on .NET types.");
        }

        Console.WriteLine();
        Console.WriteLine($"  Variable type gaps: {totalGaps}");

        return totalGaps;
    }

    // =========================================================================
    // C) Expression Operator Audit
    // =========================================================================

    private static int RunExpressionOperatorAudit()
    {
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine("  C) EXPRESSION OPERATOR AUDIT");
        Console.WriteLine("-------------------------------------------------------------");

        // The 57 C++ expression operators from Instr::Expressao enum.
        // Name -> numeric value in C++.
        var cppOperators = new Dictionary<string, int>
        {
            // Control
            ["Fim"] = 0,
            ["Coment"] = 1,

            // Escape sequences
            ["BarraN"] = 2,
            ["BarraB"] = 3,
            ["BarraC"] = 4,
            ["BarraD"] = 5,

            // Variable access
            ["VarIni"] = 6,
            ["VarFim"] = 7,
            ["DoisPontos"] = 8,
            ["Ponto"] = 9,
            ["Arg"] = 10,
            ["Colchetes"] = 11,

            // Arithmetic
            ["Add"] = 20,
            ["Sub"] = 21,
            ["Mul"] = 22,
            ["Div"] = 23,
            ["Mod"] = 24,

            // Bitwise
            ["And"] = 25,
            ["Or"] = 26,
            ["Xor"] = 27,
            ["Shl"] = 28,
            ["Shr"] = 29,

            // Comparison
            ["Igual"] = 30,
            ["Diferente"] = 31,
            ["Menor"] = 32,
            ["Maior"] = 33,
            ["MenorIgual"] = 34,
            ["MaiorIgual"] = 35,
            ["IgualTipo"] = 36,
            ["DiferenteTipo"] = 37,

            // Logical
            ["LogicoE"] = 38,
            ["LogicoOu"] = 39,
            ["LogicoNao"] = 40,

            // Unary
            ["Neg"] = 41,
            ["Complemento"] = 42,
            ["PreInc"] = 43,
            ["PreDec"] = 44,
            ["PosInc"] = 45,
            ["PosDec"] = 46,

            // Assignment
            ["Atrib"] = 50,
            ["AtribAdd"] = 51,
            ["AtribSub"] = 52,
            ["AtribMul"] = 53,
            ["AtribDiv"] = 54,
            ["AtribMod"] = 55,
            ["AtribAnd"] = 56,
            ["AtribOr"] = 57,
            ["AtribXor"] = 58,
            ["AtribShl"] = 59,
            ["AtribShr"] = 60,

            // Ternary
            ["Ternario"] = 61,
            ["TernarioDois"] = 62,
            ["NullCoalesce"] = 63,

            // Special
            ["Virgula"] = 64,

            // Literals
            ["NumInt"] = 70,
            ["NumReal"] = 71,
            ["Texto"] = 72,
            ["Nulo"] = 73,

            // References
            ["Classe"] = 80,
            ["ClasseDinamica"] = 81,
            ["Este"] = 82,
        };

        // Get all values from the .NET ExpressionOp enum.
        var dotnetOps = Enum.GetNames<ExpressionOp>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dotnetOpValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var op in Enum.GetValues<ExpressionOp>())
        {
            dotnetOpValues[op.ToString()] = (int)op;
        }

        var missingOps = cppOperators.Keys
            .Where(name => !dotnetOps.Contains(name))
            .OrderBy(name => cppOperators[name])
            .ToList();

        var extraOps = dotnetOps
            .Where(name => !cppOperators.ContainsKey(name))
            .OrderBy(name => name)
            .ToList();

        var valueMismatches = new List<string>();
        foreach (var (name, cppValue) in cppOperators)
        {
            if (dotnetOpValues.TryGetValue(name, out var dotnetValue) && cppValue != dotnetValue)
            {
                valueMismatches.Add($"{name}: C++={cppValue}, .NET={dotnetValue}");
            }
        }

        int gaps = missingOps.Count;

        Console.WriteLine($"  C++ operators:    {cppOperators.Count}");
        Console.WriteLine($"  .NET operators:   {dotnetOps.Count}");
        Console.WriteLine();

        if (missingOps.Count > 0)
        {
            Console.WriteLine($"  MISSING in .NET ({missingOps.Count}):");
            foreach (var name in missingOps)
                Console.WriteLine($"    - {name} (={cppOperators[name]})");
        }
        else
        {
            Console.WriteLine("  All C++ operators present in .NET.");
        }

        Console.WriteLine();

        if (extraOps.Count > 0)
        {
            Console.WriteLine($"  EXTRA in .NET ({extraOps.Count}):");
            foreach (var name in extraOps)
                Console.WriteLine($"    + {name}");
        }
        else
        {
            Console.WriteLine("  No extra .NET operators beyond C++ set.");
        }

        Console.WriteLine();

        if (valueMismatches.Count > 0)
        {
            Console.WriteLine($"  VALUE MISMATCHES ({valueMismatches.Count}):");
            foreach (var mismatch in valueMismatches)
                Console.WriteLine($"    ! {mismatch}");
        }
        else
        {
            Console.WriteLine("  All operator values match between C++ and .NET.");
        }

        Console.WriteLine();
        Console.WriteLine($"  Expression operator gaps: {gaps}");

        return gaps;
    }
}
