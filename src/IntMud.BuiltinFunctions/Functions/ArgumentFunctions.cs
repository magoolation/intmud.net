using IntMud.Runtime.Values;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Argument functions: arg0-arg9, args.
/// </summary>
public class ArgumentFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        "arg0", "arg1", "arg2", "arg3", "arg4",
        "arg5", "arg6", "arg7", "arg8", "arg9",
        "args"
    ];

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        var funcName = args.Length > 0 ? args[0].AsString().ToLowerInvariant() : "";

        if (funcName.StartsWith("arg") && funcName.Length == 4 && char.IsDigit(funcName[3]))
        {
            var index = funcName[3] - '0';
            return context.ExecutionContext.GetArgument(index);
        }

        if (funcName == "args")
        {
            return RuntimeValue.FromInt(context.ExecutionContext.ArgumentCount);
        }

        return RuntimeValue.Null;
    }
}
