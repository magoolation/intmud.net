using System.Runtime.InteropServices;
using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Core.Variables;

namespace IntMud.Types.Handlers;

/// <summary>
/// Handler for listaobj (object list) variables.
/// Stores a managed reference to a List&lt;object&gt;.
/// </summary>
public sealed class ListaObjHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ListaObj;
    public override string TypeName => "listaobj";
    public override VariableType RuntimeType => VariableType.Object;

    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        // Initialize with a new empty list
        var list = new ObjectList();
        var handle = GCHandle.Alloc(list);
        RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var list = GetList(memory);
        return list != null && list.Count > 0;
    }

    public override int GetInt(ReadOnlySpan<byte> memory)
    {
        var list = GetList(memory);
        return list?.Count ?? 0;
    }

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetInt(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var list = GetList(memory);
        return list != null ? $"<listaobj:{list.Count}>" : "nulo";
    }

    public override void SetInt(Span<byte> memory, int value) { }
    public override void SetDouble(Span<byte> memory, double value) { }
    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        RefHandler.SetPointer(dest, RefHandler.GetPointer(source));
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetInt(left).CompareTo(GetInt(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right);
    }

    public static ObjectList? GetList(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as ObjectList;
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        var list = GetList(memory);
        if (list == null)
            return false;

        switch (functionName.ToLowerInvariant())
        {
            case "total":
            case "count":
                context.SetReturnInt(list.Count);
                return true;

            case "limpar":
            case "clear":
                list.Clear();
                return true;

            case "adicionar":
            case "add":
                // Expects object in arg0
                list.Add(context.GetObjectArgument(0));
                return true;

            case "remover":
            case "remove":
                var obj = context.GetObjectArgument(0);
                list.Remove(obj);
                return true;

            case "obter":
            case "get":
                var index = context.GetIntArgument(0);
                if (index >= 0 && index < list.Count)
                    context.SetReturnObject(list[index]);
                else
                    context.SetReturnNull();
                return true;

            case "primeiro":
            case "first":
                context.SetReturnObject(list.Count > 0 ? list[0] : null);
                return true;

            case "ultimo":
            case "last":
                context.SetReturnObject(list.Count > 0 ? list[^1] : null);
                return true;

            case "contem":
            case "contains":
                context.SetReturnBool(list.Contains(context.GetObjectArgument(0)));
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Handler for listaitem (list iterator) variables.
/// </summary>
public sealed class ListaItemHandler : VariableTypeHandlerBase
{
    public override OpCode OpCode => OpCode.ListaItem;
    public override string TypeName => "listaitem";
    public override VariableType RuntimeType => VariableType.Object;

    // Store: list pointer (IntPtr.Size) + current index (4 bytes)
    public override int GetSize(ReadOnlySpan<byte> instruction) => IntPtr.Size + 4;

    public override void Initialize(Span<byte> memory, ReadOnlySpan<byte> instruction)
    {
        memory.Clear();
    }

    public override bool GetBool(ReadOnlySpan<byte> memory)
    {
        var list = GetList(memory);
        var index = GetIndex(memory);
        return list != null && index < list.Count;
    }

    public override int GetInt(ReadOnlySpan<byte> memory) => GetIndex(memory);

    public override double GetDouble(ReadOnlySpan<byte> memory) => GetIndex(memory);

    public override string GetText(ReadOnlySpan<byte> memory)
    {
        var list = GetList(memory);
        var index = GetIndex(memory);
        return list != null ? $"<listaitem:{index}/{list.Count}>" : "nulo";
    }

    public override void SetInt(Span<byte> memory, int value)
    {
        SetIndex(memory, value);
    }

    public override void SetDouble(Span<byte> memory, double value)
    {
        SetIndex(memory, (int)value);
    }

    public override void SetText(Span<byte> memory, string value) { }

    public override void Assign(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        source.CopyTo(dest);
    }

    public override int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return GetIndex(left).CompareTo(GetIndex(right));
    }

    public override bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return RefHandler.GetPointer(left) == RefHandler.GetPointer(right) &&
               GetIndex(left) == GetIndex(right);
    }

    private static ObjectList? GetList(ReadOnlySpan<byte> memory)
    {
        var ptr = RefHandler.GetPointer(memory);
        if (ptr == IntPtr.Zero)
            return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as ObjectList;
    }

    private static int GetIndex(ReadOnlySpan<byte> memory)
    {
        return MemoryMarshal.Read<int>(memory[IntPtr.Size..]);
    }

    private static void SetIndex(Span<byte> memory, int value)
    {
        MemoryMarshal.Write(memory[IntPtr.Size..], in value);
    }

    public override bool ExecuteFunction(Span<byte> memory, string functionName, IExecutionContext context)
    {
        switch (functionName.ToLowerInvariant())
        {
            case "iniciar":
            case "begin":
                // Set list and reset index
                var listObj = context.GetObjectArgument(0);
                if (listObj is ObjectList list)
                {
                    var handle = GCHandle.Alloc(list);
                    RefHandler.SetPointer(memory, GCHandle.ToIntPtr(handle));
                    SetIndex(memory, 0);
                }
                return true;

            case "proximo":
            case "next":
                SetIndex(memory, GetIndex(memory) + 1);
                return true;

            case "anterior":
            case "prev":
                var idx = GetIndex(memory);
                if (idx > 0)
                    SetIndex(memory, idx - 1);
                return true;

            case "atual":
            case "current":
                var currentList = GetList(memory);
                var currentIdx = GetIndex(memory);
                if (currentList != null && currentIdx >= 0 && currentIdx < currentList.Count)
                    context.SetReturnObject(currentList[currentIdx]);
                else
                    context.SetReturnNull();
                return true;

            case "fim":
            case "end":
                var l = GetList(memory);
                var i = GetIndex(memory);
                context.SetReturnBool(l == null || i >= l.Count);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Helper class for object lists.
/// </summary>
public class ObjectList : List<object?>
{
}
