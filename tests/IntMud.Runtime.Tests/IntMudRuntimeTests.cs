using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Types;
using IntMud.Runtime.Values;
using Xunit;

using CompiledVariable = IntMud.Compiler.Bytecode.CompiledVariable;
using CompiledFunction = IntMud.Compiler.Bytecode.CompiledFunction;

namespace IntMud.Runtime.Tests;

public class IntMudRuntimeTests
{
    private static CompiledUnit CreateUnitWithSpecialType(string className, string typeName, string varName)
    {
        var unit = new CompiledUnit { ClassName = className };
        unit.Variables.Add(new CompiledVariable
        {
            Name = varName,
            TypeName = typeName
        });
        return unit;
    }

    private static CompiledUnit CreateUnitWithFunction(string className, string funcName, byte[] bytecode)
    {
        var unit = new CompiledUnit { ClassName = className };
        unit.Functions[funcName] = new CompiledFunction
        {
            Name = funcName,
            Bytecode = bytecode
        };
        return unit;
    }

    [Fact]
    public void Initialize_WithTimerType_CreatesInstance()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "inttempo", "myTimer")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.Instances);
        Assert.True(runtime.Instances.ContainsKey("TestClass"));
    }

    [Fact]
    public void Initialize_WithExecTriggerType_CreatesInstance()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "intexec", "myTrigger")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.Instances);
    }

    [Fact]
    public void Initialize_WithConsoleType_CreatesInstance()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "telatxt", "tela")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.Instances);
    }

    [Fact]
    public void Initialize_WithServerType_CreatesInstance()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "serv", "servidor")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.Instances);
    }

    [Fact]
    public void Initialize_WithDebugType_CreatesInstance()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "debug", "dbg")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.Instances);
    }

    [Fact]
    public void Initialize_WithNoSpecialTypes_DoesNotCreateInstance()
    {
        var unit = new CompiledUnit { ClassName = "TestClass" };
        unit.Variables.Add(new CompiledVariable
        {
            Name = "normalVar",
            TypeName = "int32"
        });

        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = unit
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Empty(runtime.Instances);
    }

    [Fact]
    public void Initialize_WithMultipleSpecialTypes_CreatesMultipleInstances()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["Class1"] = CreateUnitWithSpecialType("Class1", "inttempo", "timer1"),
            ["Class2"] = CreateUnitWithSpecialType("Class2", "telatxt", "tela"),
            ["Class3"] = CreateUnitWithSpecialType("Class3", "intexec", "trigger")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Equal(3, runtime.Instances.Count);
    }

    [Fact]
    public void Initialize_RegistersTimersWithManager()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "inttempo", "myTimer")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.SpecialTypes.Timers);
        Assert.Equal("myTimer", runtime.SpecialTypes.Timers.First().VariableName);
    }

    [Fact]
    public void Initialize_RegistersExecTriggersWithManager()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "intexec", "myTrigger")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.SpecialTypes.ExecTriggers);
        Assert.Equal("myTrigger", runtime.SpecialTypes.ExecTriggers.First().VariableName);
    }

    [Fact]
    public void CreateInstance_WithValidClass_ReturnsInstance()
    {
        var unit = new CompiledUnit { ClassName = "TestClass" };
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = unit
        };

        var runtime = new IntMudRuntime(units);
        var instance = runtime.CreateInstance("TestClass");

        Assert.NotNull(instance);
        Assert.Equal("TestClass", instance!.ClassName);
    }

    [Fact]
    public void CreateInstance_WithInvalidClass_ReturnsNull()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase);

        var runtime = new IntMudRuntime(units);
        var instance = runtime.CreateInstance("NonExistent");

        Assert.Null(instance);
    }

    [Fact]
    public void CreateInstance_WithInheritance_ResolvesBaseClasses()
    {
        var baseUnit = new CompiledUnit { ClassName = "BaseClass" };
        baseUnit.Functions["baseFunc"] = new CompiledFunction
        {
            Name = "baseFunc",
            Bytecode = new byte[] { (byte)BytecodeOp.Return }
        };

        var derivedUnit = new CompiledUnit { ClassName = "DerivedClass" };
        derivedUnit.BaseClasses.Add("BaseClass");

        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["BaseClass"] = baseUnit,
            ["DerivedClass"] = derivedUnit
        };

        var runtime = new IntMudRuntime(units);
        var instance = runtime.CreateInstance("DerivedClass");

        Assert.NotNull(instance);
        Assert.True(instance!.HasMethod("baseFunc"));
    }

    [Fact]
    public void SetTimer_UpdatesTimerValue()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "inttempo", "myTimer")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();
        runtime.SetTimer("TestClass", "myTimer", 100);

        var timer = runtime.SpecialTypes.Timers.First();
        Assert.Equal(100, timer.Value);
    }

    [Fact]
    public void SetExecTrigger_UpdatesTriggerValue()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "intexec", "myTrigger")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();
        runtime.SetExecTrigger("TestClass", "myTrigger", 1);

        var trigger = runtime.SpecialTypes.ExecTriggers.First();
        Assert.Equal(1, trigger.Value);
    }

    [Fact]
    public void OnOutput_EventFires_WhenWriteOutputCalled()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase);
        var runtime = new IntMudRuntime(units);

        string? receivedOutput = null;
        runtime.OnOutput += text => receivedOutput = text;

        runtime.WriteOutput("Hello World");

        Assert.Equal("Hello World", receivedOutput);
    }

    [Fact]
    public void Initialize_CaseInsensitiveTypeNames_Works()
    {
        var unit = new CompiledUnit { ClassName = "TestClass" };
        unit.Variables.Add(new CompiledVariable
        {
            Name = "timer",
            TypeName = "INTTEMPO" // uppercase
        });

        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = unit
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();

        Assert.Single(runtime.Instances);
        Assert.Single(runtime.SpecialTypes.Timers);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        var units = new Dictionary<string, CompiledUnit>(StringComparer.OrdinalIgnoreCase)
        {
            ["TestClass"] = CreateUnitWithSpecialType("TestClass", "inttempo", "timer")
        };

        var runtime = new IntMudRuntime(units);
        runtime.Initialize();
        runtime.Dispose();

        Assert.False(runtime.IsRunning);
    }
}
