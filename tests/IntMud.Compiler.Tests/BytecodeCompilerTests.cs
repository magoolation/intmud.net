using IntMud.Compiler.Ast;
using IntMud.Compiler.Bytecode;
using Xunit;

namespace IntMud.Compiler.Tests;

public class BytecodeEmitterTests
{
    [Fact]
    public void EmitPushInt_EncodesCorrectly()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitPushInt(42);

        var bytecode = emitter.GetBytecode();
        Assert.Equal(5, bytecode.Length); // 1 opcode + 4 bytes int
        Assert.Equal((byte)BytecodeOp.PushInt, bytecode[0]);
        Assert.Equal(42, BitConverter.ToInt32(bytecode, 1));
    }

    [Fact]
    public void EmitPushDouble_EncodesCorrectly()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitPushDouble(3.14159);

        var bytecode = emitter.GetBytecode();
        Assert.Equal(9, bytecode.Length); // 1 opcode + 8 bytes double
        Assert.Equal((byte)BytecodeOp.PushDouble, bytecode[0]);
        Assert.Equal(3.14159, BitConverter.ToDouble(bytecode, 1), precision: 5);
    }

    [Fact]
    public void EmitPushString_AddsToStringPool()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitPushString("Hello");

        Assert.Single(stringPool);
        Assert.Equal("Hello", stringPool[0]);

        var bytecode = emitter.GetBytecode();
        Assert.Equal(3, bytecode.Length); // 1 opcode + 2 bytes index
        Assert.Equal((byte)BytecodeOp.PushString, bytecode[0]);
    }

    [Fact]
    public void EmitPushString_ReusesDuplicates()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitPushString("Hello");
        emitter.EmitPushString("Hello");

        Assert.Single(stringPool);
    }

    [Fact]
    public void EmitJump_AndPatch_Works()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        var jumpPos = emitter.EmitJump();
        emitter.EmitNop();
        emitter.EmitNop();
        emitter.PatchJump(jumpPos);

        var bytecode = emitter.GetBytecode();
        // Jump at 0, offset at 1-2, then 2 nops
        Assert.Equal((byte)BytecodeOp.Jump, bytecode[0]);
        var offset = BitConverter.ToInt16(bytecode, 1);
        Assert.Equal(2, offset); // Skip 2 nops
    }

    [Fact]
    public void EmitCall_EncodesCorrectly()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitCall("testFunc", 3);

        var bytecode = emitter.GetBytecode();
        Assert.Equal(4, bytecode.Length); // 1 opcode + 2 bytes index + 1 byte argcount
        Assert.Equal((byte)BytecodeOp.Call, bytecode[0]);
        Assert.Equal(3, bytecode[3]); // arg count
    }

    [Fact]
    public void EmitLoadClassDynamic_EncodesCorrectly()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitLoadClassDynamic();

        var bytecode = emitter.GetBytecode();
        Assert.Single(bytecode);
        Assert.Equal((byte)BytecodeOp.LoadClassDynamic, bytecode[0]);
    }

    [Fact]
    public void EmitLoadClassMemberDynamic_EncodesCorrectly()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitLoadClassMemberDynamic();

        var bytecode = emitter.GetBytecode();
        Assert.Single(bytecode);
        Assert.Equal((byte)BytecodeOp.LoadClassMemberDynamic, bytecode[0]);
    }

    [Fact]
    public void EmitStoreClassMember_EncodesCorrectly()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitStoreClassMember("TestClass", "testMember");

        var bytecode = emitter.GetBytecode();
        Assert.Equal(5, bytecode.Length); // 1 opcode + 2 bytes class + 2 bytes member
        Assert.Equal((byte)BytecodeOp.StoreClassMember, bytecode[0]);
        Assert.Equal(2, stringPool.Count);
        Assert.Equal("TestClass", stringPool[0]);
        Assert.Equal("testMember", stringPool[1]);
    }

    [Fact]
    public void EmitStoreClassMemberDynamic_EncodesCorrectly()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        emitter.EmitStoreClassMemberDynamic();

        var bytecode = emitter.GetBytecode();
        Assert.Single(bytecode);
        Assert.Equal((byte)BytecodeOp.StoreClassMemberDynamic, bytecode[0]);
    }

    [Fact]
    public void LoopContext_BreakAndContinue()
    {
        var stringPool = new List<string>();
        var emitter = new BytecodeEmitter(stringPool);

        var loopStart = emitter.DefineLabel();
        emitter.PushLoopContext();

        emitter.EmitNop(); // Body start
        emitter.EmitBreak();
        emitter.EmitContinue();

        emitter.PopLoopContext(loopStart);

        // Should have generated jumps
        var bytecode = emitter.GetBytecode();
        Assert.True(bytecode.Length > 3);
    }
}

