using IntMud.Runtime.Values;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Control flow and utility functions.
/// </summary>
public class ControlFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        "nulo", "classe", "nomeclasse",
        "exec", "execobj", "execclasse",
        "_progfim", "_progerro", "_progexec"
    ];

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        var funcName = args[0].AsString().ToLowerInvariant();

        return funcName switch
        {
            "nulo" => RuntimeValue.Null,
            "classe" => ExecuteClasse(context, args),
            "nomeclasse" => ExecuteNomeClasse(context, args),
            "exec" => ExecuteExec(context, args),
            "execobj" => ExecuteExecObj(context, args),
            "execclasse" => ExecuteExecClasse(context, args),
            "_progfim" => ExecuteProgFim(context),
            "_progerro" => ExecuteProgErro(context),
            "_progexec" => ExecuteProgExec(context),
            _ => RuntimeValue.Null
        };
    }

    /// <summary>
    /// classe(obj) - Get object's class name.
    /// </summary>
    private static RuntimeValue ExecuteClasse(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        var obj = args.Length > 1
            ? args[1].AsObject() as Runtime.Execution.RuntimeObject
            : context.CurrentObject;

        if (obj == null)
            return RuntimeValue.FromString("");

        return RuntimeValue.FromString(obj.Class.Name);
    }

    /// <summary>
    /// nomeclasse(className) - Get class definition.
    /// </summary>
    private static RuntimeValue ExecuteNomeClasse(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.Null;

        var className = args[1].AsString();
        var cls = context.ClassRegistry.GetClass(className);

        // Return some representation of the class
        return cls != null ? RuntimeValue.FromString(cls.Name) : RuntimeValue.Null;
    }

    /// <summary>
    /// exec(functionName, args...) - Execute function on current object.
    /// </summary>
    private static RuntimeValue ExecuteExec(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 2)
            return RuntimeValue.Null;

        var funcName = args[1].AsString();
        var funcArgs = new RuntimeValue[args.Length - 2];
        Array.Copy(args, 2, funcArgs, 0, funcArgs.Length);

        var obj = context.CurrentObject;
        if (obj == null)
            return RuntimeValue.Null;

        var function = obj.GetFunction(funcName);
        if (function == null)
            return RuntimeValue.Null;

        // Execute the function
        var interpreter = new Runtime.Execution.AstInterpreter(context.ExecutionContext);
        return interpreter.CallFunction(obj, function, funcArgs);
    }

    /// <summary>
    /// execobj(obj, functionName, args...) - Execute function on object.
    /// </summary>
    private static RuntimeValue ExecuteExecObj(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.Null;

        var obj = args[1].AsObject() as Runtime.Execution.RuntimeObject;
        if (obj == null)
            return RuntimeValue.Null;

        var funcName = args[2].AsString();
        var funcArgs = new RuntimeValue[args.Length - 3];
        Array.Copy(args, 3, funcArgs, 0, funcArgs.Length);

        var function = obj.GetFunction(funcName);
        if (function == null)
            return RuntimeValue.Null;

        var interpreter = new Runtime.Execution.AstInterpreter(context.ExecutionContext);
        return interpreter.CallFunction(obj, function, funcArgs);
    }

    /// <summary>
    /// execclasse(className, functionName, args...) - Execute function on class.
    /// </summary>
    private static RuntimeValue ExecuteExecClasse(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 3)
            return RuntimeValue.Null;

        var className = args[1].AsString();
        var cls = context.ClassRegistry.GetClass(className);
        if (cls == null)
            return RuntimeValue.Null;

        var funcName = args[2].AsString();
        var function = cls.LookupFunction(funcName);
        if (function == null)
            return RuntimeValue.Null;

        var funcArgs = new RuntimeValue[args.Length - 3];
        Array.Copy(args, 3, funcArgs, 0, funcArgs.Length);

        // Execute on first object or null target
        var obj = cls.FirstObject;
        var interpreter = new Runtime.Execution.AstInterpreter(context.ExecutionContext);
        return interpreter.CallFunction(obj, function, funcArgs);
    }

    /// <summary>
    /// _progfim - Check if program should terminate.
    /// </summary>
    private static RuntimeValue ExecuteProgFim(BuiltinFunctionContext context)
    {
        return RuntimeValue.FromInt(context.ExecutionContext.IsTerminated ? 1 : 0);
    }

    /// <summary>
    /// _progerro - Get last error.
    /// </summary>
    private static RuntimeValue ExecuteProgErro(BuiltinFunctionContext context)
    {
        // Not implemented yet - would need error tracking
        return RuntimeValue.FromInt(0);
    }

    /// <summary>
    /// _progexec - Get instruction count.
    /// </summary>
    private static RuntimeValue ExecuteProgExec(BuiltinFunctionContext context)
    {
        return RuntimeValue.FromInt(context.ExecutionContext.InstructionCount);
    }
}
