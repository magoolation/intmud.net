using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Types;
using IntMud.Runtime.Values;
using Xunit;

namespace IntMud.Runtime.Tests;

public class SpecialTypeRegistryTests
{
    [Theory]
    [InlineData("inttempo", true)]
    [InlineData("INTTEMPO", true)]
    [InlineData("IntTempo", true)]
    [InlineData("int32", false)]
    [InlineData("txt100", false)]
    public void IsTimerType_ReturnsCorrectResult(string typeName, bool expected)
    {
        Assert.Equal(expected, SpecialTypeRegistry.IsTimerType(typeName));
    }

    [Theory]
    [InlineData("intexec", true)]
    [InlineData("INTEXEC", true)]
    [InlineData("IntExec", true)]
    [InlineData("int32", false)]
    public void IsExecTriggerType_ReturnsCorrectResult(string typeName, bool expected)
    {
        Assert.Equal(expected, SpecialTypeRegistry.IsExecTriggerType(typeName));
    }

    [Theory]
    [InlineData("telatxt", true)]
    [InlineData("TELATXT", true)]
    [InlineData("TelaTxt", true)]
    [InlineData("txt100", false)]
    public void IsConsoleType_ReturnsCorrectResult(string typeName, bool expected)
    {
        Assert.Equal(expected, SpecialTypeRegistry.IsConsoleType(typeName));
    }

    [Theory]
    [InlineData("serv", true)]
    [InlineData("SERV", true)]
    [InlineData("socket", false)]
    public void IsServerType_ReturnsCorrectResult(string typeName, bool expected)
    {
        Assert.Equal(expected, SpecialTypeRegistry.IsServerType(typeName));
    }

    [Theory]
    [InlineData("debug", true)]
    [InlineData("DEBUG", true)]
    [InlineData("dbg", false)]
    public void IsDebugType_ReturnsCorrectResult(string typeName, bool expected)
    {
        Assert.Equal(expected, SpecialTypeRegistry.IsDebugType(typeName));
    }

    [Theory]
    [InlineData("inttempo")]
    [InlineData("intexec")]
    [InlineData("telatxt")]
    [InlineData("serv")]
    [InlineData("debug")]
    public void IsSpecialType_ReturnsTrue_ForSpecialTypes(string typeName)
    {
        Assert.True(SpecialTypeRegistry.IsSpecialType(typeName));
    }

    [Theory]
    [InlineData("int32")]
    [InlineData("txt100")]
    [InlineData("real")]
    [InlineData("socket")]
    public void IsSpecialType_ReturnsFalse_ForRegularTypes(string typeName)
    {
        Assert.False(SpecialTypeRegistry.IsSpecialType(typeName));
    }

    [Fact]
    public void GetEventFunctionName_TimerExpired_ReturnsCorrectName()
    {
        var funcName = SpecialTypeRegistry.GetEventFunctionName("inttempo", "myTimer", SpecialTypeEvent.TimerExpired);
        Assert.Equal("myTimer_exec", funcName);
    }

    [Fact]
    public void GetEventFunctionName_ValueChanged_ReturnsCorrectName()
    {
        var funcName = SpecialTypeRegistry.GetEventFunctionName("intexec", "myTrigger", SpecialTypeEvent.ValueChanged);
        Assert.Equal("myTrigger_exec", funcName);
    }

    [Fact]
    public void GetEventFunctionName_KeyPressed_ReturnsCorrectName()
    {
        var funcName = SpecialTypeRegistry.GetEventFunctionName("telatxt", "tela", SpecialTypeEvent.KeyPressed);
        Assert.Equal("tela_tecla", funcName);
    }

    [Fact]
    public void GetEventFunctionName_DebugError_ReturnsCorrectName()
    {
        var funcName = SpecialTypeRegistry.GetEventFunctionName("debug", "dbg", SpecialTypeEvent.DebugError);
        Assert.Equal("dbg_erro", funcName);
    }

    [Fact]
    public void GetEventFunctionName_WrongType_ReturnsNull()
    {
        var funcName = SpecialTypeRegistry.GetEventFunctionName("int32", "myVar", SpecialTypeEvent.TimerExpired);
        Assert.Null(funcName);
    }
}

public class TimerInstanceTests
{
    private static BytecodeRuntimeObject CreateTestObject()
    {
        var unit = new CompiledUnit { ClassName = "TestClass" };
        return new BytecodeRuntimeObject(unit);
    }

    [Fact]
    public void TimerInstance_InitialValue_IsZero()
    {
        var owner = CreateTestObject();
        var timer = new TimerInstance(owner, "myTimer");

        Assert.Equal(0, timer.Value);
        Assert.False(timer.IsActive);
    }

    [Fact]
    public void TimerInstance_WithPositiveValue_IsActive()
    {
        var owner = CreateTestObject();
        var timer = new TimerInstance(owner, "myTimer") { Value = 100 };

        Assert.Equal(100, timer.Value);
        Assert.True(timer.IsActive);
    }

    [Fact]
    public void TimerInstance_WithZeroValue_IsNotActive()
    {
        var owner = CreateTestObject();
        var timer = new TimerInstance(owner, "myTimer") { Value = 0 };

        Assert.False(timer.IsActive);
    }
}

