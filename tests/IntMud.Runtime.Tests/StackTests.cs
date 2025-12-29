using IntMud.Core.Variables;
using IntMud.Runtime.Stacks;
using Xunit;

namespace IntMud.Runtime.Tests;

public class DataStackTests
{
    [Fact]
    public void Allocate_ReturnsCorrectSizeSpan()
    {
        using var stack = new DataStack();

        var span = stack.Allocate(100);

        Assert.Equal(100, span.Length);
        Assert.Equal(100, stack.Position);
    }

    [Fact]
    public void PushString_StoresAndRetrievesString()
    {
        using var stack = new DataStack();

        var offset = stack.PushString("Hello, World!");
        var result = stack.GetString(offset);

        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Push_StoresAndRetrievesBytes()
    {
        using var stack = new DataStack();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        var offset = stack.Push(data);
        var span = stack.GetSpan(offset, 5);

        Assert.Equal(data, span.ToArray());
    }

    [Fact]
    public void Clear_ResetsPosition()
    {
        using var stack = new DataStack();

        stack.Allocate(100);
        stack.Clear();

        Assert.Equal(0, stack.Position);
    }

    [Fact]
    public void Free_ReducesPosition()
    {
        using var stack = new DataStack();

        stack.Allocate(100);
        stack.Free(40);

        Assert.Equal(60, stack.Position);
    }

    [Fact]
    public void SaveAndRestore_WorksCorrectly()
    {
        using var stack = new DataStack();

        stack.Allocate(50);
        var saved = stack.SavePosition();
        stack.Allocate(50);

        Assert.Equal(100, stack.Position);

        stack.RestorePosition(saved);

        Assert.Equal(50, stack.Position);
    }
}

public class VariableStackTests
{
    [Fact]
    public void PushInt_StoresValue()
    {
        var stack = new VariableStack();

        stack.PushInt(42);

        Assert.Equal(1, stack.Count);
        Assert.Equal(VariableType.Int, stack.Peek().Type);
        Assert.Equal(42, stack.Peek().IntValue);
    }

    [Fact]
    public void PushDouble_StoresValue()
    {
        var stack = new VariableStack();

        stack.PushDouble(3.14159);

        Assert.Equal(1, stack.Count);
        Assert.Equal(VariableType.Double, stack.Peek().Type);
        Assert.Equal(3.14159, stack.Peek().DoubleValue, precision: 5);
    }

    [Fact]
    public void Pop_ReturnsAndRemovesValue()
    {
        var stack = new VariableStack();

        stack.PushInt(1);
        stack.PushInt(2);
        stack.PushInt(3);

        Assert.Equal(3, stack.Pop().IntValue);
        Assert.Equal(2, stack.Pop().IntValue);
        Assert.Equal(1, stack.Pop().IntValue);
        Assert.Equal(0, stack.Count);
    }

    [Fact]
    public void Peek_ReturnsWithoutRemoving()
    {
        var stack = new VariableStack();

        stack.PushInt(42);
        var peek1 = stack.Peek();
        var peek2 = stack.Peek();

        Assert.Equal(42, peek1.IntValue);
        Assert.Equal(42, peek2.IntValue);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void PushBool_StoresAsInt()
    {
        var stack = new VariableStack();

        stack.PushBool(true);
        Assert.Equal(1, stack.Pop().IntValue);

        stack.PushBool(false);
        Assert.Equal(0, stack.Pop().IntValue);
    }

    [Fact]
    public void Duplicate_CopiesTopValue()
    {
        var stack = new VariableStack();

        stack.PushInt(42);
        stack.Duplicate();

        Assert.Equal(2, stack.Count);
        Assert.Equal(42, stack.Pop().IntValue);
        Assert.Equal(42, stack.Pop().IntValue);
    }

    [Fact]
    public void Swap_SwapsTopTwo()
    {
        var stack = new VariableStack();

        stack.PushInt(1);
        stack.PushInt(2);
        stack.Swap();

        Assert.Equal(1, stack.Pop().IntValue);
        Assert.Equal(2, stack.Pop().IntValue);
    }

    [Fact]
    public void Clear_RemovesAllValues()
    {
        var stack = new VariableStack();

        stack.PushInt(1);
        stack.PushInt(2);
        stack.PushInt(3);
        stack.Clear();

        Assert.Equal(0, stack.Count);
        Assert.True(stack.IsEmpty);
    }

    [Fact]
    public void Overflow_ThrowsException()
    {
        var stack = new VariableStack(maxEntries: 3);

        stack.PushInt(1);
        stack.PushInt(2);
        stack.PushInt(3);

        Assert.Throws<StackOverflowException>(() => stack.PushInt(4));
    }
}

public class FunctionStackTests
{
    [Fact]
    public void Push_CreatesNewFrame()
    {
        var stack = new FunctionStack();

        ref var frame = ref stack.Push();
        frame.FunctionName = "testFunc";
        frame.ArgumentCount = 2;

        Assert.Equal(1, stack.Depth);
    }

    [Fact]
    public void Pop_RemovesFrame()
    {
        var stack = new FunctionStack();

        ref var frame = ref stack.Push();
        frame.FunctionName = "test";

        stack.Pop();

        Assert.Equal(0, stack.Depth);
    }

    [Fact]
    public void Current_ReturnsTopFrame()
    {
        var stack = new FunctionStack();

        ref var frame1 = ref stack.Push();
        frame1.FunctionName = "outer";

        ref var frame2 = ref stack.Push();
        frame2.FunctionName = "inner";

        ref var current = ref stack.Current;
        Assert.Equal("inner", current.FunctionName);
    }

    [Fact]
    public void MaxDepth_ThrowsOnOverflow()
    {
        var stack = new FunctionStack(maxDepth: 3);

        stack.Push();
        stack.Push();
        stack.Push();

        Assert.Throws<StackOverflowException>(() => stack.Push());
    }

    [Fact]
    public void NestedCalls_MaintainsCallStack()
    {
        var stack = new FunctionStack();

        ref var f1 = ref stack.Push();
        f1.FunctionName = "main";
        f1.ArgumentCount = 0;

        ref var f2 = ref stack.Push();
        f2.FunctionName = "helper";
        f2.ArgumentCount = 2;

        Assert.Equal(2, stack.Depth);
        Assert.Equal("helper", stack.Current.FunctionName);

        stack.Pop();

        Assert.Equal(1, stack.Depth);
        Assert.Equal("main", stack.Current.FunctionName);
    }
}
