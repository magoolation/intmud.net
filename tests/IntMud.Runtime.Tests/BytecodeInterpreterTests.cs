using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;
using Xunit;

using BytecodeCompiledFunction = IntMud.Compiler.Bytecode.CompiledFunction;

namespace IntMud.Runtime.Tests;

public class BytecodeInterpreterTests
{
    [Fact]
    public void Execute_SimpleReturn_ReturnsNull()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitReturn();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.True(result.IsNull);
    }

    [Fact]
    public void Execute_ReturnInt_ReturnsValue()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushInt(42);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void Execute_Arithmetic_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // 10 + 5 = 15
            emitter.EmitPushInt(10);
            emitter.EmitPushInt(5);
            emitter.EmitAdd();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(15, result.AsInt());
    }

    [Fact]
    public void Execute_Subtraction_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushInt(10);
            emitter.EmitPushInt(3);
            emitter.EmitSub();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(7, result.AsInt());
    }

    [Fact]
    public void Execute_Multiplication_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushInt(6);
            emitter.EmitPushInt(7);
            emitter.EmitMul();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void Execute_StringConcatenation_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("Hello, ");
            emitter.EmitPushString("World!");
            emitter.EmitAdd();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("Hello, World!", result.AsString());
    }

    [Fact]
    public void Execute_Comparison_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushInt(10);
            emitter.EmitPushInt(5);
            emitter.EmitGt();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.True(result.IsTruthy);
    }

    [Fact]
    public void Execute_ConditionalJump_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // if (true) return 1; else return 2;
            emitter.EmitPushTrue();
            var jumpToElse = emitter.EmitJumpIfFalse();
            emitter.EmitPushInt(1);
            emitter.EmitReturnValue();
            emitter.PatchJump(jumpToElse);
            emitter.EmitPushInt(2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(1, result.AsInt());
    }

    [Fact]
    public void Execute_LocalVariables_Work()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // local = 42; return local;
            emitter.EmitPushInt(42);
            emitter.EmitStoreLocal(0);
            emitter.EmitLoadLocal(0);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void Execute_GlobalVariables_Work()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // global = 100; return global;
            emitter.EmitPushInt(100);
            emitter.EmitStoreGlobal("testVar");
            emitter.EmitLoadGlobal("testVar");
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(100, result.AsInt());
        Assert.Equal(100, interpreter.Globals["testVar"].AsInt());
    }

    [Fact]
    public void Execute_ArgumentAccess_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // return arg0 + arg1;
            emitter.EmitLoadArg(0);
            emitter.EmitLoadArg(1);
            emitter.EmitAdd();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.ExecuteFunction(func, new[]
        {
            RuntimeValue.FromInt(10),
            RuntimeValue.FromInt(20)
        });

        Assert.Equal(30, result.AsInt());
    }

    [Fact]
    public void Execute_ArgumentCount_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitLoadArgCount();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.ExecuteFunction(func, new[]
        {
            RuntimeValue.FromInt(1),
            RuntimeValue.FromInt(2),
            RuntimeValue.FromInt(3)
        });

        Assert.Equal(3, result.AsInt());
    }

    [Fact]
    public void Execute_StackOperations_Work()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // dup: 5 -> 5, 5; add: -> 10
            emitter.EmitPushInt(5);
            emitter.EmitDup();
            emitter.EmitAdd();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(10, result.AsInt());
    }

    [Fact]
    public void Execute_SwapOperation_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // 10, 3 -> 3, 10 -> 3 - 10 = -7
            emitter.EmitPushInt(10);
            emitter.EmitPushInt(3);
            emitter.EmitSwap();
            emitter.EmitSub();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(-7, result.AsInt());
    }

    [Fact]
    public void Execute_BitwiseOperations_Work()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // 0xFF & 0x0F = 0x0F = 15
            emitter.EmitPushInt(0xFF);
            emitter.EmitPushInt(0x0F);
            emitter.EmitBitAnd();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(15, result.AsInt());
    }

    [Fact]
    public void Execute_Negation_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushInt(42);
            emitter.EmitNeg();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(-42, result.AsInt());
    }

    [Fact]
    public void Execute_FunctionNotFound_Throws()
    {
        var unit = CreateTestUnit();

        var interpreter = new BytecodeInterpreter(unit);

        Assert.Throws<RuntimeException>(() => interpreter.Execute("nonexistent"));
    }

    [Fact]
    public void Execute_Terminate_ThrowsTerminateException()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitTerminate();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);

        Assert.Throws<TerminateException>(() => interpreter.Execute("test"));
    }

    private static CompiledUnit CreateTestUnit()
    {
        return new CompiledUnit { ClassName = "TestClass" };
    }

    private static BytecodeCompiledFunction CreateFunction(string name, Action<BytecodeEmitter> emit, List<string> stringPool)
    {
        var emitter = new BytecodeEmitter(stringPool);
        emit(emitter);

        return new BytecodeCompiledFunction
        {
            Name = name,
            Bytecode = emitter.GetBytecode()
        };
    }
}
