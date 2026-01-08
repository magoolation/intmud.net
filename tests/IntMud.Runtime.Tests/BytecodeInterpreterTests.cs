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

    [Fact]
    public void Execute_StoreClassMember_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // Store 42 to TestClass:testVar, then load it back
            emitter.EmitPushInt(42);
            emitter.EmitStoreClassMember("TestClass", "testVar");
            // Load from globals with qualified name
            emitter.EmitLoadGlobal("TestClass:testVar");
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(42, result.AsInt());
    }

    [Fact]
    public void Execute_StoreClassMemberDynamic_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // Store 100 to dynamically-named class:member
            emitter.EmitPushInt(100);
            emitter.EmitPushString("MyClass");
            emitter.EmitPushString("myVar");
            emitter.EmitStoreClassMemberDynamic();
            // Load it back via globals
            emitter.EmitLoadGlobal("MyClass:myVar");
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(100, result.AsInt());
    }

    [Fact]
    public void Execute_LoadClassDynamic_Works()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // Push class name and load dynamically
            emitter.EmitPushString("TestClass");
            emitter.EmitLoadClassDynamic();
            // For now, LoadClass returns null if class not found
            // Just verify it doesn't crash
            emitter.EmitPop();
            emitter.EmitPushInt(1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(1, result.AsInt());
    }

    [Fact]
    public void Execute_LoadClassMemberDynamic_Works()
    {
        var unit = CreateTestUnit();
        // First, add a constant to the unit
        unit.Constants["testConst"] = new IntMud.Compiler.Bytecode.CompiledConstant
        {
            Name = "testConst",
            Type = IntMud.Compiler.Bytecode.ConstantType.Int,
            IntValue = 99
        };

        var func = CreateFunction("test", emitter =>
        {
            // Load class member dynamically
            emitter.EmitPushString("TestClass");
            emitter.EmitPushString("testConst");
            emitter.EmitLoadClassMemberDynamic();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(99, result.AsInt());
    }

    [Fact]
    public void Execute_StringConcat_ForDynamicNames()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // Test concatenation: "Test" + "Class" = "TestClass"
            emitter.EmitPushString("Test");
            emitter.EmitPushString("Class");
            emitter.EmitConcat();
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("TestClass", result.AsString());
    }

    [Fact]
    public void RuntimeValue_ArrayBasics_Work()
    {
        // Test the RuntimeValue array functionality
        var array = RuntimeValue.CreateArray(3);
        array.SetIndex(0, RuntimeValue.FromInt(10));
        array.SetIndex(1, RuntimeValue.FromInt(20));
        array.SetIndex(2, RuntimeValue.FromInt(30));

        // Verify array basics work
        Assert.Equal(3, array.Length);
        Assert.Equal(10, array.GetIndex(0).AsInt());
        Assert.Equal(20, array.GetIndex(1).AsInt());
        Assert.Equal(30, array.GetIndex(2).AsInt());
    }

    [Fact]
    public void RuntimeValue_EmptyArrayAccess_ReturnsNull()
    {
        // Test that accessing an empty array returns null (safe access)
        var array = RuntimeValue.CreateArray(0);

        Assert.Equal(0, array.Length);
        Assert.True(array.GetIndex(0).IsNull);
    }

    [Fact]
    public void RuntimeValue_ArrayOutOfBoundsAccess_ReturnsNull()
    {
        // Test that accessing out of bounds returns null (safe access)
        var array = RuntimeValue.CreateArray(2);
        array.SetIndex(0, RuntimeValue.FromInt(1));
        array.SetIndex(1, RuntimeValue.FromInt(2));

        Assert.True(array.GetIndex(-1).IsNull);
        Assert.True(array.GetIndex(5).IsNull);
    }

    [Fact]
    public void Execute_Txt1_ReturnsFirstWord()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // txt1("hello world") should return "hello"
            emitter.EmitPushString("hello world");
            emitter.EmitCall("txt1", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("hello", result.AsString());
    }

    [Fact]
    public void Execute_Txt2_ReturnsRestAfterFirstWord()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // txt2("hello world test") should return "world test"
            emitter.EmitPushString("hello world test");
            emitter.EmitCall("txt2", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("world test", result.AsString());
    }

    [Fact]
    public void Execute_TxtWithLength_TruncatesString()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // txt("hello world", 5) should return "hello"
            emitter.EmitPushString("hello world");
            emitter.EmitPushInt(5);
            emitter.EmitCall("txt", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("hello", result.AsString());
    }

    [Fact]
    public void Execute_Txt1_SingleWord_ReturnsEntireWord()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // txt1("hello") should return "hello"
            emitter.EmitPushString("hello");
            emitter.EmitCall("txt1", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("hello", result.AsString());
    }

    [Fact]
    public void Execute_Txt2_SingleWord_ReturnsEmpty()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // txt2("hello") should return ""
            emitter.EmitPushString("hello");
            emitter.EmitCall("txt2", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("", result.AsString());
    }

    #region New Builtin Functions Tests

    [Fact]
    public void Execute_Txtsublin_ReturnsCorrectLine()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("line1\nline2\nline3");
            emitter.EmitPushInt(2);
            emitter.EmitCall("txtsublin", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("line2", result.AsString());
    }

    [Fact]
    public void Execute_Txtfim_ReturnsLastNChars()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("Hello World");
            emitter.EmitPushInt(5);
            emitter.EmitCall("txtfim", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("World", result.AsString());
    }

    [Fact]
    public void Execute_Txte_ReturnsOneIfContains()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("Hello World");
            emitter.EmitPushString("world");
            emitter.EmitCall("txte", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(1, result.AsInt()); // Case-insensitive
    }

    [Fact]
    public void Execute_Txtnum_FormatsWithLeadingZeros()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushInt(42);
            emitter.EmitPushInt(5);
            emitter.EmitCall("txtnum", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("00042", result.AsString());
    }

    [Fact]
    public void Execute_Txtproclin_ReturnsLineNumber()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("apple\nbanana\ncherry");
            emitter.EmitPushString("banana");
            emitter.EmitCall("txtproclin", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(2, result.AsInt()); // 1-based line number
    }

    [Fact]
    public void Execute_Txttrocamai_ReplacesIgnoringCase()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("Hello WORLD");
            emitter.EmitPushString("world");
            emitter.EmitPushString("Universe");
            emitter.EmitCall("txttrocamai", 3);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal("Hello Universe", result.AsString());
    }

    [Fact]
    public void Execute_Intpos_ReturnsOneBasedPosition()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("Hello World");
            emitter.EmitPushString("World");
            emitter.EmitCall("intpos", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(7, result.AsInt()); // 1-based
    }

    [Fact]
    public void Execute_Inttotal_SumsArrayElements()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // Create array using push (more reliable for test)
            emitter.EmitPushInt(0);
            emitter.EmitCall("vetor", 1);
            emitter.EmitStoreLocal(0);

            // Push values using arrpush
            emitter.EmitLoadLocal(0);
            emitter.EmitPushInt(10);
            emitter.EmitCall("push", 2);
            emitter.EmitPop();

            emitter.EmitLoadLocal(0);
            emitter.EmitPushInt(20);
            emitter.EmitCall("push", 2);
            emitter.EmitPop();

            emitter.EmitLoadLocal(0);
            emitter.EmitPushInt(30);
            emitter.EmitCall("push", 2);
            emitter.EmitPop();

            emitter.EmitLoadLocal(0);
            emitter.EmitCall("inttotal", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(60, result.AsInt());
    }

    [Fact]
    public void Execute_Intbit_ChecksBitSet()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // Check if bit 2 is set in 5 (binary 101)
            emitter.EmitPushInt(5);
            emitter.EmitPushInt(2);
            emitter.EmitCall("intbit", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(1, result.AsInt()); // bit 2 is set in 5
    }

    [Fact]
    public void Execute_Intbith_SetsBit()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            // Set bit 1 in 4 (binary 100) -> should get 6 (binary 110)
            emitter.EmitPushInt(4);
            emitter.EmitPushInt(1);
            emitter.EmitCall("intbith", 2);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(6, result.AsInt());
    }

    [Fact]
    public void Execute_Matrad_ConvertsDegToRad()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushInt(180);
            emitter.EmitCall("matrad", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(Math.PI, result.AsDouble(), 5);
    }

    [Fact]
    public void Execute_Matdeg_ConvertsRadToDeg()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushDouble(Math.PI);
            emitter.EmitCall("matdeg", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(180.0, result.AsDouble(), 5);
    }

    [Fact]
    public void Execute_Tempo_ReturnsTimestamp()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitCall("tempo", 0);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.InRange(result.AsInt(), now - 1, now + 1);
    }

    [Fact]
    public void Execute_Args_ReturnsArgumentArray()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitCall("args", 0);
            emitter.EmitCall("tam", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.ExecuteFunction(func, new[] {
            RuntimeValue.FromInt(10),
            RuntimeValue.FromInt(20),
            RuntimeValue.FromInt(30)
        });

        Assert.Equal(3, result.AsInt());
    }

    [Fact]
    public void Execute_Txtsepara_SplitsText()
    {
        var unit = CreateTestUnit();
        var func = CreateFunction("test", emitter =>
        {
            emitter.EmitPushString("a,b,c");
            emitter.EmitPushString(",");
            emitter.EmitCall("txtsepara", 2);
            emitter.EmitCall("tam", 1);
            emitter.EmitReturnValue();
        }, unit.StringPool);
        unit.Functions["test"] = func;

        var interpreter = new BytecodeInterpreter(unit);
        var result = interpreter.Execute("test");

        Assert.Equal(3, result.AsInt());
    }

    #endregion

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
