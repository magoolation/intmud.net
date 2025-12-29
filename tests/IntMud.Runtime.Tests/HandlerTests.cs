using IntMud.Types.Handlers;
using Xunit;

namespace IntMud.Runtime.Tests;

public class IntegerHandlerTests
{
    [Fact]
    public void Int32Handler_GetSetInt_Works()
    {
        var handler = new Int32Handler();
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);
        handler.SetInt(memory, 12345);

        Assert.Equal(12345, handler.GetInt(memory));
    }

    [Fact]
    public void Int32Handler_GetBool_ReturnsTrueForNonZero()
    {
        var handler = new Int32Handler();
        var memory = new byte[4];

        handler.SetInt(memory, 1);
        Assert.True(handler.GetBool(memory));

        handler.SetInt(memory, 0);
        Assert.False(handler.GetBool(memory));
    }

    [Fact]
    public void Int8Handler_ClampValues()
    {
        var handler = new Int8Handler();
        var memory = new byte[1];

        handler.SetInt(memory, 127);
        Assert.Equal(127, handler.GetInt(memory));

        handler.SetInt(memory, -128);
        Assert.Equal(-128, handler.GetInt(memory));
    }

    [Fact]
    public void UInt8Handler_UnsignedRange()
    {
        var handler = new UInt8Handler();
        var memory = new byte[1];

        handler.SetInt(memory, 255);
        Assert.Equal(255, handler.GetInt(memory));

        handler.SetInt(memory, 0);
        Assert.Equal(0, handler.GetInt(memory));
    }

    [Fact]
    public void Int16Handler_GetSetInt_Works()
    {
        var handler = new Int16Handler();
        var memory = new byte[2];

        handler.SetInt(memory, 32767);
        Assert.Equal(32767, handler.GetInt(memory));

        handler.SetInt(memory, -32768);
        Assert.Equal(-32768, handler.GetInt(memory));
    }

    [Fact]
    public void Int32Handler_Compare_Works()
    {
        var handler = new Int32Handler();
        var mem1 = new byte[4];
        var mem2 = new byte[4];

        handler.SetInt(mem1, 10);
        handler.SetInt(mem2, 20);

        Assert.True(handler.Compare(mem1, mem2) < 0);
        Assert.True(handler.Compare(mem2, mem1) > 0);

        handler.SetInt(mem2, 10);
        Assert.True(handler.Compare(mem1, mem2) == 0);
    }
}

public class RealHandlerTests
{
    [Fact]
    public void RealHandler_GetSetDouble_Works()
    {
        var handler = new RealHandler();
        var memory = new byte[4];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);
        handler.SetDouble(memory, 3.14f);

        Assert.Equal(3.14f, (float)handler.GetDouble(memory), precision: 2);
    }

    [Fact]
    public void Real2Handler_GetSetDouble_Works()
    {
        var handler = new Real2Handler();
        var memory = new byte[8];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);
        handler.SetDouble(memory, 3.141592653589793);

        Assert.Equal(3.141592653589793, handler.GetDouble(memory), precision: 10);
    }

    [Fact]
    public void RealHandler_SetFromInt_Converts()
    {
        var handler = new RealHandler();
        var memory = new byte[4];

        handler.SetInt(memory, 42);

        Assert.Equal(42.0f, (float)handler.GetDouble(memory));
    }
}

public class TextHandlerTests
{
    [Fact]
    public void Txt1Handler_GetSetText_Works()
    {
        var handler = new Txt1Handler(64);
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);
        handler.SetText(memory, "Hello, World!");

        Assert.Equal("Hello, World!", handler.GetText(memory));
    }

    [Fact]
    public void Txt1Handler_TruncatesLongText()
    {
        var handler = new Txt1Handler(10);
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);
        handler.SetText(memory, "This is a very long text that should be truncated");

        var result = handler.GetText(memory);
        Assert.True(result.Length <= 10);
    }

    [Fact]
    public void Txt1Handler_GetBool_ReturnsTrueForNonEmpty()
    {
        var handler = new Txt1Handler(64);
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);

        handler.SetText(memory, "test");
        Assert.True(handler.GetBool(memory));

        handler.SetText(memory, "");
        Assert.False(handler.GetBool(memory));
    }

    [Fact]
    public void Txt1Handler_GetInt_ParsesNumber()
    {
        var handler = new Txt1Handler(64);
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);
        handler.SetText(memory, "12345");

        Assert.Equal(12345, handler.GetInt(memory));
    }

    [Fact]
    public void Txt2Handler_LargerCapacity()
    {
        var handler = new Txt2Handler(300);
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);

        var longText = new string('X', 280);
        handler.SetText(memory, longText);

        Assert.Equal(longText, handler.GetText(memory));
    }
}

public class RefHandlerTests
{
    [Fact]
    public void RefHandler_NullByDefault()
    {
        var handler = new RefHandler();
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);

        Assert.False(handler.GetBool(memory)); // null = false
        Assert.Equal(0, handler.GetInt(memory));
    }

    [Fact]
    public void RefHandler_GetText_ReturnsNuloForNull()
    {
        var handler = new RefHandler();
        var memory = new byte[handler.GetSize(ReadOnlySpan<byte>.Empty)];

        handler.Initialize(memory, ReadOnlySpan<byte>.Empty);

        Assert.Equal("nulo", handler.GetText(memory));
    }
}

public class Int1HandlerTests
{
    [Fact]
    public void Int1Handler_BitValues()
    {
        var handler = new Int1Handler();
        var memory = new byte[1];

        handler.SetInt(memory, 0);
        Assert.Equal(0, handler.GetInt(memory));
        Assert.False(handler.GetBool(memory));

        handler.SetInt(memory, 1);
        Assert.Equal(1, handler.GetInt(memory));
        Assert.True(handler.GetBool(memory));
    }

    [Fact]
    public void Int1Handler_ClampsToBit()
    {
        var handler = new Int1Handler();
        var memory = new byte[1];

        // Any non-zero becomes 1
        handler.SetInt(memory, 100);
        Assert.Equal(1, handler.GetInt(memory));

        handler.SetInt(memory, -5);
        Assert.Equal(1, handler.GetInt(memory));
    }
}
