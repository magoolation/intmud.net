using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Object manipulation functions: criar, apagar, este, ref, etc.
/// </summary>
public class ObjectFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        "criar", "apagar", "este", "ref",
        "_objprim", "_objult", "_objprox", "_objant",
        "_objtot", "_objind", "_objfim"
    ];

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        var funcName = context.ExecutionContext.Arguments[0].AsString().ToLowerInvariant();
        return funcName switch
        {
            "criar" => ExecuteCriar(context, args),
            "apagar" => ExecuteApagar(context, args),
            "este" => ExecuteEste(context),
            "ref" => ExecuteRef(context, args),
            "_objprim" => ExecuteObjPrim(context, args),
            "_objult" => ExecuteObjUlt(context, args),
            "_objprox" => ExecuteObjProx(context, args),
            "_objant" => ExecuteObjAnt(context, args),
            "_objtot" => ExecuteObjTot(context, args),
            "_objind" => ExecuteObjInd(context, args),
            "_objfim" => ExecuteObjFim(context, args),
            _ => RuntimeValue.Null
        };
    }

    /// <summary>
    /// criar(className) - Create a new object.
    /// </summary>
    private static RuntimeValue ExecuteCriar(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        var className = args[0].AsString();
        var obj = context.ClassRegistry.CreateObject(className, context.ExecutionContext);
        return RuntimeValue.FromObject(obj);
    }

    /// <summary>
    /// apagar(obj) - Delete an object.
    /// </summary>
    private static RuntimeValue ExecuteApagar(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
        {
            // Delete current object
            if (context.CurrentObject != null)
            {
                context.ClassRegistry.MarkForDeletion(context.CurrentObject);
                return RuntimeValue.FromInt(1);
            }
            return RuntimeValue.FromInt(0);
        }

        var obj = args[0].AsObject() as RuntimeObject;
        if (obj != null)
        {
            context.ClassRegistry.MarkForDeletion(obj);
            return RuntimeValue.FromInt(1);
        }

        return RuntimeValue.FromInt(0);
    }

    /// <summary>
    /// este - Get current object.
    /// </summary>
    private static RuntimeValue ExecuteEste(BuiltinFunctionContext context)
    {
        return context.CurrentObject != null
            ? RuntimeValue.FromObject(context.CurrentObject)
            : RuntimeValue.Null;
    }

    /// <summary>
    /// ref(obj) - Get reference to object.
    /// </summary>
    private static RuntimeValue ExecuteRef(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        return args[0];  // Just return the same reference
    }

    /// <summary>
    /// _objprim(className) - Get first object of class.
    /// </summary>
    private static RuntimeValue ExecuteObjPrim(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        var className = args[0].AsString();
        var obj = context.ClassRegistry.GetFirstObject(className) as RuntimeObject;
        return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
    }

    /// <summary>
    /// _objult(className) - Get last object of class.
    /// </summary>
    private static RuntimeValue ExecuteObjUlt(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        var className = args[0].AsString();
        var obj = context.ClassRegistry.GetLastObject(className);
        return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
    }

    /// <summary>
    /// _objprox(obj) - Get next object.
    /// </summary>
    private static RuntimeValue ExecuteObjProx(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        RuntimeObject? obj;
        if (args.Length < 1)
            obj = context.CurrentObject;
        else
            obj = args[0].AsObject() as RuntimeObject;

        if (obj == null)
            return RuntimeValue.Null;

        var next = obj.NextObject;
        return next != null ? RuntimeValue.FromObject(next) : RuntimeValue.Null;
    }

    /// <summary>
    /// _objant(obj) - Get previous object.
    /// </summary>
    private static RuntimeValue ExecuteObjAnt(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        RuntimeObject? obj;
        if (args.Length < 1)
            obj = context.CurrentObject;
        else
            obj = args[0].AsObject() as RuntimeObject;

        if (obj == null)
            return RuntimeValue.Null;

        var prev = obj.PreviousObject;
        return prev != null ? RuntimeValue.FromObject(prev) : RuntimeValue.Null;
    }

    /// <summary>
    /// _objtot(className) - Get object count.
    /// </summary>
    private static RuntimeValue ExecuteObjTot(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.FromInt(0);

        var className = args[0].AsString();
        return RuntimeValue.FromInt(context.ClassRegistry.GetObjectCount(className));
    }

    /// <summary>
    /// _objind(className) - Reset object iteration and get first.
    /// </summary>
    private static RuntimeValue ExecuteObjInd(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        var className = args[0].AsString();
        context.ClassRegistry.ResetObjectIteration(className);
        var obj = context.ClassRegistry.GetNextObject(className);
        return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
    }

    /// <summary>
    /// _objfim(className) - Check if iteration is finished.
    /// </summary>
    private static RuntimeValue ExecuteObjFim(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.FromInt(1);

        var className = args[0].AsString();
        return RuntimeValue.FromInt(context.ClassRegistry.HasMoreObjects(className) ? 0 : 1);
    }
}
