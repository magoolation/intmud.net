using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;

namespace IntMud.BuiltinFunctions.Functions;

/// <summary>
/// Object manipulation functions - Complete implementation matching original IntMUD.
/// </summary>
public class ObjectFunctions : IBuiltinFunction
{
    public IEnumerable<string> Names =>
    [
        // Object creation/deletion
        "criar", "apagar", "este", "ref",
        // Object navigation
        "objantes", "objdepois",
        // Object info
        "inttotal",
        // Internal helper functions
        "_objprim", "_objult", "_objprox", "_objant",
        "_objtot", "_objind", "_objfim",
        // Variable exchange
        "vartroca", "vartrocacod"
    ];

    public RuntimeValue Execute(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        var funcName = context.ExecutionContext.Arguments[0].AsString().ToLowerInvariant();
        return funcName switch
        {
            // Object creation/deletion
            "criar" => ExecuteCriar(context, args),
            "apagar" => ExecuteApagar(context, args),
            "este" => ExecuteEste(context),
            "ref" => ExecuteRef(context, args),

            // Object navigation
            "objantes" => ExecuteObjAntes(context, args),
            "objdepois" => ExecuteObjDepois(context, args),

            // Object info
            "inttotal" => ExecuteIntTotal(context, args),

            // Internal helpers
            "_objprim" => ExecuteObjPrim(context, args),
            "_objult" => ExecuteObjUlt(context, args),
            "_objprox" => ExecuteObjProx(context, args),
            "_objant" => ExecuteObjAnt(context, args),
            "_objtot" => ExecuteObjTot(context, args),
            "_objind" => ExecuteObjInd(context, args),
            "_objfim" => ExecuteObjFim(context, args),

            // Variable exchange
            "vartroca" => ExecuteVarTroca(context, args),
            "vartrocacod" => ExecuteVarTrocaCod(context, args),

            _ => RuntimeValue.Null
        };
    }

    #region Object Creation/Deletion

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

        // Return the first non-null object reference
        foreach (var arg in args)
        {
            var obj = arg.AsObject();
            if (obj != null)
                return RuntimeValue.FromObject(obj);
        }

        return RuntimeValue.Null;
    }

    #endregion

    #region Object Navigation

    /// <summary>
    /// objantes(obj) - Get previous object in class list.
    /// </summary>
    private static RuntimeValue ExecuteObjAntes(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        var obj = args[0].AsObject() as RuntimeObject;
        if (obj == null)
            return RuntimeValue.Null;

        var prev = obj.PreviousObject;
        return prev != null ? RuntimeValue.FromObject(prev) : RuntimeValue.Null;
    }

    /// <summary>
    /// objdepois(obj) - Get next object in class list.
    /// </summary>
    private static RuntimeValue ExecuteObjDepois(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        if (args.Length < 1)
            return RuntimeValue.Null;

        var obj = args[0].AsObject() as RuntimeObject;
        if (obj == null)
            return RuntimeValue.Null;

        var next = obj.NextObject;
        return next != null ? RuntimeValue.FromObject(next) : RuntimeValue.Null;
    }

    #endregion

    #region Object Info

    /// <summary>
    /// inttotal(value) - Get total count/length.
    /// For strings: length
    /// For objects: number of objects of same class
    /// For arrays: array length
    /// </summary>
    private static RuntimeValue ExecuteIntTotal(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        int total = 0;

        foreach (var arg in args)
        {
            switch (arg.Type)
            {
                case RuntimeValueType.String:
                    total += arg.AsString().Length;
                    break;

                case RuntimeValueType.Object:
                    var obj = arg.AsObject() as RuntimeObject;
                    if (obj != null)
                    {
                        // Return count of objects of this class
                        total += context.ClassRegistry.GetObjectCount(obj.Class.Name);
                    }
                    break;

                case RuntimeValueType.Array:
                    var arr = arg.AsArray();
                    if (arr != null)
                        total += arr.Count;
                    break;
            }
        }

        return RuntimeValue.FromInt(total);
    }

    #endregion

    #region Internal Helper Functions

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

    #endregion

    #region Variable Exchange

    /// <summary>
    /// vartroca(text, replacements...) - Variable-based text replacement.
    /// Replaces $varname with corresponding value.
    /// </summary>
    private static RuntimeValue ExecuteVarTroca(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        return ExecuteVarTrocaInternal(context, args, encoded: false);
    }

    /// <summary>
    /// vartrocacod(text, replacements...) - Variable-based text replacement with encoding.
    /// Same as vartroca but encodes special characters in values.
    /// </summary>
    private static RuntimeValue ExecuteVarTrocaCod(BuiltinFunctionContext context, RuntimeValue[] args)
    {
        return ExecuteVarTrocaInternal(context, args, encoded: true);
    }

    private static RuntimeValue ExecuteVarTrocaInternal(BuiltinFunctionContext context, RuntimeValue[] args, bool encoded)
    {
        if (args.Length < 1)
            return RuntimeValue.FromString("");

        var text = args[0].AsString();
        if (args.Length < 2)
            return RuntimeValue.FromString(text);

        // Build replacement dictionary from remaining args
        // Args come in pairs: name, value, name, value, ...
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i + 1 < args.Length; i += 2)
        {
            var name = args[i].AsString();
            var value = args[i + 1].AsString();

            if (encoded)
            {
                // Encode special characters
                value = EncodeValue(value);
            }

            replacements[name] = value;
        }

        // Perform replacements
        var result = new System.Text.StringBuilder();
        int pos = 0;

        while (pos < text.Length)
        {
            if (text[pos] == '$')
            {
                // Find variable name
                int start = pos + 1;
                int end = start;

                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                    end++;

                if (end > start)
                {
                    var varName = text[start..end];
                    if (replacements.TryGetValue(varName, out var replacement))
                    {
                        result.Append(replacement);
                        pos = end;
                        continue;
                    }
                }
            }

            result.Append(text[pos]);
            pos++;
        }

        return RuntimeValue.FromString(result.ToString());
    }

    private static string EncodeValue(string value)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in value)
        {
            if (c == '@' || c == '\\' || c == '"' || c < 32)
            {
                sb.Append('@');
                sb.Append((char)(c + 64));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    #endregion
}