public class BytecodeCompilerTests
{
    [Fact]
    public void Compile_SimpleFunction_GeneratesBytecode()
    {
        var ast = CreateSimpleAst();

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Equal("TestClass", unit.ClassName);
        Assert.Single(unit.Functions);
        Assert.True(unit.Functions.ContainsKey("testFunc"));
    }

    [Fact]
    public void Compile_VariableDeclaration_RegistersVariable()
    {
        var classNode = new ClassDefinitionNode { Name = "TestClass" };
        classNode.Members.Add(new VariableDeclarationNode
        {
            Name = "counter",
            TypeName = "int32"
        });

        var ast = new CompilationUnitNode();
        ast.Classes.Add(classNode);

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Single(unit.Variables);
        Assert.Equal("counter", unit.Variables[0].Name);
        Assert.Equal("int32", unit.Variables[0].TypeName);
    }

    [Fact]
    public void Compile_ConstantDefinition_StoresValue()
    {
        var classNode = new ClassDefinitionNode { Name = "TestClass" };
        classNode.Members.Add(new ConstantDefinitionNode
        {
            Name = "MAX_VALUE",
            Value = new NumericLiteralNode { Value = 100, IsInteger = true }
        });

        var ast = new CompilationUnitNode();
        ast.Classes.Add(classNode);

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Single(unit.Constants);
        Assert.Equal(ConstantType.Int, unit.Constants["MAX_VALUE"].Type);
        Assert.Equal(100, unit.Constants["MAX_VALUE"].IntValue);
    }

    [Fact]
    public void Compile_StringConstant_StoresValue()
    {
        var classNode = new ClassDefinitionNode { Name = "TestClass" };
        classNode.Members.Add(new ConstantDefinitionNode
        {
            Name = "MESSAGE",
            Value = new StringLiteralNode { Value = "Hello World" }
        });

        var ast = new CompilationUnitNode();
        ast.Classes.Add(classNode);

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Equal(ConstantType.String, unit.Constants["MESSAGE"].Type);
        Assert.Equal("Hello World", unit.Constants["MESSAGE"].StringValue);
    }

    [Fact]
    public void Compile_ExpressionConstant_GeneratesBytecode()
    {
        // Test: const EXPR = 10 + 5
        var classNode = new ClassDefinitionNode { Name = "TestClass" };
        classNode.Members.Add(new ConstantDefinitionNode
        {
            Name = "EXPR",
            Value = new BinaryExpressionNode
            {
                Left = new NumericLiteralNode { Value = 10, IsInteger = true },
                Operator = BinaryOperator.Add,
                Right = new NumericLiteralNode { Value = 5, IsInteger = true }
            }
        });

        var ast = new CompilationUnitNode();
        ast.Classes.Add(classNode);

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Equal(ConstantType.Expression, unit.Constants["EXPR"].Type);
        Assert.NotNull(unit.Constants["EXPR"].ExpressionBytecode);
        Assert.True(unit.Constants["EXPR"].ExpressionBytecode!.Length > 0);
    }

    [Fact]
    public void Compile_ExpressionConstant_WithArgReference_GeneratesBytecode()
    {
        // Test: const ARG_EXPR = arg0 + 1
        var classNode = new ClassDefinitionNode { Name = "TestClass" };
        classNode.Members.Add(new ConstantDefinitionNode
        {
            Name = "ARG_EXPR",
            Value = new BinaryExpressionNode
            {
                Left = new ArgReferenceNode { Index = 0 },
                Operator = BinaryOperator.Add,
                Right = new NumericLiteralNode { Value = 1, IsInteger = true }
            }
        });

        var ast = new CompilationUnitNode();
        ast.Classes.Add(classNode);

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Equal(ConstantType.Expression, unit.Constants["ARG_EXPR"].Type);
        Assert.NotNull(unit.Constants["ARG_EXPR"].ExpressionBytecode);
    }

    [Fact]
    public void Compile_Inheritance_RecordsBaseClasses()
    {
        var classNode = new ClassDefinitionNode { Name = "Child" };
        classNode.BaseClasses.Add("Parent1");
        classNode.BaseClasses.Add("Parent2");

        var ast = new CompilationUnitNode();
        ast.Classes.Add(classNode);

        var unit = BytecodeCompiler.Compile(ast);

        Assert.Equal(2, unit.BaseClasses.Count);
        Assert.Contains("Parent1", unit.BaseClasses);
        Assert.Contains("Parent2", unit.BaseClasses);
    }