public class ExecTriggerInstanceTests
{
    private static BytecodeRuntimeObject CreateTestObject()
    {
        var unit = new CompiledUnit { ClassName = "TestClass" };
        return new BytecodeRuntimeObject(unit);
    }

    [Fact]
    public void ExecTriggerInstance_InitialValue_IsZero()
    {
        var owner = CreateTestObject();
        var trigger = new ExecTriggerInstance(owner, "myTrigger");

        Assert.Equal(0, trigger.Value);
        Assert.Equal(0, trigger.PreviousValue);
        Assert.False(trigger.ShouldFire);
    }

    [Fact]
    public void ExecTriggerInstance_SetNonZero_ShouldFire()
    {
        var owner = CreateTestObject();
        var trigger = new ExecTriggerInstance(owner, "myTrigger")
        {
            Value = 1,
            PreviousValue = 0
        };

        Assert.True(trigger.ShouldFire);
    }

    [Fact]
    public void ExecTriggerInstance_SetZero_ShouldNotFire()
    {
        var owner = CreateTestObject();
        var trigger = new ExecTriggerInstance(owner, "myTrigger")
        {
            Value = 0,
            PreviousValue = 1
        };

        Assert.False(trigger.ShouldFire);
    }

    [Fact]
    public void ExecTriggerInstance_SameValue_ShouldNotFire()
    {
        var owner = CreateTestObject();
        var trigger = new ExecTriggerInstance(owner, "myTrigger")
        {
            Value = 1,
            PreviousValue = 1
        };

        Assert.False(trigger.ShouldFire);
    }
}

public class SpecialTypeManagerTests
{
    private static BytecodeRuntimeObject CreateTestObject()
    {
        var unit = new CompiledUnit { ClassName = "TestClass" };
        return new BytecodeRuntimeObject(unit);
    }

    [Fact]
    public void RegisterTimer_AddsToTimerList()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();

        manager.RegisterTimer(owner, "myTimer");

        Assert.Single(manager.Timers);
    }

    [Fact]
    public void RegisterExecTrigger_AddsToTriggerList()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();

        manager.RegisterExecTrigger(owner, "myTrigger");

        Assert.Single(manager.ExecTriggers);
    }

    [Fact]
    public void ProcessTimerTick_DecreasesTimerValue()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterTimer(owner, "myTimer");
        manager.SetTimerValue(owner, "myTimer", 100);

        manager.ProcessTimerTick(10);

        var timer = manager.Timers.First();
        Assert.Equal(90, timer.Value);
    }

    [Fact]
    public void ProcessTimerTick_ReturnsFiredTimers()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterTimer(owner, "myTimer");
        manager.SetTimerValue(owner, "myTimer", 5);

        var firedTimers = manager.ProcessTimerTick(10).ToList();

        Assert.Single(firedTimers);
        Assert.Equal("myTimer", firedTimers[0].VariableName);
        Assert.Equal(0, firedTimers[0].Value);
    }

    [Fact]
    public void ProcessTimerTick_DoesNotFireInactiveTimers()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterTimer(owner, "myTimer");
        manager.SetTimerValue(owner, "myTimer", 0);

        var firedTimers = manager.ProcessTimerTick(10).ToList();

        Assert.Empty(firedTimers);
    }

    [Fact]
    public void ProcessExecTriggers_ReturnsFiredTriggers()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterExecTrigger(owner, "myTrigger");
        manager.SetExecTriggerValue(owner, "myTrigger", 1);

        var firedTriggers = manager.ProcessExecTriggers().ToList();

        Assert.Single(firedTriggers);
        Assert.Equal("myTrigger", firedTriggers[0].VariableName);
    }

    [Fact]
    public void ProcessExecTriggers_UpdatesPreviousValue()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterExecTrigger(owner, "myTrigger");
        manager.SetExecTriggerValue(owner, "myTrigger", 1);

        manager.ProcessExecTriggers();

        var trigger = manager.ExecTriggers.First();
        Assert.Equal(1, trigger.PreviousValue);
    }

    [Fact]
    public void ProcessExecTriggers_SecondCall_DoesNotFireAgain()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterExecTrigger(owner, "myTrigger");
        manager.SetExecTriggerValue(owner, "myTrigger", 1);

        manager.ProcessExecTriggers(); // First call fires
        var firedTriggers = manager.ProcessExecTriggers().ToList(); // Second call should not fire

        Assert.Empty(firedTriggers);
    }

    [Fact]
    public void Clear_RemovesAllInstances()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterTimer(owner, "timer1");
        manager.RegisterTimer(owner, "timer2");
        manager.RegisterExecTrigger(owner, "trigger1");

        manager.Clear();

        Assert.Empty(manager.Timers);
        Assert.Empty(manager.ExecTriggers);
    }

    [Fact]
    public void SetTimerValue_UpdatesCorrectTimer()
    {
        var manager = new SpecialTypeManager();
        var owner = CreateTestObject();
        manager.RegisterTimer(owner, "timer1");
        manager.RegisterTimer(owner, "timer2");

        manager.SetTimerValue(owner, "timer2", 50);

        var timer1 = manager.Timers.First(t => t.VariableName == "timer1");
        var timer2 = manager.Timers.First(t => t.VariableName == "timer2");
        Assert.Equal(0, timer1.Value);
        Assert.Equal(50, timer2.Value);
    }
}