    [Fact]
    public void Compile_NoClass_ThrowsException()
    {
        var ast = new CompilationUnitNode();

        Assert.Throws<CompilerException>(() => BytecodeCompiler.Compile(ast));
    }

    private static CompilationUnitNode CreateSimpleAst()
    {
        var funcNode = new FunctionDefinitionNode { Name = "testFunc" };
        funcNode.Body.Add(new ReturnStatementNode());

        var classNode = new ClassDefinitionNode { Name = "TestClass" };
        classNode.Members.Add(funcNode);

        var ast = new CompilationUnitNode();
        ast.Classes.Add(classNode);

        return ast;
    }
}

public class BytecodeDisassemblerTests
{
    [Fact]
    public void Disassemble_Function_ProducesOutput()
    {
        var stringPool = new List<string> { "testFunc" };
        var function = new CompiledFunction
        {
            Name = "testFunc",
            Bytecode = new byte[] { (byte)BytecodeOp.PushInt, 42, 0, 0, 0, (byte)BytecodeOp.Return }
        };

        var output = BytecodeDisassembler.Disassemble(function, stringPool);

        Assert.Contains("testFunc", output);
        Assert.Contains("PushInt", output);
        Assert.Contains("42", output);
        Assert.Contains("Return", output);
    }

    [Fact]
    public void Disassemble_Unit_IncludesAllSections()
    {
        var unit = new CompiledUnit { ClassName = "TestClass" };
        unit.Variables.Add(new CompiledVariable { Name = "counter", TypeName = "int32" });
        unit.Constants["PI"] = new CompiledConstant { Name = "PI", Type = ConstantType.Double, DoubleValue = 3.14 };
        unit.Functions["init"] = new CompiledFunction
        {
            Name = "init",
            Bytecode = new byte[] { (byte)BytecodeOp.Return }
        };

        var output = BytecodeDisassembler.Disassemble(unit);

        Assert.Contains("TestClass", output);
        Assert.Contains("Variables:", output);
        Assert.Contains("counter", output);
        Assert.Contains("Constants:", output);
        Assert.Contains("PI", output);
        Assert.Contains("init", output);
    }
}

public class CompilerScopeTests
{
    [Fact]
    public void DefineLocal_AssignsIncrementingIndices()
    {
        var scope = new CompilerScope(null);

        var idx1 = scope.DefineLocal("a", "int32");
        var idx2 = scope.DefineLocal("b", "int32");
        var idx3 = scope.DefineLocal("c", "int32");

        Assert.Equal(0, idx1);
        Assert.Equal(1, idx2);
        Assert.Equal(2, idx3);
    }

    [Fact]
    public void ResolveVariable_FindsLocal()
    {
        var scope = new CompilerScope(null);
        scope.DefineLocal("test", "int32");

        var (kind, index) = scope.ResolveVariable("test");

        Assert.Equal(VariableKind.Local, kind);
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveVariable_FindsGlobal()
    {
        var scope = new CompilerScope(null);
        scope.DefineVariable("globalVar", 0);

        var (kind, _) = scope.ResolveVariable("globalVar");

        Assert.Equal(VariableKind.Global, kind);
    }

    [Fact]
    public void ResolveVariable_ChildScope_FindsParentLocal()
    {
        var parentScope = new CompilerScope(null);
        parentScope.DefineLocal("parentVar", "int32");

        var childScope = new CompilerScope(parentScope);
        childScope.DefineLocal("childVar", "int32");

        var (kind, index) = childScope.ResolveVariable("parentVar");

        Assert.Equal(VariableKind.Local, kind);
        Assert.Equal(0, index);
    }

    [Fact]
    public void DefineLocal_DuplicateName_ReturnsExistingIndex()
    {
        // IntMUD allows variable redeclaration - it just reuses the existing variable
        var scope = new CompilerScope(null);
        var index1 = scope.DefineLocal("test", "int32");
        var index2 = scope.DefineLocal("test", "int32");

        Assert.Equal(index1, index2);
        Assert.Equal(1, scope.LocalCount); // Only one variable should exist
    }

    [Fact]
    public void GetLocalVariables_ReturnsAllLocals()
    {
        var scope = new CompilerScope(null);
        scope.DefineLocal("a", "int32");
        scope.DefineLocal("b", "txt1");
        scope.DefineLocal("c", "real");

        var locals = scope.GetLocalVariables();

        Assert.Equal(3, locals.Count);
        Assert.Equal("a", locals[0].Name);
        Assert.Equal("int32", locals[0].TypeName);
        Assert.Equal("b", locals[1].Name);
        Assert.Equal("txt1", locals[1].TypeName);
    }
}
