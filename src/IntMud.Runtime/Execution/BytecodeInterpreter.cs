using IntMud.Runtime.Types;
using IntMud.Runtime.Values;
using BytecodeCompiledUnit = IntMud.Compiler.Bytecode.CompiledUnit;
using BytecodeCompiledFunction = IntMud.Compiler.Bytecode.CompiledFunction;
using BytecodeOp = IntMud.Compiler.Bytecode.BytecodeOp;
using ConstantType = IntMud.Compiler.Bytecode.ConstantType;

namespace IntMud.Runtime.Execution;

/// <summary>
/// Global registry that tracks all object instances by class name.
/// This implements the IntMUD $classname syntax to get first object of a class.
/// </summary>
public static class GlobalObjectRegistry
{
    private static readonly Dictionary<string, List<BytecodeRuntimeObject>> _objectsByClass = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    /// <summary>
    /// Register an object instance.
    /// </summary>
    public static void Register(BytecodeRuntimeObject obj)
    {
        if (obj == null) return;
        lock (_lock)
        {
            if (!_objectsByClass.TryGetValue(obj.ClassName, out var list))
            {
                list = new List<BytecodeRuntimeObject>();
                _objectsByClass[obj.ClassName] = list;
            }
            if (!list.Contains(obj))
            {
                // Maintain doubly linked list: append at end
                if (list.Count > 0)
                {
                    var last = list[list.Count - 1];
                    last.NextObject = obj;
                    obj.PreviousObject = last;
                    obj.NextObject = null;
                }
                else
                {
                    obj.PreviousObject = null;
                    obj.NextObject = null;
                }
                list.Add(obj);
            }
        }
    }

    /// <summary>
    /// Unregister an object instance.
    /// </summary>
    public static void Unregister(BytecodeRuntimeObject obj)
    {
        if (obj == null) return;
        lock (_lock)
        {
            if (_objectsByClass.TryGetValue(obj.ClassName, out var list))
            {
                // Fix doubly linked list before removing
                var prev = obj.PreviousObject;
                var next = obj.NextObject;
                if (prev != null) prev.NextObject = next;
                if (next != null) next.PreviousObject = prev;
                obj.PreviousObject = null;
                obj.NextObject = null;

                list.Remove(obj);
            }
        }
    }

    /// <summary>
    /// Get the first object of a class (implements $classname).
    /// </summary>
    public static BytecodeRuntimeObject? GetFirstObject(string className)
    {
        lock (_lock)
        {
            if (_objectsByClass.TryGetValue(className, out var list) && list.Count > 0)
            {
                return list[0];
            }
        }
        return null;
    }

    /// <summary>
    /// Get all objects of a class.
    /// </summary>
    public static IReadOnlyList<BytecodeRuntimeObject> GetObjects(string className)
    {
        lock (_lock)
        {
            if (_objectsByClass.TryGetValue(className, out var list))
            {
                return list.ToList();
            }
        }
        return Array.Empty<BytecodeRuntimeObject>();
    }

    /// <summary>
    /// Get all registered objects across all classes.
    /// </summary>
    public static IReadOnlyList<BytecodeRuntimeObject> GetAllObjects()
    {
        lock (_lock)
        {
            var result = new List<BytecodeRuntimeObject>();
            foreach (var list in _objectsByClass.Values)
            {
                result.AddRange(list);
            }
            return result;
        }
    }

    /// <summary>
    /// Clear all registered objects (for testing).
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _objectsByClass.Clear();
        }
    }
}

/// <summary>
/// Interprets bytecode instructions for the IntMUD virtual machine.
/// </summary>
public sealed class BytecodeInterpreter
{
    private readonly BytecodeCompiledUnit _unit;
    private readonly Dictionary<string, BytecodeCompiledUnit> _loadedUnits;
    private readonly RuntimeValue[] _valueStack;
    private readonly RuntimeValue[] _locals;
    private readonly Dictionary<string, RuntimeValue> _globals;
    private readonly Stack<CallFrame> _callStack;
    private int _sp; // Stack pointer
    private int _ip; // Instruction pointer

    private const int MaxStackSize = 500;
    private const int MaxCallDepth = 40;
    private const int MaxLocals = 256;

    /// <summary>
    /// Delegate for writing output (escreva function).
    /// </summary>
    public Action<string>? WriteOutput { get; set; }

    /// <summary>
    /// Delegate for reading input (leia function).
    /// </summary>
    public Func<string>? ReadInput { get; set; }

    /// <summary>
    /// Buffer for captured output (for testing).
    /// </summary>
    private readonly List<string> _outputBuffer = new();

    /// <summary>
    /// Random number generator for math functions.
    /// </summary>
    private readonly Random _random = new();

    /// <summary>
    /// Get all output that was written.
    /// </summary>
    public IReadOnlyList<string> OutputBuffer => _outputBuffer;

    /// <summary>
    /// Clear the output buffer.
    /// </summary>
    public void ClearOutputBuffer() => _outputBuffer.Clear();

    public BytecodeInterpreter(BytecodeCompiledUnit unit, Dictionary<string, BytecodeCompiledUnit>? loadedUnits = null)
    {
        _unit = unit;
        _loadedUnits = loadedUnits ?? new Dictionary<string, BytecodeCompiledUnit>(StringComparer.OrdinalIgnoreCase);
        _valueStack = new RuntimeValue[MaxStackSize];
        _locals = new RuntimeValue[MaxLocals];
        _globals = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
        _callStack = new Stack<CallFrame>(MaxCallDepth);
        _sp = 0;
        _ip = 0;
    }

    /// <summary>
    /// The compiled unit being executed.
    /// </summary>
    public BytecodeCompiledUnit Unit => _unit;

    /// <summary>
    /// Global variables.
    /// </summary>
    public Dictionary<string, RuntimeValue> Globals => _globals;

    /// <summary>
    /// Execute a function by name.
    /// </summary>
    public RuntimeValue Execute(string functionName, params RuntimeValue[] arguments)
    {
        if (!_unit.Functions.TryGetValue(functionName, out var function))
        {
            throw new RuntimeException($"Function '{functionName}' not found");
        }

        return ExecuteFunction(function, arguments);
    }

    /// <summary>
    /// Execute a compiled function.
    /// </summary>
    public RuntimeValue ExecuteFunction(BytecodeCompiledFunction function, RuntimeValue[] arguments)
    {
        // Push call frame
        if (_callStack.Count >= MaxCallDepth)
        {
            throw new RuntimeException("Call stack overflow");
        }

        var frame = new CallFrame
        {
            Function = function,
            ReturnAddress = _ip,
            LocalsBase = 0, // Each function gets its own locals space
            StackBase = _sp,
            Arguments = arguments
        };
        _callStack.Push(frame);

        // Initialize locals
        Array.Clear(_locals, 0, _locals.Length);

        // Execute bytecode
        _ip = 0;
        var bytecode = function.Bytecode;
        var stringPool = _unit.StringPool;

        try
        {
            while (_ip < bytecode.Length)
            {
                var op = (BytecodeOp)bytecode[_ip++];

                switch (op)
                {
                    case BytecodeOp.Nop:
                        break;

                    case BytecodeOp.Pop:
                        if (_sp > frame.StackBase)
                            _sp--;
                        break;

                    case BytecodeOp.Dup:
                        if (_sp <= frame.StackBase)
                            throw new RuntimeException("Stack underflow");
                        Push(_valueStack[_sp - 1]);
                        break;

                    case BytecodeOp.Swap:
                        if (_sp - frame.StackBase < 2)
                            throw new RuntimeException("Stack underflow");
                        (_valueStack[_sp - 1], _valueStack[_sp - 2]) = (_valueStack[_sp - 2], _valueStack[_sp - 1]);
                        break;

                    case BytecodeOp.PushNull:
                        Push(RuntimeValue.Null);
                        break;

                    case BytecodeOp.PushInt:
                        Push(RuntimeValue.FromInt(ReadInt32(bytecode)));
                        break;

                    case BytecodeOp.PushDouble:
                        Push(RuntimeValue.FromDouble(ReadDouble(bytecode)));
                        break;

                    case BytecodeOp.PushString:
                        var strIdx = ReadUInt16(bytecode);
                        Push(RuntimeValue.FromString(stringPool[strIdx]));
                        break;

                    case BytecodeOp.PushTrue:
                        Push(RuntimeValue.True);
                        break;

                    case BytecodeOp.PushFalse:
                        Push(RuntimeValue.False);
                        break;

                    case BytecodeOp.LoadLocal:
                        var localIdx = ReadUInt16(bytecode);
                        Push(_locals[localIdx]);
                        break;

                    case BytecodeOp.StoreLocal:
                        localIdx = ReadUInt16(bytecode);
                        _locals[localIdx] = Pop();
                        break;

                    case BytecodeOp.LoadGlobal:
                        var globalName = stringPool[ReadUInt16(bytecode)];
                        Push(_globals.TryGetValue(globalName, out var globalVal) ? globalVal : RuntimeValue.Null);
                        break;

                    case BytecodeOp.StoreGlobal:
                        globalName = stringPool[ReadUInt16(bytecode)];
                        _globals[globalName] = Pop();
                        break;

                    case BytecodeOp.LoadField:
                        var fieldName = stringPool[ReadUInt16(bytecode)];
                        var obj = Pop();
                        Push(LoadField(obj, fieldName));
                        break;

                    case BytecodeOp.StoreField:
                        fieldName = stringPool[ReadUInt16(bytecode)];
                        var value = Pop();
                        obj = Pop();
                        StoreField(obj, fieldName, value);
                        break;

                    case BytecodeOp.LoadFieldDynamic:
                        // Field name is on stack as string
                        var dynamicFieldName = Pop().AsString();
                        obj = Pop();
                        Push(LoadField(obj, dynamicFieldName));
                        break;

                    case BytecodeOp.StoreFieldDynamic:
                        // Stack: [value, object, fieldName] (fieldName at top)
                        dynamicFieldName = Pop().AsString();
                        obj = Pop();
                        value = Pop();
                        StoreField(obj, dynamicFieldName, value);
                        break;

                    case BytecodeOp.LoadArg:
                        var argIdx = bytecode[_ip++];
                        Push(argIdx < arguments.Length ? arguments[argIdx] : RuntimeValue.Null);
                        break;

                    case BytecodeOp.StoreArg:
                        var storeArgIdx = bytecode[_ip++];
                        if (storeArgIdx < arguments.Length)
                            arguments[storeArgIdx] = Pop();
                        else
                            Pop(); // Discard value if arg doesn't exist
                        break;

                    case BytecodeOp.LoadArgCount:
                        Push(RuntimeValue.FromInt(arguments.Length));
                        break;

                    case BytecodeOp.LoadThis:
                        var currentFrame = _callStack.Count > 0 ? _callStack.Peek() : default;
                        Push(currentFrame.ThisObject != null
                            ? RuntimeValue.FromObject(currentFrame.ThisObject)
                            : RuntimeValue.Null);
                        break;

                    case BytecodeOp.LoadIndex:
                        var index = Pop();
                        var array = Pop();
                        Push(LoadIndex(array, index));
                        break;

                    case BytecodeOp.StoreIndex:
                        // Stack order: [value, array, index] (index at top)
                        // After assignment expression compiles value, then array, then index
                        index = Pop();
                        array = Pop();
                        value = Pop();
                        StoreIndex(array, index, value);
                        break;

                    // Dynamic identifier operations
                    case BytecodeOp.Concat:
                        // Concatenate two strings on stack
                        var str2 = Pop().AsString();
                        var str1 = Pop().AsString();
                        Push(RuntimeValue.FromString(str1 + str2));
                        break;

                    case BytecodeOp.LoadDynamic:
                        // Load variable by dynamic name (name on stack)
                        // Resolves: local -> instance field -> global
                        var varName = Pop().AsString();
                        Push(LoadDynamicVariable(varName));
                        break;

                    case BytecodeOp.StoreDynamic:
                        // Store to variable by dynamic name
                        // Stack: [name, value] (value at top)
                        value = Pop();
                        varName = Pop().AsString();
                        StoreDynamicVariable(varName, value);
                        break;

                    // Arithmetic
                    case BytecodeOp.Add:
                        var b = Pop();
                        var a = Pop();
                        Push(a + b);
                        break;

                    case BytecodeOp.Sub:
                        b = Pop();
                        a = Pop();
                        Push(a - b);
                        break;

                    case BytecodeOp.Mul:
                        b = Pop();
                        a = Pop();
                        Push(a * b);
                        break;

                    case BytecodeOp.Div:
                        b = Pop();
                        a = Pop();
                        Push(a / b);
                        break;

                    case BytecodeOp.Mod:
                        b = Pop();
                        a = Pop();
                        Push(a % b);
                        break;

                    case BytecodeOp.Neg:
                        a = Pop();
                        Push(-a);
                        break;

                    case BytecodeOp.Inc:
                        a = Pop();
                        Push(RuntimeValue.FromInt(a.AsInt() + 1));
                        break;

                    case BytecodeOp.Dec:
                        a = Pop();
                        Push(RuntimeValue.FromInt(a.AsInt() - 1));
                        break;

                    // Bitwise
                    case BytecodeOp.BitAnd:
                        b = Pop();
                        a = Pop();
                        Push(a & b);
                        break;

                    case BytecodeOp.BitOr:
                        b = Pop();
                        a = Pop();
                        Push(a | b);
                        break;

                    case BytecodeOp.BitXor:
                        b = Pop();
                        a = Pop();
                        Push(a ^ b);
                        break;

                    case BytecodeOp.BitNot:
                        a = Pop();
                        Push(~a);
                        break;

                    case BytecodeOp.Shl:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.ShiftLeft(a, b));
                        break;

                    case BytecodeOp.Shr:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.ShiftRight(a, b));
                        break;

                    // Comparison
                    case BytecodeOp.Eq:
                        b = Pop();
                        a = Pop();
                        Push(a == b);
                        break;

                    case BytecodeOp.Ne:
                        b = Pop();
                        a = Pop();
                        Push(a != b);
                        break;

                    case BytecodeOp.Lt:
                        b = Pop();
                        a = Pop();
                        Push(a < b);
                        break;

                    case BytecodeOp.Le:
                        b = Pop();
                        a = Pop();
                        Push(a <= b);
                        break;

                    case BytecodeOp.Gt:
                        b = Pop();
                        a = Pop();
                        Push(a > b);
                        break;

                    case BytecodeOp.Ge:
                        b = Pop();
                        a = Pop();
                        Push(a >= b);
                        break;

                    case BytecodeOp.StrictEq:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.FromBool(a.StrictEquals(b)));
                        break;

                    case BytecodeOp.StrictNe:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.FromBool(!a.StrictEquals(b)));
                        break;

                    // Logical
                    case BytecodeOp.And:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.LogicalAnd(a, b));
                        break;

                    case BytecodeOp.Or:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.LogicalOr(a, b));
                        break;

                    case BytecodeOp.Not:
                        a = Pop();
                        Push(RuntimeValue.LogicalNot(a));
                        break;

                    // Control flow
                    case BytecodeOp.Jump:
                        var offset = ReadInt16(bytecode);
                        _ip += offset;
                        break;

                    case BytecodeOp.JumpIfTrue:
                        offset = ReadInt16(bytecode);
                        if (Pop().IsTruthy)
                            _ip += offset;
                        break;

                    case BytecodeOp.JumpIfFalse:
                        offset = ReadInt16(bytecode);
                        if (!Pop().IsTruthy)
                            _ip += offset;
                        break;

                    case BytecodeOp.JumpIfNull:
                        offset = ReadInt16(bytecode);
                        if (Pop().IsNull)
                            _ip += offset;
                        break;

                    case BytecodeOp.JumpIfNotNull:
                        offset = ReadInt16(bytecode);
                        if (!Pop().IsNull)
                            _ip += offset;
                        break;

                    // Function calls
                    case BytecodeOp.Call:
                        var funcName = stringPool[ReadUInt16(bytecode)];
                        var argCount = bytecode[_ip++];
                        ExecuteCall(funcName, argCount);
                        break;

                    case BytecodeOp.CallMethod:
                        var methodName = stringPool[ReadUInt16(bytecode)];
                        argCount = bytecode[_ip++];
                        ExecuteMethodCall(methodName, argCount);
                        break;

                    case BytecodeOp.CallMethodDynamic:
                        var dynamicMethodName = Pop().AsString();
                        argCount = bytecode[_ip++];
                        ExecuteMethodCall(dynamicMethodName, argCount);
                        break;

                    case BytecodeOp.CallDynamic:
                        var dynamicFuncName = Pop().AsString();
                        argCount = bytecode[_ip++];
                        ExecuteCall(dynamicFuncName, argCount);
                        break;

                    case BytecodeOp.CallBuiltin:
                        var builtinId = ReadUInt16(bytecode);
                        argCount = bytecode[_ip++];
                        ExecuteBuiltinCall(builtinId, argCount);
                        break;

                    case BytecodeOp.Return:
                        _callStack.Pop();
                        _sp = frame.StackBase;
                        return RuntimeValue.Null;

                    case BytecodeOp.ReturnValue:
                        var retVal = Pop();
                        _callStack.Pop();
                        _sp = frame.StackBase;
                        return retVal;

                    // Object operations
                    case BytecodeOp.New:
                        var className = stringPool[ReadUInt16(bytecode)];
                        var newArgCount = bytecode[_ip++];
                        // Pop arguments from stack (in reverse order)
                        var newArgs = new RuntimeValue[newArgCount];
                        for (int i = newArgCount - 1; i >= 0; i--)
                        {
                            newArgs[i] = Pop();
                        }
                        Push(CreateObject(className, newArgs));
                        break;

                    case BytecodeOp.Delete:
                        Pop(); // Discard the object to delete
                        Push(RuntimeValue.Null); // Delete expression evaluates to null
                        break;

                    case BytecodeOp.TypeOf:
                        a = Pop();
                        Push(RuntimeValue.FromString(GetTypeName(a)));
                        break;

                    case BytecodeOp.InstanceOf:
                        className = stringPool[ReadUInt16(bytecode)];
                        a = Pop();
                        Push(RuntimeValue.FromBool(IsInstanceOf(a, className)));
                        break;

                    case BytecodeOp.LoadClass:
                        className = stringPool[ReadUInt16(bytecode)];
                        Push(LoadClass(className));
                        break;

                    case BytecodeOp.LoadClassMember:
                        var classIdx = ReadUInt16(bytecode);
                        var memberIdx = ReadUInt16(bytecode);
                        className = stringPool[classIdx];
                        var memberName = stringPool[memberIdx];
                        Push(LoadClassMember(className, memberName));
                        break;

                    case BytecodeOp.LoadClassDynamic:
                        // Class name is on stack as string
                        className = Pop().AsString();
                        Push(LoadClass(className));
                        break;

                    case BytecodeOp.LoadClassMemberDynamic:
                        // Stack: [className, memberName]
                        memberName = Pop().AsString();
                        className = Pop().AsString();
                        Push(LoadClassMember(className, memberName));
                        break;

                    case BytecodeOp.StoreClassMember:
                        classIdx = ReadUInt16(bytecode);
                        memberIdx = ReadUInt16(bytecode);
                        className = stringPool[classIdx];
                        memberName = stringPool[memberIdx];
                        StoreClassMember(className, memberName, Pop());
                        break;

                    case BytecodeOp.StoreClassMemberDynamic:
                        // Stack: [value, className, memberName]
                        memberName = Pop().AsString();
                        className = Pop().AsString();
                        var storeValue = Pop();
                        StoreClassMember(className, memberName, storeValue);
                        break;

                    // Special
                    case BytecodeOp.Terminate:
                        throw new TerminateException();

                    case BytecodeOp.Debug:
                        // Debug breakpoint - could be expanded
                        break;

                    case BytecodeOp.Line:
                        // Line number for debugging - skip for now
                        ReadUInt16(bytecode);
                        break;

                    case BytecodeOp.InitSpecialType:
                        var specialTypeName = stringPool[ReadUInt16(bytecode)];
                        Push(CreateSpecialTypeInstance(specialTypeName));
                        break;

                    default:
                        throw new RuntimeException($"Unknown opcode: {op}");
                }
            }
        }
        finally
        {
            if (_callStack.Count > 0 && _callStack.Peek().Function == function)
            {
                _callStack.Pop();
            }
        }

        return RuntimeValue.Null;
    }

    private void Push(RuntimeValue value)
    {
        if (_sp >= MaxStackSize)
            throw new RuntimeException("Stack overflow");
        _valueStack[_sp++] = value;
    }

    private RuntimeValue Pop()
    {
        if (_sp <= 0)
            throw new RuntimeException("Stack underflow");
        return _valueStack[--_sp];
    }

    private int ReadInt32(byte[] bytecode)
    {
        var value = BitConverter.ToInt32(bytecode, _ip);
        _ip += 4;
        return value;
    }

    private double ReadDouble(byte[] bytecode)
    {
        var value = BitConverter.ToDouble(bytecode, _ip);
        _ip += 8;
        return value;
    }

    private ushort ReadUInt16(byte[] bytecode)
    {
        var value = BitConverter.ToUInt16(bytecode, _ip);
        _ip += 2;
        return value;
    }

    private short ReadInt16(byte[] bytecode)
    {
        var value = BitConverter.ToInt16(bytecode, _ip);
        _ip += 2;
        return value;
    }

    private RuntimeValue LoadField(RuntimeValue obj, string fieldName)
    {
        // Handle special type property access
        if (obj.Type == RuntimeValueType.Object)
        {
            var instance = obj.AsObject();
            var result = GetSpecialTypeProperty(instance, fieldName);
            if (result.HasValue)
                return result.Value;
        }

        // Handle BytecodeRuntimeObject
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is BytecodeRuntimeObject runtimeObj)
        {
            return runtimeObj.GetField(fieldName);
        }

        // Handle array element access with numeric field names (arr.0, arr.[i])
        if (obj.Type == RuntimeValueType.Array && int.TryParse(fieldName, out var index))
        {
            return obj.GetIndex(index);
        }

        // Handle string character access with numeric field names (s.0, s.[i])
        if (obj.Type == RuntimeValueType.String && int.TryParse(fieldName, out index))
        {
            var str = obj.AsString();
            if (index >= 0 && index < str.Length)
            {
                return RuntimeValue.FromString(str[index].ToString());
            }
            return RuntimeValue.Null;
        }

        // Handle special built-in properties
        return fieldName.ToLowerInvariant() switch
        {
            // String properties
            "tamanho" or "tam" when obj.Type == RuntimeValueType.String => RuntimeValue.FromInt(obj.AsString().Length),
            "maiusculo" or "mai" when obj.Type == RuntimeValueType.String => RuntimeValue.FromString(obj.AsString().ToUpperInvariant()),
            "minusculo" or "min" when obj.Type == RuntimeValueType.String => RuntimeValue.FromString(obj.AsString().ToLowerInvariant()),

            // Array/list properties - ini returns first element, fim returns last
            "tamanho" or "tam" or "total" when obj.Type == RuntimeValueType.Array => RuntimeValue.FromInt(obj.Length),
            "ini" or "primeiro" or "first" when obj.Type == RuntimeValueType.Array => obj.Length > 0 ? obj.GetIndex(0) : RuntimeValue.Null,
            "fim" or "ultimo" or "last" when obj.Type == RuntimeValueType.Array => obj.Length > 0 ? obj.GetIndex(obj.Length - 1) : RuntimeValue.Null,

            _ => RuntimeValue.Null
        };
    }

    /// <summary>
    /// Get property from a special type instance.
    /// Returns null if not a special type or property not found.
    /// </summary>
    private RuntimeValue? GetSpecialTypeProperty(object? instance, string propertyName)
    {
        var lowerProp = propertyName.ToLowerInvariant();

        switch (instance)
        {
            case TelaTxtInstance telaTxt:
                return GetTelaTxtProperty(telaTxt, propertyName);

            case TextoTxtInstance textoTxt:
                switch (lowerProp)
                {
                    case "linhas":
                        return RuntimeValue.FromInt(textoTxt.Linhas);
                    case "bytes":
                        return RuntimeValue.FromInt(textoTxt.Bytes);
                    case "ini":
                        return RuntimeValue.FromObject(textoTxt.Ini());
                    case "fim":
                        return RuntimeValue.FromObject(textoTxt.Fim());
                    // Methods that can be called without parentheses
                    case "limpar":
                        textoTxt.Limpar();
                        return RuntimeValue.Null;
                    case "rand":
                        textoTxt.Rand();
                        return RuntimeValue.Null;
                    case "ordena":
                        textoTxt.Ordena();
                        return RuntimeValue.Null;
                    default:
                        return null;
                }

            case TextoPosInstance textoPos:
                // Handle method-like properties (called without parentheses in IntMUD)
                switch (lowerProp)
                {
                    case "lin":
                        return RuntimeValue.FromInt(textoPos.Lin);
                    case "linha":
                        return RuntimeValue.FromInt(textoPos.Linha);
                    case "byte":
                        return RuntimeValue.FromInt(textoPos.Byte);
                    case "texto":
                        return RuntimeValue.FromString(textoPos.Texto());
                    case "depois":
                        // depois - Move to next line (method called as property)
                        textoPos.Depois();
                        return textoPos.Lin > 0 ? RuntimeValue.FromObject(textoPos) : RuntimeValue.Null;
                    case "antes":
                        // antes - Move to previous line (method called as property)
                        textoPos.Antes();
                        return textoPos.Lin > 0 ? RuntimeValue.FromObject(textoPos) : RuntimeValue.Null;
                    case "remove":
                        // remove - Remove current line (method called as property)
                        textoPos.Remove();
                        return RuntimeValue.Null;
                    case "juntar":
                        // juntar - Join with next line (method called as property)
                        textoPos.Juntar();
                        return RuntimeValue.Null;
                    default:
                        return null;
                }

            case ListaObjInstance listaObj:
                return lowerProp switch
                {
                    "total" => RuntimeValue.FromInt(listaObj.Total),
                    "ini" => listaObj.Ini() is { } ini ? RuntimeValue.FromObject(ini) : RuntimeValue.Null,
                    "fim" => listaObj.Fim() is { } fim ? RuntimeValue.FromObject(fim) : RuntimeValue.Null,
                    "objini" => listaObj.ObjIni is { } objIni ? RuntimeValue.FromObject(objIni) : RuntimeValue.Null,
                    "objfim" => listaObj.ObjFim is { } objFim ? RuntimeValue.FromObject(objFim) : RuntimeValue.Null,
                    _ => null,
                };

            case ListaItemInstance listaItem:
                switch (lowerProp)
                {
                    case "total":
                        return RuntimeValue.FromInt(listaItem.Total);
                    case "obj":
                        return listaItem.Obj is { } obj ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
                    case "objlista":
                        return listaItem.ObjLista is { } lista ? RuntimeValue.FromObject(lista) : RuntimeValue.Null;
                    case "objantes":
                        return listaItem.ObjAntes is { } antes ? RuntimeValue.FromObject(antes) : RuntimeValue.Null;
                    case "objdepois":
                        return listaItem.ObjDepois is { } depois ? RuntimeValue.FromObject(depois) : RuntimeValue.Null;
                    case "depois":
                        // depois - Move to next item (method called as property, matching C++ FuncDepois)
                        listaItem.Depois();
                        return listaItem.IsValid ? RuntimeValue.FromObject(listaItem) : RuntimeValue.Null;
                    case "antes":
                        // antes - Move to previous item (method called as property, matching C++ FuncAntes)
                        listaItem.Antes();
                        return listaItem.IsValid ? RuntimeValue.FromObject(listaItem) : RuntimeValue.Null;
                    case "remove":
                        // remove - Remove current item (method called as property)
                        listaItem.Remove();
                        return RuntimeValue.Null;
                    default:
                        return null;
                }

            case IndiceObjInstance indiceObj:
                return lowerProp switch
                {
                    "nome" => RuntimeValue.FromString(indiceObj.Nome),
                    _ => null
                };

            case IndiceItemInstance indiceItem:
                return lowerProp switch
                {
                    "txt" => RuntimeValue.FromString(indiceItem.Txt),
                    "obj" => indiceItem.Obj is { } obj ? RuntimeValue.FromObject(obj) : RuntimeValue.Null,
                    _ => null
                };

            case IntTempoInstance intTempo:
                return lowerProp switch
                {
                    "valor" or "" => RuntimeValue.FromInt(intTempo.Valor),
                    "abs" => RuntimeValue.FromInt(intTempo.Abs),
                    "pos" => RuntimeValue.FromInt(intTempo.Pos),
                    "neg" => RuntimeValue.FromInt(intTempo.Neg),
                    _ => null
                };

            case IntExecInstance intExec:
                return lowerProp switch
                {
                    "valor" or "" => RuntimeValue.FromInt(intExec.Valor),
                    _ => null
                };

            case IntIncInstance intInc:
                return lowerProp switch
                {
                    "valor" or "" => RuntimeValue.FromInt(intInc.Valor),
                    _ => null
                };

            case DataHoraInstance dataHora:
                return lowerProp switch
                {
                    "ano" => RuntimeValue.FromInt(dataHora.Ano),
                    "mes" => RuntimeValue.FromInt(dataHora.Mes),
                    "dia" => RuntimeValue.FromInt(dataHora.Dia),
                    "hora" => RuntimeValue.FromInt(dataHora.Hora),
                    "min" => RuntimeValue.FromInt(dataHora.Min),
                    "seg" => RuntimeValue.FromInt(dataHora.Seg),
                    "diasem" => RuntimeValue.FromInt(dataHora.DiaSem),
                    "numdias" => RuntimeValue.FromInt(dataHora.NumDias),
                    "numseg" => RuntimeValue.FromInt(dataHora.NumSeg),
                    "numtotal" => RuntimeValue.FromDouble(dataHora.NumTotal),
                    "bissexto" => RuntimeValue.FromBool(dataHora.Bissexto),
                    _ => null
                };

            case DebugInstance debug:
                return lowerProp switch
                {
                    "exec" => RuntimeValue.FromInt(debug.Exec),
                    "err" => RuntimeValue.FromInt(debug.Err),
                    "log" => RuntimeValue.FromInt(debug.Log),
                    "utempo" => RuntimeValue.FromDouble(debug.Utempo()),
                    "stempo" => RuntimeValue.FromDouble(debug.Stempo()),
                    "mem" => RuntimeValue.FromDouble(debug.Mem()),
                    "memmax" => RuntimeValue.FromDouble(debug.MemMax()),
                    "func" => RuntimeValue.FromInt(_callStack.Count),
                    _ => null
                };

            case ArqTxtInstance arqTxt:
                return lowerProp switch
                {
                    "valido" => RuntimeValue.FromBool(arqTxt.Valido),
                    "eof" => RuntimeValue.FromBool(arqTxt.Eof),
                    "pos" => RuntimeValue.FromInt(arqTxt.Pos),
                    _ => null
                };

            case TextoVarInstance textoVar:
                return lowerProp switch
                {
                    "total" => RuntimeValue.FromInt(textoVar.Total),
                    "ini" => RuntimeValue.FromString(textoVar.Ini()),
                    "fim" => RuntimeValue.FromString(textoVar.Fim()),
                    _ => null
                };

            case TextoObjInstance textoObj:
                return lowerProp switch
                {
                    "total" => RuntimeValue.FromInt(textoObj.Total),
                    "ini" => RuntimeValue.FromString(textoObj.Ini()),
                    "fim" => RuntimeValue.FromString(textoObj.Fim()),
                    _ => null
                };

            case NomeObjInstance nomeObj:
                return lowerProp switch
                {
                    "nome" => RuntimeValue.FromString(nomeObj.Nome()),
                    _ => null
                };

            case ArqDirInstance arqDir:
                return lowerProp switch
                {
                    "lin" => RuntimeValue.FromBool(arqDir.Lin),
                    "texto" => RuntimeValue.FromString(arqDir.Texto()),
                    _ => null
                };

            case ArqLogInstance arqLog:
                return lowerProp switch
                {
                    "valido" => RuntimeValue.FromBool(arqLog.Valido),
                    _ => null
                };

            case ArqMemInstance arqMem:
                return lowerProp switch
                {
                    "tamanho" => RuntimeValue.FromInt(arqMem.Tamanho),
                    "pos" => RuntimeValue.FromInt(arqMem.Pos),
                    "eof" => RuntimeValue.FromBool(arqMem.Eof),
                    _ => null
                };

            case ArqExecInstance arqExec:
                return lowerProp switch
                {
                    "valido" => RuntimeValue.FromBool(arqExec.Valido),
                    _ => null
                };

            case ArqProgInstance arqProg:
                return lowerProp switch
                {
                    "lin" => RuntimeValue.FromBool(arqProg.Lin),
                    "texto" => RuntimeValue.FromString(arqProg.Texto()),
                    _ => null
                };

            case ProgInstance prog:
                return lowerProp switch
                {
                    "arquivo" => RuntimeValue.FromString(prog.Arquivo()),
                    "arqnome" => RuntimeValue.FromString(prog.ArqNome()),
                    "classe" => RuntimeValue.FromString(prog.Classe()),
                    "nivel" => RuntimeValue.FromInt(prog.Nivel()),
                    _ => null
                };

            case ServInstance serv:
                return lowerProp switch
                {
                    "valido" => RuntimeValue.FromBool(serv.Valido),
                    _ => null
                };

            case SocketInstance socket:
                return lowerProp switch
                {
                    "valido" => RuntimeValue.FromBool(socket.Valido),
                    "ip" => RuntimeValue.FromString(socket.Ip),
                    "iplocal" => RuntimeValue.FromString(socket.IpLocal),
                    "porta" => RuntimeValue.FromInt(socket.Porta),
                    "proto" => RuntimeValue.FromInt(socket.Proto),
                    "aflooder" => RuntimeValue.FromInt(socket.AFlooder),
                    "cores" => RuntimeValue.FromInt(socket.Cores),
                    _ => null
                };

            default:
                return null;
        }
    }

    private void StoreField(RuntimeValue obj, string fieldName, RuntimeValue value)
    {
        // Handle special type property assignment
        if (obj.Type == RuntimeValueType.Object)
        {
            if (SetSpecialTypeProperty(obj.AsObject(), fieldName, value))
                return;
        }

        // Handle BytecodeRuntimeObject
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is BytecodeRuntimeObject runtimeObj)
        {
            // Apply type truncation/clamping to match C++ behavior
            var typeName = runtimeObj.GetFieldTypeName(fieldName);
            if (typeName != null)
                value = ClampValueForType(value, typeName);
            runtimeObj.SetField(fieldName, value);
            return;
        }

        // Handle array element assignment with numeric field names (arr.0 = x, arr.[i] = x)
        if (obj.Type == RuntimeValueType.Array && int.TryParse(fieldName, out var index))
        {
            obj.SetIndex(index, value);
        }
    }

    /// <summary>
    /// Clamp/truncate a RuntimeValue to match the declared variable type.
    /// Matches C++ OperadorAtrib semantics for basic numeric types.
    /// </summary>
    private static RuntimeValue ClampValueForType(RuntimeValue value, string typeName)
    {
        switch (typeName.ToLowerInvariant())
        {
            case "int1":
            {
                // C++ stores as a single bit: 0 or 1
                return RuntimeValue.FromInt(value.IsTruthy ? 1 : 0);
            }
            case "int8":
            {
                // C++ clamps to [-128, 127]
                var v = value.AsInt();
                if (v < -0x80) v = -0x80;
                else if (v > 0x7F) v = 0x7F;
                return RuntimeValue.FromInt(v);
            }
            case "uint8":
            {
                // C++ clamps to [0, 255]
                var v = value.AsInt();
                if (v < 0) v = 0;
                else if (v > 0xFF) v = 0xFF;
                return RuntimeValue.FromInt(v);
            }
            case "int16":
            {
                // C++ clamps to [-32768, 32767]
                var v = value.AsInt();
                if (v < -0x8000) v = -0x8000;
                else if (v > 0x7FFF) v = 0x7FFF;
                return RuntimeValue.FromInt(v);
            }
            case "uint16":
            {
                // C++ clamps to [0, 65535]
                var v = value.AsInt();
                if (v < 0) v = 0;
                else if (v > 0xFFFF) v = 0xFFFF;
                return RuntimeValue.FromInt(v);
            }
            case "int32":
            case "int":
            {
                // C++ stores as signed 32-bit int
                var v = value.AsInt();
                if (v < int.MinValue) v = int.MinValue;
                else if (v > int.MaxValue) v = int.MaxValue;
                return RuntimeValue.FromInt(v);
            }
            case "uint32":
            {
                // C++ clamps double to [0, 4294967295]
                var v = value.AsDouble();
                if (v < 0) v = 0;
                else if (v > 0xFFFFFFFFL) v = 0xFFFFFFFFL;
                return RuntimeValue.FromInt((long)(uint)v);
            }
            case "real":
            {
                // C++ uses float (32-bit); truncate double to float precision
                var v = (float)value.AsDouble();
                return RuntimeValue.FromDouble(v);
            }
            // "real2" or "real64" uses double natively - no truncation needed
            default:
                return value;
        }
    }

    /// <summary>
    /// Set property on a special type instance.
    /// Returns true if handled, false otherwise.
    /// </summary>
    private bool SetSpecialTypeProperty(object? instance, string propertyName, RuntimeValue value)
    {
        var lowerProp = propertyName.ToLowerInvariant();

        switch (instance)
        {
            case TelaTxtInstance telaTxt:
                SetTelaTxtProperty(telaTxt, propertyName, value);
                return true;

            case TextoPosInstance textoPos:
                switch (lowerProp)
                {
                    case "linha":
                        textoPos.Linha = (int)value.AsInt();
                        return true;
                }
                return false;

            case IndiceObjInstance indiceObj:
                switch (lowerProp)
                {
                    case "nome":
                        indiceObj.Nome = value.AsString();
                        return true;
                }
                return false;

            case IntTempoInstance intTempo:
                switch (lowerProp)
                {
                    case "valor":
                    case "":
                        intTempo.Valor = (int)value.AsInt();
                        return true;
                }
                return false;

            case IntExecInstance intExec:
                switch (lowerProp)
                {
                    case "valor":
                    case "":
                        intExec.Valor = (int)value.AsInt();
                        return true;
                }
                return false;

            case IntIncInstance intInc:
                switch (lowerProp)
                {
                    case "valor":
                    case "":
                        intInc.Valor = (int)value.AsInt();
                        return true;
                }
                return false;

            case DataHoraInstance dataHora:
                switch (lowerProp)
                {
                    case "ano": dataHora.Ano = (int)value.AsInt(); return true;
                    case "mes": dataHora.Mes = (int)value.AsInt(); return true;
                    case "dia": dataHora.Dia = (int)value.AsInt(); return true;
                    case "hora": dataHora.Hora = (int)value.AsInt(); return true;
                    case "min": dataHora.Min = (int)value.AsInt(); return true;
                    case "seg": dataHora.Seg = (int)value.AsInt(); return true;
                    case "numdias": dataHora.SetNumDias((int)value.AsInt()); return true;
                    case "numseg": dataHora.SetNumSeg((int)value.AsInt()); return true;
                    case "numtotal": dataHora.SetNumTotal(value.AsDouble()); return true;
                }
                return false;

            case DebugInstance debug:
                switch (lowerProp)
                {
                    case "exec": debug.Exec = (int)value.AsInt(); return true;
                    case "err": debug.Err = (int)value.AsInt(); return true;
                    case "log": debug.Log = (int)value.AsInt(); return true;
                }
                return false;

            case SocketInstance socket:
                switch (lowerProp)
                {
                    case "proto": socket.Proto = (int)value.AsInt(); return true;
                    case "aflooder": socket.AFlooder = (int)value.AsInt(); return true;
                    case "cores": socket.Cores = (int)value.AsInt(); return true;
                }
                return false;

            case ArqMemInstance arqMem:
                switch (lowerProp)
                {
                    case "pos": arqMem.Pos = (int)value.AsInt(); return true;
                }
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Load a variable by dynamic name. Resolves in order:
    /// 1. Instance field (on current 'this' object)
    /// 2. Global variable
    /// </summary>
    private RuntimeValue LoadDynamicVariable(string varName)
    {
        // First, try to load from current 'this' object's fields
        if (_callStack.Count > 0)
        {
            var frame = _callStack.Peek();
            if (frame.ThisObject is BytecodeRuntimeObject thisObj)
            {
                // Check if the field exists on this object
                var fieldValue = thisObj.GetField(varName);
                if (fieldValue.Type != RuntimeValueType.Null || thisObj.HasField(varName))
                {
                    return fieldValue;
                }
            }
        }

        // Fall back to global variables
        if (_globals.TryGetValue(varName, out var globalValue))
        {
            return globalValue;
        }

        // Variable not found - return null
        return RuntimeValue.Null;
    }

    /// <summary>
    /// Store to a variable by dynamic name. Resolves in order:
    /// 1. Instance field (on current 'this' object)
    /// 2. Global variable
    /// </summary>
    private void StoreDynamicVariable(string varName, RuntimeValue value)
    {
        // First, try to store to current 'this' object's fields
        if (_callStack.Count > 0)
        {
            var frame = _callStack.Peek();
            if (frame.ThisObject is BytecodeRuntimeObject thisObj)
            {
                // Check if the field exists on this object
                if (thisObj.HasField(varName))
                {
                    thisObj.SetField(varName, value);
                    return;
                }
            }
        }

        // Fall back to global variables
        _globals[varName] = value;
    }

    private RuntimeValue LoadIndex(RuntimeValue container, RuntimeValue index)
    {
        var idx = (int)index.AsInt();

        // Handle array indexing
        if (container.Type == RuntimeValueType.Array)
        {
            var array = container.AsArray();
            if (array == null || idx < 0 || idx >= array.Count)
                return RuntimeValue.Null;
            return array[idx];
        }

        // Handle string indexing (returns single character as string)
        if (container.Type == RuntimeValueType.String)
        {
            var str = container.AsString();
            if (idx < 0 || idx >= str.Length)
                return RuntimeValue.EmptyString;
            return RuntimeValue.FromString(str[idx].ToString());
        }

        return RuntimeValue.Null;
    }

    private void StoreIndex(RuntimeValue container, RuntimeValue index, RuntimeValue value)
    {
        var idx = (int)index.AsInt();

        // Handle array indexing
        if (container.Type == RuntimeValueType.Array)
        {
            var array = container.AsArray();
            if (array == null || idx < 0)
                return;

            // Auto-expand array if needed
            while (idx >= array.Count)
                array.Add(RuntimeValue.Null);

            array[idx] = value;
        }
        // Note: String indexing for assignment is not supported (strings are immutable)
    }

    private void ExecuteCall(string funcName, int argCount)
    {
        // Collect arguments from stack
        var args = new RuntimeValue[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        // Get current frame for 'this' context
        var currentFrame = _callStack.Count > 0 ? _callStack.Peek() : default;
        var thisObj = currentFrame.ThisObject;

        // Try to find function in current object's class (including inherited)
        if (thisObj != null)
        {
            var (method, definingUnit) = thisObj.GetMethodWithUnit(funcName);
            if (method != null && definingUnit != null)
            {
                var result = ExecuteFunctionWithThis(method, thisObj, definingUnit, args);
                Push(result);
                return;
            }

            // Try to find expression constant in current object's class (including inherited)
            // This handles cases like: const msg = _tela.msg(arg0)
            // When called as msg("text"), it should evaluate the expression with args
            var (constant, constantUnit) = thisObj.GetConstantWithUnit(funcName);
            if (constant != null && constantUnit != null && constant.Type == ConstantType.Expression && constant.ExpressionBytecode != null)
            {
                // Use the defining unit's string pool for the expression bytecode
                var result = ExecuteExpressionConstant(constant, thisObj, constantUnit, args);
                Push(result);
                return;
            }
        }

        // Try to find function in current unit (fallback for static context)
        if (_unit.Functions.TryGetValue(funcName, out var function))
        {
            RuntimeValue result;
            if (thisObj != null)
            {
                result = ExecuteFunctionWithThis(function, thisObj, args);
            }
            else
            {
                result = ExecuteFunction(function, args);
            }
            Push(result);
            return;
        }

        // Try to find expression constant in current unit
        if (_unit.Constants.TryGetValue(funcName, out var unitConstant) &&
            unitConstant.Type == ConstantType.Expression && unitConstant.ExpressionBytecode != null)
        {
            var result = thisObj != null
                ? ExecuteExpressionConstant(unitConstant, thisObj, _unit, args)
                : EvaluateExpressionBytecode(unitConstant.ExpressionBytecode);
            Push(result);
            return;
        }

        // Try builtin functions
        var builtinResult = CallBuiltinFunction(funcName, args);
        Push(builtinResult);
    }

    private void ExecuteMethodCall(string methodName, int argCount)
    {
        // Collect arguments from stack
        var args = new RuntimeValue[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        // Pop the object
        var obj = Pop();

        // Handle TelaTxtInstance method calls (telatxt special type)
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is TelaTxtInstance telaTxt)
        {
            var result = CallTelaTxtMethod(telaTxt, methodName, args);
            Push(result);
            return;
        }

        // Handle TextoTxtInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is TextoTxtInstance textoTxt)
        {
            var result = CallTextoTxtMethod(textoTxt, methodName, args);
            Push(result);
            return;
        }

        // Handle TextoPosInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is TextoPosInstance textoPos)
        {
            var result = CallTextoPosMethod(textoPos, methodName, args);
            Push(result);
            return;
        }

        // Handle ListaObjInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ListaObjInstance listaObj)
        {
            var result = CallListaObjMethod(listaObj, methodName, args);
            Push(result);
            return;
        }

        // Handle ListaItemInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ListaItemInstance listaItem)
        {
            var result = CallListaItemMethod(listaItem, methodName, args);
            Push(result);
            return;
        }

        // Handle IndiceObjInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is IndiceObjInstance indiceObj)
        {
            var result = CallIndiceObjMethod(indiceObj, methodName, args);
            Push(result);
            return;
        }

        // Handle IndiceItemInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is IndiceItemInstance indiceItem)
        {
            var result = CallIndiceItemMethod(indiceItem, methodName, args);
            Push(result);
            return;
        }

        // Handle DataHoraInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is DataHoraInstance dataHora)
        {
            var result = CallDataHoraMethod(dataHora, methodName, args);
            Push(result);
            return;
        }

        // Handle ArqTxtInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ArqTxtInstance arqTxt)
        {
            var result = CallArqTxtMethod(arqTxt, methodName, args);
            Push(result);
            return;
        }

        // Handle ArqSavInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ArqSavInstance arqSav)
        {
            var result = CallArqSavMethod(arqSav, methodName, args);
            Push(result);
            return;
        }

        // Handle ServInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ServInstance serv)
        {
            var result = CallServMethod(serv, methodName, args);
            Push(result);
            return;
        }

        // Handle SocketInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is SocketInstance socket)
        {
            var result = CallSocketMethod(socket, methodName, args);
            Push(result);
            return;
        }

        // Handle TextoVarInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is TextoVarInstance textoVar)
        {
            var result = CallTextoVarMethod(textoVar, methodName, args);
            Push(result);
            return;
        }

        // Handle TextoObjInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is TextoObjInstance textoObj)
        {
            var result = CallTextoObjMethod(textoObj, methodName, args);
            Push(result);
            return;
        }

        // Handle NomeObjInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is NomeObjInstance nomeObj)
        {
            var result = CallNomeObjMethod(nomeObj, methodName, args);
            Push(result);
            return;
        }

        // Handle ArqDirInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ArqDirInstance arqDir)
        {
            var result = CallArqDirMethod(arqDir, methodName, args);
            Push(result);
            return;
        }

        // Handle ArqLogInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ArqLogInstance arqLog)
        {
            var result = CallArqLogMethod(arqLog, methodName, args);
            Push(result);
            return;
        }

        // Handle ArqMemInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ArqMemInstance arqMem)
        {
            var result = CallArqMemMethod(arqMem, methodName, args);
            Push(result);
            return;
        }

        // Handle ArqExecInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ArqExecInstance arqExec)
        {
            var result = CallArqExecMethod(arqExec, methodName, args);
            Push(result);
            return;
        }

        // Handle ArqProgInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ArqProgInstance arqProg)
        {
            var result = CallArqProgMethod(arqProg, methodName, args);
            Push(result);
            return;
        }

        // Handle ProgInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is ProgInstance prog)
        {
            var result = CallProgMethod(prog, methodName, args);
            Push(result);
            return;
        }

        // Handle DebugInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is DebugInstance debug)
        {
            var result = CallDebugMethod(debug, methodName, args);
            Push(result);
            return;
        }

        // Handle IntTempoInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is IntTempoInstance intTempo)
        {
            var result = CallIntTempoMethod(intTempo, methodName, args);
            Push(result);
            return;
        }

        // Handle IntExecInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is IntExecInstance intExec)
        {
            var result = CallIntExecMethod(intExec, methodName, args);
            Push(result);
            return;
        }

        // Handle IntIncInstance method calls
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is IntIncInstance intInc2)
        {
            var result = CallIntIncMethod(intInc2, methodName, args);
            Push(result);
            return;
        }

        // Handle BytecodeRuntimeObject method calls with virtual dispatch
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is BytecodeRuntimeObject runtimeObj)
        {
            // Use GetMethodWithUnit to get the method and its defining class unit
            // This is important for inheritance - the method might be defined in a base class
            var (method, definingUnit) = runtimeObj.GetMethodWithUnit(methodName);
            if (method != null && definingUnit != null)
            {
                var result = ExecuteFunctionWithThis(method, runtimeObj, definingUnit, args);
                Push(result);
                return;
            }
        }

        // Handle built-in methods on strings
        if (obj.Type == RuntimeValueType.String)
        {
            var result = CallStringMethod(obj.AsString(), methodName, args);
            Push(result);
            return;
        }

        // Handle ClassReference - static method calls (classe:função with no object)
        // In IntMUD C++, static calls execute with this=null
        if (obj.Type == RuntimeValueType.ClassReference && obj.AsObject() is BytecodeCompiledUnit classUnit)
        {
            // Find the function in the class
            if (classUnit.Functions.TryGetValue(methodName, out var function))
            {
                // Execute the function with a null 'this' object
                // We need to create a temporary context without a valid 'this'
                var result = ExecuteStaticMethodCall(function, classUnit, args);
                Push(result);
                return;
            }
        }

        // Method not found
        Push(RuntimeValue.Null);
    }

    /// <summary>
    /// Call a method on a TelaTxtInstance (telatxt special type).
    /// </summary>
    private RuntimeValue CallTelaTxtMethod(TelaTxtInstance telaTxt, string methodName, RuntimeValue[] args)
    {
        // Connect telaTxt output to our WriteOutput delegate
        telaTxt.WriteOutput ??= WriteOutput;

        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "msg":
                // msg(text...) - Write messages to console
                var sb = new System.Text.StringBuilder();
                foreach (var arg in args)
                {
                    sb.Append(arg.AsString());
                }
                telaTxt.Msg(sb.ToString());
                return RuntimeValue.Null;

            case "limpa":
                // limpa - Clear the screen
                telaTxt.Limpa();
                return RuntimeValue.Null;

            case "bipe":
                // bipe - Emit a beep sound
                telaTxt.EmitBipe();
                return RuntimeValue.Null;

            case "proto":
                // proto - Get protocol (1 if active, 0 if not)
                return RuntimeValue.FromInt(telaTxt.Proto);

            case "posx":
                // posx - Get current column position
                return RuntimeValue.FromInt(telaTxt.PosX);

            case "texto":
                // texto - Get/set the input text
                if (args.Length > 0)
                {
                    telaTxt.Texto = args[0].AsString();
                }
                return RuntimeValue.FromString(telaTxt.Texto);

            case "total":
                // total - Get/set maximum input line length
                if (args.Length > 0)
                {
                    telaTxt.Total = (int)args[0].AsInt();
                }
                return RuntimeValue.FromInt(telaTxt.Total);

            case "linha":
                // linha - Get/set current line position
                if (args.Length > 0)
                {
                    telaTxt.Linha = (int)args[0].AsInt();
                }
                return RuntimeValue.FromInt(telaTxt.Linha);

            case "tecla":
                // tecla(key) - Simulate a key press
                if (args.Length > 0)
                {
                    telaTxt.ProcessKey(args[0].AsString());
                }
                return RuntimeValue.Null;

            default:
                // Unknown method
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a TextoTxtInstance.
    /// </summary>
    private RuntimeValue CallTextoTxtMethod(TextoTxtInstance textoTxt, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "addfim":
                // addfim(text) - Add line at end
                if (args.Length > 0)
                    textoTxt.AddFim(args[0].AsString());
                return RuntimeValue.Null;

            case "addini":
                // addini(text) - Add line at beginning
                if (args.Length > 0)
                    textoTxt.AddIni(args[0].AsString());
                return RuntimeValue.Null;

            case "limpar":
                // limpar() - Clear all lines
                textoTxt.Limpar();
                return RuntimeValue.Null;

            case "ler":
                // ler(filename) - Read from file
                if (args.Length > 0)
                    return RuntimeValue.FromBool(textoTxt.Ler(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            case "salvar":
                // salvar(filename) - Save to file
                if (args.Length > 0)
                    return RuntimeValue.FromBool(textoTxt.Salvar(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            case "rand":
                // rand() - Shuffle lines randomly
                textoTxt.Rand();
                return RuntimeValue.Null;

            case "ordena":
                // ordena() - Sort lines
                textoTxt.Ordena();
                return RuntimeValue.Null;

            case "ini":
                // ini() - Get first position
                return RuntimeValue.FromObject(textoTxt.Ini());

            case "fim":
                // fim() - Get last position
                return RuntimeValue.FromObject(textoTxt.Fim());

            case "getline":
                // getline(index) - Get line at index
                if (args.Length > 0)
                    return RuntimeValue.FromString(textoTxt.GetLine((int)args[0].AsInt()));
                return RuntimeValue.FromString("");

            case "setline":
                // setline(index, text) - Set line at index
                if (args.Length > 1)
                    textoTxt.SetLine((int)args[0].AsInt(), args[1].AsString());
                return RuntimeValue.Null;

            case "removeline":
                // removeline(index) - Remove line at index
                if (args.Length > 0)
                    textoTxt.RemoveLine((int)args[0].AsInt());
                return RuntimeValue.Null;

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a TextoPosInstance.
    /// </summary>
    private RuntimeValue CallTextoPosMethod(TextoPosInstance textoPos, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "depois":
                // depois() - Move to next line
                textoPos.Depois();
                // Return the position if still valid
                return textoPos.Lin > 0 ? RuntimeValue.FromObject(textoPos) : RuntimeValue.Null;

            case "antes":
                // antes() - Move to previous line
                textoPos.Antes();
                // Return the position if still valid
                return textoPos.Lin > 0 ? RuntimeValue.FromObject(textoPos) : RuntimeValue.Null;

            case "texto":
                // texto(start, len) - Get substring
                if (args.Length >= 2)
                    return RuntimeValue.FromString(textoPos.Texto((int)args[0].AsInt(), (int)args[1].AsInt()));
                if (args.Length >= 1)
                    return RuntimeValue.FromString(textoPos.Texto((int)args[0].AsInt()));
                return RuntimeValue.FromString(textoPos.Texto());

            case "textolin":
                // textolin(maxLen) - Get line text limited
                if (args.Length > 0)
                    return RuntimeValue.FromString(textoPos.TextoLin((int)args[0].AsInt()));
                return RuntimeValue.FromString(textoPos.Texto());

            case "mudar":
                // mudar(text, start, len) - Replace text
                if (args.Length >= 3)
                    textoPos.Mudar(args[0].AsString(), (int)args[1].AsInt(), (int)args[2].AsInt());
                else if (args.Length >= 1)
                    textoPos.Mudar(args[0].AsString());
                return RuntimeValue.Null;

            case "add":
                // add(text) - Add text line before current position
                // add(source, count) - Add lines from source position
                if (args.Length >= 2 && args[0].Type == RuntimeValueType.Object &&
                    args[0].AsObject() is TextoPosInstance sourcePos)
                {
                    return RuntimeValue.FromInt(textoPos.Add(sourcePos, (int)args[1].AsInt()));
                }
                if (args.Length >= 1)
                {
                    return RuntimeValue.FromInt(textoPos.Add(args[0].AsString()));
                }
                return RuntimeValue.FromInt(0);

            case "addpos":
                // addpos(text) - Add text line and move position past it
                // addpos(source, count) - Add lines and move position past them
                if (args.Length >= 2 && args[0].Type == RuntimeValueType.Object &&
                    args[0].AsObject() is TextoPosInstance sourcePosForAddPos)
                    return RuntimeValue.FromInt(textoPos.AddPos(sourcePosForAddPos, (int)args[1].AsInt()));
                if (args.Length >= 1)
                    return RuntimeValue.FromInt(textoPos.AddPos(args[0].AsString()));
                return RuntimeValue.FromInt(0);

            case "remove":
                // remove() - Remove current line
                textoPos.Remove();
                return RuntimeValue.Null;

            case "juntar":
                // juntar() - Join current line with PREVIOUS line
                return RuntimeValue.FromBool(textoPos.Juntar());

            case "txtproc":
                // txtproc(search, [startChar], [numLines]) - Search for text (case sensitive)
                if (args.Length >= 3)
                    return RuntimeValue.FromInt(textoPos.TxtProc(args[0].AsString(), (int)args[1].AsInt(), (int)args[2].AsInt()));
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(textoPos.TxtProc(args[0].AsString(), (int)args[1].AsInt()));
                if (args.Length >= 1)
                    return RuntimeValue.FromInt(textoPos.TxtProc(args[0].AsString()));
                return RuntimeValue.FromInt(-1);

            case "txtprocmai":
                // txtprocmai(search, [startChar], [numLines]) - Search for text (case insensitive)
                if (args.Length >= 3)
                    return RuntimeValue.FromInt(textoPos.TxtProcMai(args[0].AsString(), (int)args[1].AsInt(), (int)args[2].AsInt()));
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(textoPos.TxtProcMai(args[0].AsString(), (int)args[1].AsInt()));
                if (args.Length >= 1)
                    return RuntimeValue.FromInt(textoPos.TxtProcMai(args[0].AsString()));
                return RuntimeValue.FromInt(-1);

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a ListaObjInstance.
    /// </summary>
    private RuntimeValue CallListaObjMethod(ListaObjInstance listaObj, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "addini":
                // addini(obj, ...) - Add one or more objects at beginning
                {
                    ListaItemInstance? lastItem = null;
                    // Add in reverse order so first arg ends up first
                    for (int i = args.Length - 1; i >= 0; i--)
                    {
                        if (args[i].Type == RuntimeValueType.Object && args[i].AsObject() is { } addObj)
                            lastItem = listaObj.AddIni(addObj);
                    }
                    return lastItem != null ? RuntimeValue.FromObject(lastItem) : RuntimeValue.Null;
                }

            case "addfim":
                // addfim(obj, ...) - Add one or more objects at end
                {
                    ListaItemInstance? lastItem = null;
                    foreach (var arg in args)
                    {
                        if (arg.Type == RuntimeValueType.Object && arg.AsObject() is { } addObj)
                            lastItem = listaObj.AddFim(addObj);
                    }
                    return lastItem != null ? RuntimeValue.FromObject(lastItem) : RuntimeValue.Null;
                }

            case "addini1":
                // addini1(obj) - Add at beginning if not present, returns ListaItem or null
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object)
                {
                    var item = listaObj.AddIni1(args[0].AsObject()!);
                    return item != null ? RuntimeValue.FromObject(item) : RuntimeValue.Null;
                }
                return RuntimeValue.Null;

            case "addfim1":
                // addfim1(obj) - Add at end if not present, returns ListaItem or null
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object)
                {
                    var item = listaObj.AddFim1(args[0].AsObject()!);
                    return item != null ? RuntimeValue.FromObject(item) : RuntimeValue.Null;
                }
                return RuntimeValue.Null;

            case "ini":
                // ini() - Get first item
                var ini = listaObj.Ini();
                return ini != null ? RuntimeValue.FromObject(ini) : RuntimeValue.Null;

            case "fim":
                // fim() - Get last item
                var fim = listaObj.Fim();
                return fim != null ? RuntimeValue.FromObject(fim) : RuntimeValue.Null;

            case "limpar":
                // limpar() - Clear list
                listaObj.Limpar();
                return RuntimeValue.Null;

            case "apagar":
                // apagar() - Clear and delete objects
                listaObj.Apagar();
                return RuntimeValue.Null;

            case "possui":
                // possui(obj) - Check if contains
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object)
                {
                    var objToCheck = args[0].AsObject();
                    return RuntimeValue.FromBool(objToCheck != null && listaObj.Possui(objToCheck));
                }
                return RuntimeValue.FromBool(false);

            case "rand":
                // rand() - Shuffle list randomly
                listaObj.Rand();
                return RuntimeValue.Null;

            case "inverter":
                // inverter() - Reverse list
                listaObj.Inverter();
                return RuntimeValue.Null;

            case "remove":
                // remove(obj, ...) - Remove one or more objects; no args = remove duplicates
                if (args.Length > 0)
                {
                    int totalRemoved = 0;
                    foreach (var arg in args)
                    {
                        if (arg.Type == RuntimeValueType.Object && arg.AsObject() is { } objToRemove)
                            totalRemoved += listaObj.Remove(objToRemove);
                    }
                    return RuntimeValue.FromInt(totalRemoved);
                }
                else
                {
                    // No args: remove duplicate objects from list
                    listaObj.RemoveDuplicates();
                    return RuntimeValue.Null;
                }

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a ListaItemInstance.
    /// </summary>
    private RuntimeValue CallListaItemMethod(ListaItemInstance listaItem, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "depois":
                // depois(count) - Mutate this item to advance forward (C++ FuncDepois behavior)
                {
                    int depoisCount = args.Length > 0 ? (int)args[0].AsInt() : 1;
                    listaItem.Depois(depoisCount);
                    return listaItem.IsValid ? RuntimeValue.FromObject(listaItem) : RuntimeValue.Null;
                }

            case "antes":
                // antes(count) - Mutate this item to move backward (C++ FuncAntes behavior)
                {
                    int antesCount = args.Length > 0 ? (int)args[0].AsInt() : 1;
                    listaItem.Antes(antesCount);
                    return listaItem.IsValid ? RuntimeValue.FromObject(listaItem) : RuntimeValue.Null;
                }

            case "remove":
                // remove() - Remove this item
                listaItem.Remove();
                return RuntimeValue.Null;

            case "addantes":
                // addantes(obj) - Add before this item
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object)
                {
                    var objToAdd = args[0].AsObject();
                    if (objToAdd != null)
                        listaItem.AddAntes(objToAdd);
                }
                return RuntimeValue.Null;

            case "adddepois":
                // adddepois(obj) - Add after this item
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object)
                {
                    var objToAdd = args[0].AsObject();
                    if (objToAdd != null)
                        listaItem.AddDepois(objToAdd);
                }
                return RuntimeValue.Null;

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an IndiceObjInstance.
    /// </summary>
    private RuntimeValue CallIndiceObjMethod(IndiceObjInstance indiceObj, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "obj":
                // obj(name) - Get object by name
                if (args.Length > 0)
                {
                    var obj = IndiceObjInstance.Obj(args[0].AsString());
                    return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
                }
                return RuntimeValue.Null;

            case "ini":
                // ini() - Get first object in index (alphabetically)
                var iniObj = IndiceObjInstance.Ini();
                return iniObj != null ? RuntimeValue.FromObject(iniObj) : RuntimeValue.Null;

            case "fim":
                // fim() - Get last object in index (alphabetically)
                var fimObj = IndiceObjInstance.Fim();
                return fimObj != null ? RuntimeValue.FromObject(fimObj) : RuntimeValue.Null;

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an IndiceItemInstance.
    /// </summary>
    private RuntimeValue CallIndiceItemMethod(IndiceItemInstance indiceItem, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "antes":
                // antes() - Move to previous item (navigation)
                indiceItem.Antes();
                // Return the item's current object after moving
                return indiceItem.Obj != null ? RuntimeValue.FromObject(indiceItem.Obj) : RuntimeValue.Null;

            case "depois":
                // depois() - Move to next item (navigation)
                indiceItem.Depois();
                // Return the item's current object after moving
                return indiceItem.Obj != null ? RuntimeValue.FromObject(indiceItem.Obj) : RuntimeValue.Null;

            case "ini":
                // ini() - Move to first item
                var iniObj = indiceItem.Ini();
                return iniObj != null ? RuntimeValue.FromObject(iniObj) : RuntimeValue.Null;

            case "fim":
                // fim() - Move to last item
                var fimObj = indiceItem.Fim();
                return fimObj != null ? RuntimeValue.FromObject(fimObj) : RuntimeValue.Null;

            case "obj":
                // obj(name) - Look up object by name
                if (args.Length > 0)
                {
                    var obj = indiceItem.LookupObj(args[0].AsString());
                    return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
                }
                return RuntimeValue.Null;

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a DataHoraInstance.
    /// </summary>
    private RuntimeValue CallDataHoraMethod(DataHoraInstance dataHora, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "agora":
                dataHora.Agora();
                return RuntimeValue.Null;

            case "novadata":
                if (args.Length >= 1) dataHora.Ano = (int)args[0].AsInt();
                if (args.Length >= 2) dataHora.Mes = (int)args[1].AsInt();
                if (args.Length >= 3) dataHora.Dia = (int)args[2].AsInt();
                return RuntimeValue.Null;

            case "novahora":
                if (args.Length >= 1) dataHora.Hora = (int)args[0].AsInt();
                if (args.Length >= 2) dataHora.Min = (int)args[1].AsInt();
                if (args.Length >= 3) dataHora.Seg = (int)args[2].AsInt();
                return RuntimeValue.Null;

            case "antes":
                dataHora.Antes();
                return RuntimeValue.Null;

            case "depois":
                dataHora.Depois();
                return RuntimeValue.Null;

            // These are properties but C++ also exposes them as methods (setting numfunc)
            case "ano": return RuntimeValue.FromInt(dataHora.Ano);
            case "mes": return RuntimeValue.FromInt(dataHora.Mes);
            case "dia": return RuntimeValue.FromInt(dataHora.Dia);
            case "hora": return RuntimeValue.FromInt(dataHora.Hora);
            case "min": return RuntimeValue.FromInt(dataHora.Min);
            case "seg": return RuntimeValue.FromInt(dataHora.Seg);
            case "diasem": return RuntimeValue.FromInt(dataHora.DiaSem);
            case "bissexto": return RuntimeValue.FromInt(dataHora.Bissexto ? 1 : 0);
            case "numdias": return RuntimeValue.FromInt(dataHora.NumDias);
            case "numseg": return RuntimeValue.FromInt(dataHora.NumSeg);
            case "numtotal": return RuntimeValue.FromDouble(dataHora.NumTotal);

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an ArqTxtInstance.
    /// </summary>
    private RuntimeValue CallArqTxtMethod(ArqTxtInstance arqTxt, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "abrir":
                // abrir(filename, mode) - Open file
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(arqTxt.Abrir(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromBool(arqTxt.Abrir(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            case "ler":
                // ler() - Read line
                return RuntimeValue.FromString(arqTxt.Ler());

            case "escr":
                // escr(text) - Write line
                if (args.Length > 0)
                    arqTxt.Escr(args[0].AsString());
                return RuntimeValue.Null;

            case "escrsem":
                // escrsem(text) - Write without newline
                if (args.Length > 0)
                    arqTxt.EscrSem(args[0].AsString());
                return RuntimeValue.Null;

            case "fechar":
                // fechar() - Close file
                arqTxt.Fechar();
                return RuntimeValue.Null;

            case "flush":
                // flush() - Flush buffer
                arqTxt.Flush();
                return RuntimeValue.Null;

            case "existe":
                // existe(filename) - Check if file exists
                if (args.Length > 0)
                    return RuntimeValue.FromBool(arqTxt.Existe(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            case "truncar":
                // truncar(filename) - Truncate file
                if (args.Length > 0)
                    return RuntimeValue.FromBool(arqTxt.Truncar(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an ArqSavInstance.
    /// </summary>
    private RuntimeValue CallArqSavMethod(ArqSavInstance arqSav, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "ler":
                // ler(filename, listaobj, mode) - Read objects from file
                if (args.Length >= 3 && args[1].Type == RuntimeValueType.Object &&
                    args[1].AsObject() is ListaObjInstance lista)
                    return RuntimeValue.FromInt(arqSav.Ler(args[0].AsString(), lista, (int)args[2].AsInt()));
                if (args.Length >= 2 && args[1].Type == RuntimeValueType.Object &&
                    args[1].AsObject() is ListaObjInstance lista2)
                    return RuntimeValue.FromInt(arqSav.Ler(args[0].AsString(), lista2, 0));
                return RuntimeValue.FromInt(0);

            case "salvar":
                // salvar(filename, listaobj, mode, param) - Save objects to file
                if (args.Length >= 4 && args[1].Type == RuntimeValueType.Object &&
                    args[1].AsObject() is ListaObjInstance savLista)
                    return RuntimeValue.FromInt(arqSav.Salvar(args[0].AsString(), savLista,
                        (int)args[2].AsInt(), args[3].AsString()));
                if (args.Length >= 3 && args[1].Type == RuntimeValueType.Object &&
                    args[1].AsObject() is ListaObjInstance savLista2)
                    return RuntimeValue.FromInt(arqSav.Salvar(args[0].AsString(), savLista2,
                        (int)args[2].AsInt(), ""));
                if (args.Length >= 2 && args[1].Type == RuntimeValueType.Object &&
                    args[1].AsObject() is ListaObjInstance savLista3)
                    return RuntimeValue.FromInt(arqSav.Salvar(args[0].AsString(), savLista3, 0, ""));
                return RuntimeValue.FromInt(0);

            case "existe":
                // existe(filename) - Check if file exists
                if (args.Length > 0)
                    return RuntimeValue.FromBool(arqSav.Existe(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            case "valido":
                // valido(filename) - Check if file is valid
                if (args.Length > 0)
                    return RuntimeValue.FromBool(arqSav.Valido(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            case "senha":
                // senha(password) - Set password
                if (args.Length > 0)
                    arqSav.Senha(args[0].AsString());
                return RuntimeValue.Null;

            case "dias":
                // dias(filename) - Get file age in days
                if (args.Length > 0)
                    return RuntimeValue.FromInt(arqSav.Dias(args[0].AsString()));
                return RuntimeValue.FromInt(-1);

            case "apagar":
                // apagar(filename) - Delete file
                if (args.Length > 0)
                    return RuntimeValue.FromBool(arqSav.Apagar(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            case "limpar":
                // limpar(filename) - Truncate file
                if (args.Length > 0)
                    return RuntimeValue.FromBool(arqSav.Limpar(args[0].AsString()));
                return RuntimeValue.FromBool(false);

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a TextoVarInstance.
    /// </summary>
    private RuntimeValue CallTextoVarMethod(TextoVarInstance textoVar, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "valor":
                if (args.Length > 0)
                    return textoVar.Valor(args[0].AsString());
                return RuntimeValue.Null;
            case "valorini":
                return textoVar.ValorIni();
            case "valorfim":
                return textoVar.ValorFim();
            case "mudar":
                if (args.Length >= 2)
                    textoVar.Mudar(args[0].AsString(), args[1]);
                else if (args.Length == 1)
                {
                    // mudar("name=value") format
                    var s = args[0].AsString();
                    var eq = s.IndexOf('=');
                    if (eq >= 0)
                        textoVar.Mudar(s.Substring(0, eq), RuntimeValue.FromString(s.Substring(eq + 1)));
                }
                return RuntimeValue.Null;
            case "ini":
                return RuntimeValue.FromString(textoVar.Ini());
            case "fim":
                return RuntimeValue.FromString(textoVar.Fim());
            case "depois":
                return RuntimeValue.FromString(textoVar.Depois());
            case "antes":
                return RuntimeValue.FromString(textoVar.Antes());
            case "nomevar":
                return RuntimeValue.FromString(textoVar.NomeVar());
            case "tipo":
                if (args.Length > 0)
                    return RuntimeValue.FromString(textoVar.Tipo(args[0].AsString()));
                return RuntimeValue.FromString("");
            case "total":
                return RuntimeValue.FromInt(textoVar.Total);
            case "limpar":
                textoVar.Limpar();
                return RuntimeValue.Null;
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a TextoObjInstance.
    /// </summary>
    private RuntimeValue CallTextoObjMethod(TextoObjInstance textoObj, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "valor":
                if (args.Length > 0)
                {
                    var obj = textoObj.Valor(args[0].AsString());
                    return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
                }
                return RuntimeValue.Null;
            case "valorini":
            {
                var obj = textoObj.ValorIni();
                return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
            }
            case "valorfim":
            {
                var obj = textoObj.ValorFim();
                return obj != null ? RuntimeValue.FromObject(obj) : RuntimeValue.Null;
            }
            case "mudar":
                if (args.Length >= 2 && args[1].Type == RuntimeValueType.Object && args[1].AsObject() is BytecodeRuntimeObject rObj)
                    textoObj.Mudar(args[0].AsString(), rObj);
                else if (args.Length >= 2)
                    textoObj.Mudar(args[0].AsString(), null);
                return RuntimeValue.Null;
            case "ini":
                return RuntimeValue.FromString(textoObj.Ini());
            case "fim":
                return RuntimeValue.FromString(textoObj.Fim());
            case "depois":
                return RuntimeValue.FromString(textoObj.Depois());
            case "antes":
                return RuntimeValue.FromString(textoObj.Antes());
            case "nomevar":
                return RuntimeValue.FromString(textoObj.NomeVar());
            case "apagar":
                if (args.Length > 0)
                    textoObj.Apagar(args[0].AsString());
                return RuntimeValue.Null;
            case "total":
                return RuntimeValue.FromInt(textoObj.Total);
            case "limpar":
                textoObj.Limpar();
                return RuntimeValue.Null;
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a NomeObjInstance.
    /// </summary>
    private RuntimeValue CallNomeObjMethod(NomeObjInstance nomeObj, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "ini":
                if (args.Length > 0)
                    nomeObj.Ini(args[0].AsString());
                return RuntimeValue.Null;
            case "nome":
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object && args[0].AsObject() is BytecodeRuntimeObject searchObj)
                    return RuntimeValue.FromInt(nomeObj.FuncNome(searchObj) ? 1 : 0);
                return RuntimeValue.FromInt(0);
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an ArqDirInstance.
    /// </summary>
    private RuntimeValue CallArqDirMethod(ArqDirInstance arqDir, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "abrir":
                if (args.Length > 0) arqDir.Abrir(args[0].AsString());
                return RuntimeValue.Null;
            case "fechar":
                arqDir.Fechar();
                return RuntimeValue.Null;
            case "depois":
                arqDir.Depois();
                return RuntimeValue.Null;
            case "texto":
                return RuntimeValue.FromString(arqDir.Texto());
            case "lin":
                return RuntimeValue.FromBool(arqDir.Lin);
            case "tipo":
                return RuntimeValue.FromString(arqDir.Tipo());
            case "tamanho":
                return RuntimeValue.FromInt(arqDir.Tamanho());
            case "mtempo":
                return RuntimeValue.FromInt(arqDir.Mtempo());
            case "atempo":
                return RuntimeValue.FromInt(arqDir.Atempo());
            case "barra":
                if (args.Length > 0) return RuntimeValue.FromString(ArqDirInstance.Barra(args[0].AsString()));
                return RuntimeValue.FromString("");
            case "apagar":
                if (args.Length > 0) return RuntimeValue.FromBool(ArqDirInstance.Apagar(args[0].AsString()));
                return RuntimeValue.FromBool(false);
            case "apagardir":
                if (args.Length > 0) return RuntimeValue.FromBool(ArqDirInstance.ApagarDir(args[0].AsString()));
                return RuntimeValue.FromBool(false);
            case "criardir":
                if (args.Length > 0) return RuntimeValue.FromBool(ArqDirInstance.CriarDir(args[0].AsString()));
                return RuntimeValue.FromBool(false);
            case "renomear":
                if (args.Length >= 2) return RuntimeValue.FromBool(ArqDirInstance.Renomear(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromBool(false);
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an ArqLogInstance.
    /// </summary>
    private RuntimeValue CallArqLogMethod(ArqLogInstance arqLog, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "abrir":
                if (args.Length > 0) arqLog.Abrir(args[0].AsString());
                return RuntimeValue.Null;
            case "msg":
                if (args.Length > 0) arqLog.Msg(args[0].AsString());
                return RuntimeValue.Null;
            case "fechar":
                arqLog.Fechar();
                return RuntimeValue.Null;
            case "valido":
                return RuntimeValue.FromBool(arqLog.Valido);
            case "existe":
                if (args.Length > 0) return RuntimeValue.FromBool(arqLog.Existe(args[0].AsString()));
                return RuntimeValue.FromBool(false);
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an ArqMemInstance.
    /// </summary>
    private RuntimeValue CallArqMemMethod(ArqMemInstance arqMem, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "ler":
                if (args.Length > 0) return RuntimeValue.FromString(arqMem.Ler((int)args[0].AsInt()));
                return RuntimeValue.FromString(arqMem.Ler(0));
            case "escr":
                if (args.Length > 0) arqMem.Escr(args[0].AsString());
                return RuntimeValue.Null;
            case "lerbin":
                return RuntimeValue.FromInt(arqMem.LerBin());
            case "escrbin":
                if (args.Length > 0) arqMem.EscrBin((int)args[0].AsInt());
                return RuntimeValue.Null;
            case "lerbinesp":
                if (args.Length > 0) return RuntimeValue.FromString(arqMem.LerBinEsp((int)args[0].AsInt()));
                return RuntimeValue.FromString("");
            case "add":
                if (args.Length > 0) arqMem.Add(args[0].AsString());
                return RuntimeValue.Null;
            case "addbin":
                if (args.Length > 0)
                {
                    var hexStr = args[0].AsString();
                    var bytes = new byte[hexStr.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        if (i * 2 + 1 < hexStr.Length)
                            bytes[i] = Convert.ToByte(hexStr.Substring(i * 2, 2), 16);
                    }
                    arqMem.AddBin(bytes);
                }
                return RuntimeValue.Null;
            case "limpar":
                arqMem.Limpar();
                return RuntimeValue.Null;
            case "truncar":
                arqMem.Truncar();
                return RuntimeValue.Null;
            case "pos":
                if (args.Length > 0) { arqMem.Pos = (int)args[0].AsInt(); return RuntimeValue.FromInt(arqMem.Pos); }
                return RuntimeValue.FromInt(arqMem.Pos);
            case "tamanho":
                return RuntimeValue.FromInt(arqMem.Tamanho);
            case "eof":
                return RuntimeValue.FromBool(arqMem.Eof);
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an ArqExecInstance.
    /// </summary>
    private RuntimeValue CallArqExecMethod(ArqExecInstance arqExec, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "abrir":
                if (args.Length >= 2)
                    arqExec.Abrir(args[0].AsString(), args[1].AsString());
                else if (args.Length >= 1)
                    arqExec.Abrir(args[0].AsString(), "");
                return RuntimeValue.Null;
            case "msg":
                if (args.Length > 0) arqExec.Msg(args[0].AsString());
                return RuntimeValue.Null;
            case "ler":
                return RuntimeValue.FromString(arqExec.Ler());
            case "fechar":
                arqExec.Fechar();
                return RuntimeValue.Null;
            case "valido":
            case "aberto":
                return RuntimeValue.FromBool(arqExec.Valido);
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an ArqProgInstance.
    /// </summary>
    private RuntimeValue CallArqProgMethod(ArqProgInstance arqProg, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "abrir":
                if (args.Length > 0) arqProg.Abrir(args[0].AsString());
                return RuntimeValue.Null;
            case "fechar":
                arqProg.Fechar();
                return RuntimeValue.Null;
            case "depois":
                arqProg.Depois();
                return RuntimeValue.Null;
            case "lin":
                return RuntimeValue.FromBool(arqProg.Lin);
            case "texto":
                return RuntimeValue.FromString(arqProg.Texto());
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a ProgInstance.
    /// </summary>
    private RuntimeValue CallProgMethod(ProgInstance prog, string methodName, RuntimeValue[] args)
    {
        // Ensure prog has access to the class registry
        prog.SetRegistry(_loadedUnits);

        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            // File iteration - iniarq(filePattern?)
            case "iniarq":
                return RuntimeValue.FromString(prog.IniArq(
                    args.Length > 0 ? args[0].AsString() : ""));
            case "arquivo":
                return RuntimeValue.FromString(prog.Arquivo(
                    args.Length > 0 ? args[0].AsString() : ""));
            case "arqnome":
                return RuntimeValue.FromString(prog.ArqNome(
                    args.Length > 0 ? args[0].AsString() : ""));

            // Class iteration - iniclasse(pattern?)
            case "iniclasse":
                return RuntimeValue.FromString(prog.IniClasse(
                    args.Length > 0 ? args[0].AsString() : ""));
            case "classe":
                return RuntimeValue.FromString(prog.Classe());
            case "clini":
                return RuntimeValue.FromString(prog.ClIni());
            case "clfim":
                return RuntimeValue.FromString(prog.ClFim());
            case "clantes":
                return RuntimeValue.FromString(prog.ClAntes());
            case "cldepois":
                return RuntimeValue.FromString(prog.ClDepois());

            // Function iteration - inifunc(className, pattern?)
            case "inifunc":
                if (args.Length >= 2)
                    return RuntimeValue.FromString(prog.IniFunc(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromString(prog.IniFunc(args[0].AsString()));
                return RuntimeValue.FromString("");
            case "inifunctudo":
                if (args.Length >= 2)
                    return RuntimeValue.FromString(prog.IniFuncTudo(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromString(prog.IniFuncTudo(args[0].AsString()));
                return RuntimeValue.FromString("");
            case "inifunccl":
                if (args.Length >= 2)
                    return RuntimeValue.FromString(prog.IniFuncCl(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromString(prog.IniFuncCl(args[0].AsString()));
                return RuntimeValue.FromString("");

            // Inheritance - iniherda(className)
            case "iniherda":
                if (args.Length > 0)
                    return RuntimeValue.FromString(prog.IniHerda(args[0].AsString()));
                return RuntimeValue.FromString("");
            case "iniherdatudo":
                if (args.Length > 0)
                    return RuntimeValue.FromString(prog.IniHerdaTudo(args[0].AsString()));
                return RuntimeValue.FromString("");
            case "iniherdainv":
                if (args.Length > 0)
                    return RuntimeValue.FromString(prog.IniHerdaInv(args[0].AsString()));
                return RuntimeValue.FromString("");

            // Line iteration - inilinha(className, funcName?)
            case "inilinha":
                if (args.Length >= 2)
                    return RuntimeValue.FromString(prog.IniLinha(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromString(prog.IniLinha(args[0].AsString()));
                return RuntimeValue.FromString("");

            // Universal iteration
            case "lin":
                return RuntimeValue.FromInt(prog.Lin());
            case "texto":
                return RuntimeValue.FromString(prog.Texto());
            case "depois":
                return RuntimeValue.FromString(prog.Depois(
                    args.Length > 0 ? (int)args[0].AsInt() : 1));
            case "nivel":
                return RuntimeValue.FromInt(prog.Nivel());

            // Metadata - existe(className) or existe(className, name)
            case "existe":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(prog.Existe(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromInt(prog.Existe(args[0].AsString()));
                return RuntimeValue.FromInt(0);

            // Variable info - all take (className, varName)
            case "varcomum":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(prog.VarComum(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromInt(0);
            case "varsav":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(prog.VarSav(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromInt(0);
            case "varnum":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(prog.VarNum(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromInt(0);
            case "vartexto":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(prog.VarTexto(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromInt(0);
            case "vartipo":
                if (args.Length >= 2)
                    return RuntimeValue.FromString(prog.VarTipo(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromString("");
            case "varlugar":
                if (args.Length >= 2)
                    return RuntimeValue.FromString(prog.VarLugar(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromString("");
            case "varvetor":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(prog.VarVetor(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromInt(0);
            case "const":
                if (args.Length >= 2)
                    return RuntimeValue.FromString(prog.Const(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromString("");

            // Modification - all take className as first arg
            case "criar":
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(prog.Criar(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromBool(prog.Criar(args[0].AsString()));
                return RuntimeValue.FromBool(false);
            case "apagar":
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(prog.Apagar(args[0].AsString(), args[1].AsString()));
                if (args.Length >= 1)
                    return RuntimeValue.FromBool(prog.Apagar(args[0].AsString()));
                return RuntimeValue.FromBool(false);
            case "apagarlin":
                if (args.Length >= 3)
                    return RuntimeValue.FromBool(prog.ApagarLin(args[0].AsString(), args[1].AsString(), (int)args[2].AsInt()));
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(prog.ApagarLin(args[0].AsString(), (int)args[1].AsInt()));
                return RuntimeValue.FromBool(false);
            case "criarlin":
                if (args.Length >= 4)
                    return RuntimeValue.FromBool(prog.CriarLin(args[0].AsString(), args[1].AsString(), (int)args[2].AsInt(), args[3].AsString()));
                if (args.Length >= 3)
                    return RuntimeValue.FromBool(prog.CriarLin(args[0].AsString(), (int)args[1].AsInt(), args[2].AsString()));
                return RuntimeValue.FromBool(false);
            case "fantes":
                if (args.Length >= 3)
                    return RuntimeValue.FromBool(prog.FAntes(args[0].AsString(), args[1].AsString(), args[2].AsString()));
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(prog.FAntes(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromBool(false);
            case "fdepois":
                if (args.Length >= 3)
                    return RuntimeValue.FromBool(prog.FDepois(args[0].AsString(), args[1].AsString(), args[2].AsString()));
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(prog.FDepois(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromBool(false);
            case "renomear":
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(prog.Renomear(args[0].AsString(), args[1].AsString()));
                return RuntimeValue.FromBool(false);
            case "salvar":
                if (args.Length > 0)
                    return RuntimeValue.FromBool(prog.Salvar(args[0].AsString()));
                return RuntimeValue.FromBool(false);
            case "salvartudo":
                return RuntimeValue.FromBool(prog.SalvarTudo());

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a ServInstance.
    /// </summary>
    private RuntimeValue CallServMethod(ServInstance serv, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "abrir":
                // abrir(address, port) - Open server
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(serv.Abrir(args[0].AsString(), (int)args[1].AsInt()));
                if (args.Length >= 1)
                    return RuntimeValue.FromBool(serv.Abrir("0.0.0.0", (int)args[0].AsInt()));
                return RuntimeValue.FromBool(false);

            case "abrirssl":
                // abrirssl(address, port) - Open SSL server
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(serv.AbrirSsl(args[0].AsString(), (int)args[1].AsInt()));
                if (args.Length >= 1)
                    return RuntimeValue.FromBool(serv.AbrirSsl("0.0.0.0", (int)args[0].AsInt()));
                return RuntimeValue.FromBool(false);

            case "fechar":
                // fechar() - Close server
                serv.Fechar();
                return RuntimeValue.Null;

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a SocketInstance.
    /// </summary>
    private RuntimeValue CallSocketMethod(SocketInstance socket, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();

        switch (lowerMethod)
        {
            case "abrir":
                // abrir(host, port) - Open connection
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(socket.Abrir(args[0].AsString(), (int)args[1].AsInt()));
                return RuntimeValue.FromBool(false);

            case "abrirssl":
                // abrirssl(host, port) - Open SSL connection
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(socket.AbrirSsl(args[0].AsString(), (int)args[1].AsInt()));
                return RuntimeValue.FromBool(false);

            case "msg":
                // msg(text) - Send message with newline
                if (args.Length > 0)
                    socket.Msg(args[0].AsString());
                return RuntimeValue.Null;

            case "msgsem":
                // msgsem(text) - Send message without newline
                if (args.Length > 0)
                    socket.MsgSem(args[0].AsString());
                return RuntimeValue.Null;

            case "fechar":
                // fechar() - Close connection
                socket.Fechar();
                return RuntimeValue.Null;

            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on a DebugInstance.
    /// </summary>
    private RuntimeValue CallDebugMethod(DebugInstance debug, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "ini":
                debug.Ini();
                return RuntimeValue.Null;
            case "exec":
                return RuntimeValue.FromInt(debug.Exec);
            case "utempo":
                return RuntimeValue.FromDouble(debug.Utempo());
            case "stempo":
                return RuntimeValue.FromDouble(debug.Stempo());
            case "mem":
                return RuntimeValue.FromDouble(debug.Mem());
            case "memmax":
                return RuntimeValue.FromDouble(debug.MemMax());
            case "func":
                return RuntimeValue.FromInt(_callStack.Count);
            case "ver":
                return RuntimeValue.FromString(debug.Ver());
            case "data":
                return RuntimeValue.FromString(debug.Data());
            case "cmd":
                // Dynamic instruction execution - complex, return empty for now
                return RuntimeValue.FromString("");
            case "passo":
                // Step-through debugging - complex, return false for now
                return RuntimeValue.Null;
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an IntTempoInstance.
    /// </summary>
    private RuntimeValue CallIntTempoMethod(IntTempoInstance intTempo, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "abs":
                return RuntimeValue.FromInt(Math.Abs(intTempo.Valor));
            case "pos":
                return RuntimeValue.FromInt(intTempo.Valor > 0 ? intTempo.Valor : -intTempo.Valor);
            case "neg":
                return RuntimeValue.FromInt(intTempo.Valor < 0 ? intTempo.Valor : -intTempo.Valor);
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Call a method on an IntExecInstance.
    /// </summary>
    private RuntimeValue CallIntExecMethod(IntExecInstance intExec, string methodName, RuntimeValue[] args)
    {
        // IntExec has no methods, only properties (value read/write)
        return RuntimeValue.Null;
    }

    /// <summary>
    /// Call a method on an IntIncInstance.
    /// </summary>
    private RuntimeValue CallIntIncMethod(IntIncInstance intInc, string methodName, RuntimeValue[] args)
    {
        var lowerMethod = methodName.ToLowerInvariant();
        switch (lowerMethod)
        {
            case "abs":
                return RuntimeValue.FromInt(Math.Abs(intInc.Valor));
            case "pos":
                return RuntimeValue.FromInt(intInc.Valor > 0 ? intInc.Valor : -intInc.Valor);
            case "neg":
                return RuntimeValue.FromInt(intInc.Valor < 0 ? intInc.Valor : -intInc.Valor);
            default:
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Create an instance of a special type (telatxt, textotxt, listaobj, etc.).
    /// Used for local variable initialization.
    /// </summary>
    private RuntimeValue CreateSpecialTypeInstance(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "telatxt" => RuntimeValue.FromObject(new TelaTxtInstance()),
            "textotxt" => RuntimeValue.FromObject(new TextoTxtInstance()),
            "textopos" => RuntimeValue.FromObject(new TextoPosInstance()),
            "listaobj" => RuntimeValue.FromObject(new ListaObjInstance()),
            "listaitem" => RuntimeValue.FromObject(new ListaItemInstance()),
            "indiceobj" => RuntimeValue.FromObject(new IndiceObjInstance()),
            "indiceitem" => RuntimeValue.FromObject(new IndiceItemInstance()),
            "inttempo" => RuntimeValue.FromObject(new IntTempoInstance()),
            "intexec" => RuntimeValue.FromObject(new IntExecInstance()),
            "intinc" => RuntimeValue.FromObject(new IntIncInstance()),
            "datahora" => RuntimeValue.FromObject(new DataHoraInstance()),
            "debug" => RuntimeValue.FromObject(new DebugInstance()),
            "arqtxt" => RuntimeValue.FromObject(new ArqTxtInstance()),
            "arqsav" => RuntimeValue.FromObject(new ArqSavInstance()),
            "serv" => RuntimeValue.FromObject(new ServInstance()),
            "socket" => RuntimeValue.FromObject(new SocketInstance()),
            _ => RuntimeValue.Null
        };
    }

    /// <summary>
    /// Get a property from a TelaTxtInstance (telatxt special type).
    /// </summary>
    private RuntimeValue GetTelaTxtProperty(TelaTxtInstance telaTxt, string propertyName)
    {
        var lowerProp = propertyName.ToLowerInvariant();

        return lowerProp switch
        {
            "proto" => RuntimeValue.FromInt(telaTxt.Proto),
            "total" => RuntimeValue.FromInt(telaTxt.Total),
            "texto" => RuntimeValue.FromString(telaTxt.Texto),
            "linha" => RuntimeValue.FromInt(telaTxt.Linha),
            "posx" => RuntimeValue.FromInt(telaTxt.PosX),
            "isactive" => RuntimeValue.FromBool(telaTxt.IsActive),
            _ => RuntimeValue.Null
        };
    }

    /// <summary>
    /// Set a property on a TelaTxtInstance (telatxt special type).
    /// </summary>
    private void SetTelaTxtProperty(TelaTxtInstance telaTxt, string propertyName, RuntimeValue value)
    {
        var lowerProp = propertyName.ToLowerInvariant();

        switch (lowerProp)
        {
            case "total":
                telaTxt.Total = (int)value.AsInt();
                break;
            case "texto":
                telaTxt.Texto = value.AsString();
                break;
            case "linha":
                telaTxt.Linha = (int)value.AsInt();
                break;
            case "isactive":
                telaTxt.IsActive = value.IsTruthy;
                break;
            // proto and posx are read-only
        }
    }

    /// <summary>
    /// Execute a function with a 'this' reference (for object methods).
    /// Uses the object's class unit for the string pool.
    /// </summary>
    public RuntimeValue ExecuteFunctionWithThis(BytecodeCompiledFunction function, BytecodeRuntimeObject thisObj, RuntimeValue[] arguments)
    {
        return ExecuteFunctionWithThis(function, thisObj, thisObj.ClassUnit, arguments);
    }

    /// <summary>
    /// Execute a function with a 'this' reference (for object methods).
    /// Uses the specified defining unit for the string pool (important for inherited methods).
    /// </summary>
    public RuntimeValue ExecuteFunctionWithThis(BytecodeCompiledFunction function, BytecodeRuntimeObject thisObj, BytecodeCompiledUnit definingUnit, RuntimeValue[] arguments)
    {
        // Push call frame with 'this' object
        if (_callStack.Count >= MaxCallDepth)
        {
            throw new RuntimeException("Call stack overflow");
        }

        // Save caller's locals if this is a nested call
        RuntimeValue[]? savedLocals = null;
        int savedIp = _ip;
        if (_callStack.Count > 0)
        {
            savedLocals = new RuntimeValue[MaxLocals];
            Array.Copy(_locals, savedLocals, MaxLocals);
        }

        var frame = new CallFrame
        {
            Function = function,
            ReturnAddress = _ip,
            LocalsBase = 0,
            StackBase = _sp,
            Arguments = arguments,
            ThisObject = thisObj
        };
        _callStack.Push(frame);

        // Initialize locals for this function
        Array.Clear(_locals, 0, _locals.Length);

        // Execute bytecode
        _ip = 0;
        var bytecode = function.Bytecode;
        // Use the defining unit's string pool - important for inherited methods
        var stringPool = definingUnit.StringPool;

        try
        {
            var result = ExecuteBytecodeLoop(bytecode, stringPool, arguments, frame);

            // Restore caller's locals and IP
            if (savedLocals != null)
            {
                Array.Copy(savedLocals, _locals, MaxLocals);
                _ip = savedIp;
            }

            return result;
        }
        catch
        {
            // Restore caller's locals and IP on exception too
            if (savedLocals != null)
            {
                Array.Copy(savedLocals, _locals, MaxLocals);
                _ip = savedIp;
            }
            if (_callStack.Count > 0) _callStack.Pop();
            throw;
        }
    }

    /// <summary>
    /// Execute a static method call (classe:função with no object).
    /// In IntMUD C++, static calls execute with this=null.
    /// </summary>
    private RuntimeValue ExecuteStaticMethodCall(BytecodeCompiledFunction function, BytecodeCompiledUnit classUnit, RuntimeValue[] arguments)
    {
        // Push call frame without 'this' object
        if (_callStack.Count >= MaxCallDepth)
        {
            throw new RuntimeException("Call stack overflow");
        }

        // Save caller's locals if this is a nested call
        RuntimeValue[]? savedLocals = null;
        int savedIp = _ip;
        if (_callStack.Count > 0)
        {
            savedLocals = new RuntimeValue[MaxLocals];
            Array.Copy(_locals, savedLocals, MaxLocals);
        }

        var frame = new CallFrame
        {
            Function = function,
            ReturnAddress = _ip,
            LocalsBase = 0,
            StackBase = _sp,
            Arguments = arguments,
            ThisObject = null  // Static call - no 'this' object
        };
        _callStack.Push(frame);

        // Initialize locals for this function
        Array.Clear(_locals, 0, _locals.Length);

        // Execute bytecode
        _ip = 0;
        var bytecode = function.Bytecode;
        var stringPool = classUnit.StringPool;

        try
        {
            var result = ExecuteBytecodeLoop(bytecode, stringPool, arguments, frame);

            // Restore caller's locals and IP
            if (savedLocals != null)
            {
                Array.Copy(savedLocals, _locals, MaxLocals);
                _ip = savedIp;
            }

            return result;
        }
        catch
        {
            // Restore caller's locals and IP on exception too
            if (savedLocals != null)
            {
                Array.Copy(savedLocals, _locals, MaxLocals);
                _ip = savedIp;
            }
            if (_callStack.Count > 0) _callStack.Pop();
            throw;
        }
    }

    private RuntimeValue ExecuteBytecodeLoop(byte[] bytecode, List<string> stringPool, RuntimeValue[] arguments, CallFrame frame)
    {
        while (_ip < bytecode.Length)
        {
            var op = (BytecodeOp)bytecode[_ip++];

            switch (op)
            {
                case BytecodeOp.Nop:
                    break;

                case BytecodeOp.Pop:
                    if (_sp > frame.StackBase) _sp--;
                    break;

                case BytecodeOp.Dup:
                    if (_sp <= frame.StackBase) throw new RuntimeException("Stack underflow");
                    Push(_valueStack[_sp - 1]);
                    break;

                case BytecodeOp.Swap:
                    if (_sp - frame.StackBase < 2) throw new RuntimeException("Stack underflow");
                    (_valueStack[_sp - 1], _valueStack[_sp - 2]) = (_valueStack[_sp - 2], _valueStack[_sp - 1]);
                    break;

                case BytecodeOp.PushNull:
                    Push(RuntimeValue.Null);
                    break;

                case BytecodeOp.PushInt:
                    Push(RuntimeValue.FromInt(ReadInt32(bytecode)));
                    break;

                case BytecodeOp.PushDouble:
                    Push(RuntimeValue.FromDouble(ReadDouble(bytecode)));
                    break;

                case BytecodeOp.PushString:
                    var strIdx = ReadUInt16(bytecode);
                    Push(RuntimeValue.FromString(stringPool[strIdx]));
                    break;

                case BytecodeOp.PushTrue:
                    Push(RuntimeValue.True);
                    break;

                case BytecodeOp.PushFalse:
                    Push(RuntimeValue.False);
                    break;

                case BytecodeOp.LoadLocal:
                    var localIdx = ReadUInt16(bytecode);
                    Push(_locals[localIdx]);
                    break;

                case BytecodeOp.StoreLocal:
                    localIdx = ReadUInt16(bytecode);
                    _locals[localIdx] = Pop();
                    break;

                case BytecodeOp.LoadGlobal:
                    var globalName = stringPool[ReadUInt16(bytecode)];
                    Push(_globals.TryGetValue(globalName, out var globalVal) ? globalVal : RuntimeValue.Null);
                    break;

                case BytecodeOp.StoreGlobal:
                    globalName = stringPool[ReadUInt16(bytecode)];
                    _globals[globalName] = Pop();
                    break;

                case BytecodeOp.LoadField:
                    var fieldName = stringPool[ReadUInt16(bytecode)];
                    var obj = Pop();
                    Push(LoadField(obj, fieldName));
                    break;

                case BytecodeOp.StoreField:
                    fieldName = stringPool[ReadUInt16(bytecode)];
                    var value = Pop();
                    obj = Pop();
                    StoreField(obj, fieldName, value);
                    break;

                case BytecodeOp.LoadFieldDynamic:
                    // Field name is on stack as string
                    var dynamicFieldName = Pop().AsString();
                    obj = Pop();
                    Push(LoadField(obj, dynamicFieldName));
                    break;

                case BytecodeOp.StoreFieldDynamic:
                    // Stack: [value, object, fieldName] (fieldName at top)
                    dynamicFieldName = Pop().AsString();
                    obj = Pop();
                    value = Pop();
                    StoreField(obj, dynamicFieldName, value);
                    break;

                case BytecodeOp.LoadArg:
                    var argIdx = bytecode[_ip++];
                    Push(argIdx < arguments.Length ? arguments[argIdx] : RuntimeValue.Null);
                    break;

                case BytecodeOp.StoreArg:
                    var storeArgIdx = bytecode[_ip++];
                    if (storeArgIdx < arguments.Length)
                        arguments[storeArgIdx] = Pop();
                    else
                        Pop(); // Discard value if arg doesn't exist
                    break;

                case BytecodeOp.LoadArgCount:
                    Push(RuntimeValue.FromInt(arguments.Length));
                    break;

                case BytecodeOp.LoadThis:
                    Push(frame.ThisObject != null
                        ? RuntimeValue.FromObject(frame.ThisObject)
                        : RuntimeValue.Null);
                    break;

                case BytecodeOp.LoadIndex:
                    var index = Pop();
                    var array = Pop();
                    Push(LoadIndex(array, index));
                    break;

                case BytecodeOp.StoreIndex:
                    // Stack order: [value, array, index] (index at top)
                    index = Pop();
                    array = Pop();
                    value = Pop();
                    StoreIndex(array, index, value);
                    break;

                // Dynamic identifier operations
                case BytecodeOp.Concat:
                    // Concatenate two strings on stack
                    var str2 = Pop().AsString();
                    var str1 = Pop().AsString();
                    Push(RuntimeValue.FromString(str1 + str2));
                    break;

                case BytecodeOp.LoadDynamic:
                    // Load variable by dynamic name (name on stack)
                    var varName = Pop().AsString();
                    Push(LoadDynamicVariable(varName));
                    break;

                case BytecodeOp.StoreDynamic:
                    // Store to variable by dynamic name
                    // Stack: [name, value] (value at top)
                    value = Pop();
                    varName = Pop().AsString();
                    StoreDynamicVariable(varName, value);
                    break;

                // Arithmetic operations
                case BytecodeOp.Add:
                    var b = Pop();
                    var a = Pop();
                    Push(a + b);
                    break;

                case BytecodeOp.Sub:
                    b = Pop();
                    a = Pop();
                    Push(a - b);
                    break;

                case BytecodeOp.Mul:
                    b = Pop();
                    a = Pop();
                    Push(a * b);
                    break;

                case BytecodeOp.Div:
                    b = Pop();
                    a = Pop();
                    Push(a / b);
                    break;

                case BytecodeOp.Mod:
                    b = Pop();
                    a = Pop();
                    Push(a % b);
                    break;

                case BytecodeOp.Neg:
                    Push(-Pop());
                    break;

                case BytecodeOp.Inc:
                    Push(Pop() + RuntimeValue.One);
                    break;

                case BytecodeOp.Dec:
                    Push(Pop() - RuntimeValue.One);
                    break;

                // Bitwise operations
                case BytecodeOp.BitAnd:
                    b = Pop();
                    a = Pop();
                    Push(a & b);
                    break;

                case BytecodeOp.BitOr:
                    b = Pop();
                    a = Pop();
                    Push(a | b);
                    break;

                case BytecodeOp.BitXor:
                    b = Pop();
                    a = Pop();
                    Push(a ^ b);
                    break;

                case BytecodeOp.BitNot:
                    Push(~Pop());
                    break;

                case BytecodeOp.Shl:
                    b = Pop();
                    a = Pop();
                    Push(RuntimeValue.FromInt(a.AsInt() << (int)b.AsInt()));
                    break;

                case BytecodeOp.Shr:
                    b = Pop();
                    a = Pop();
                    Push(RuntimeValue.FromInt(a.AsInt() >> (int)b.AsInt()));
                    break;

                // Comparison operations
                case BytecodeOp.Lt:
                    b = Pop();
                    a = Pop();
                    Push(a < b);
                    break;

                case BytecodeOp.Le:
                    b = Pop();
                    a = Pop();
                    Push(a <= b);
                    break;

                case BytecodeOp.Gt:
                    b = Pop();
                    a = Pop();
                    Push(a > b);
                    break;

                case BytecodeOp.Ge:
                    b = Pop();
                    a = Pop();
                    Push(a >= b);
                    break;

                case BytecodeOp.Eq:
                    b = Pop();
                    a = Pop();
                    Push(a == b);
                    break;

                case BytecodeOp.Ne:
                    b = Pop();
                    a = Pop();
                    Push(a != b);
                    break;

                case BytecodeOp.StrictEq:
                    b = Pop();
                    a = Pop();
                    Push(RuntimeValue.FromBool(a.StrictEquals(b)));
                    break;

                case BytecodeOp.StrictNe:
                    b = Pop();
                    a = Pop();
                    Push(RuntimeValue.FromBool(!a.StrictEquals(b)));
                    break;

                // Logical operations
                case BytecodeOp.And:
                    b = Pop();
                    a = Pop();
                    Push(RuntimeValue.FromBool(a.IsTruthy && b.IsTruthy));
                    break;

                case BytecodeOp.Or:
                    b = Pop();
                    a = Pop();
                    Push(RuntimeValue.FromBool(a.IsTruthy || b.IsTruthy));
                    break;

                case BytecodeOp.Not:
                    Push(RuntimeValue.FromBool(!Pop().IsTruthy));
                    break;

                // Control flow
                case BytecodeOp.Jump:
                    var offset = ReadInt16(bytecode);
                    _ip += offset;
                    break;

                case BytecodeOp.JumpIfTrue:
                    offset = ReadInt16(bytecode);
                    if (Pop().IsTruthy) _ip += offset;
                    break;

                case BytecodeOp.JumpIfFalse:
                    offset = ReadInt16(bytecode);
                    if (!Pop().IsTruthy) _ip += offset;
                    break;

                case BytecodeOp.Return:
                    _callStack.Pop();
                    _sp = frame.StackBase;
                    return RuntimeValue.Null;

                case BytecodeOp.ReturnValue:
                    var retVal = Pop();
                    _callStack.Pop();
                    _sp = frame.StackBase;
                    return retVal;

                case BytecodeOp.CallMethod:
                    var methodName = stringPool[ReadUInt16(bytecode)];
                    var argCount = bytecode[_ip++];
                    ExecuteMethodCall(methodName, argCount);
                    break;

                case BytecodeOp.CallMethodDynamic:
                    var dynamicMethodName = Pop().AsString();
                    argCount = bytecode[_ip++];
                    ExecuteMethodCall(dynamicMethodName, argCount);
                    break;

                case BytecodeOp.CallDynamic:
                    var dynamicFuncName = Pop().AsString();
                    argCount = bytecode[_ip++];
                    ExecuteCall(dynamicFuncName, argCount);
                    break;

                case BytecodeOp.Call:
                    var funcName = stringPool[ReadUInt16(bytecode)];
                    argCount = bytecode[_ip++];
                    ExecuteCall(funcName, argCount);
                    break;

                case BytecodeOp.CallBuiltin:
                    var builtinId = ReadUInt16(bytecode);
                    argCount = bytecode[_ip++];
                    ExecuteBuiltinCall(builtinId, argCount);
                    break;

                case BytecodeOp.JumpIfNull:
                    offset = ReadInt16(bytecode);
                    if (Pop().IsNull) _ip += offset;
                    break;

                case BytecodeOp.JumpIfNotNull:
                    offset = ReadInt16(bytecode);
                    if (!Pop().IsNull) _ip += offset;
                    break;

                case BytecodeOp.Terminate:
                    throw new TerminateException();

                case BytecodeOp.Debug:
                    // Debug breakpoint - no-op in production
                    break;

                case BytecodeOp.Line:
                    // Line number marker for debugging - skip 2 bytes
                    _ip += 2;
                    break;

                case BytecodeOp.LoadClass:
                    var className = stringPool[ReadUInt16(bytecode)];
                    Push(LoadClass(className));
                    break;

                case BytecodeOp.LoadClassMember:
                    var classIdx = ReadUInt16(bytecode);
                    var memberIdx = ReadUInt16(bytecode);
                    className = stringPool[classIdx];
                    var memberName = stringPool[memberIdx];
                    Push(LoadClassMember(className, memberName));
                    break;

                case BytecodeOp.LoadClassDynamic:
                    // Class name is on stack as string
                    className = Pop().AsString();
                    Push(LoadClass(className));
                    break;

                case BytecodeOp.LoadClassMemberDynamic:
                    // Stack: [className, memberName]
                    memberName = Pop().AsString();
                    className = Pop().AsString();
                    Push(LoadClassMember(className, memberName));
                    break;

                case BytecodeOp.StoreClassMember:
                    classIdx = ReadUInt16(bytecode);
                    memberIdx = ReadUInt16(bytecode);
                    className = stringPool[classIdx];
                    memberName = stringPool[memberIdx];
                    StoreClassMember(className, memberName, Pop());
                    break;

                case BytecodeOp.StoreClassMemberDynamic:
                    // Stack: [value, className, memberName]
                    memberName = Pop().AsString();
                    className = Pop().AsString();
                    var storeValue = Pop();
                    StoreClassMember(className, memberName, storeValue);
                    break;

                case BytecodeOp.Delete:
                    Pop(); // Discard the object to delete
                    Push(RuntimeValue.Null); // Delete expression evaluates to null
                    break;

                case BytecodeOp.InitSpecialType:
                    var initTypeName = stringPool[ReadUInt16(bytecode)];
                    Push(CreateSpecialTypeInstance(initTypeName));
                    break;

                default:
                    throw new RuntimeException($"Unknown opcode: {op}");
            }
        }

        _callStack.Pop();
        return RuntimeValue.Null;
    }

    private RuntimeValue CallStringMethod(string str, string methodName, RuntimeValue[] args)
    {
        return methodName.ToLowerInvariant() switch
        {
            "tamanho" or "tam" => RuntimeValue.FromInt(str.Length),
            "maiusculo" or "mai" => RuntimeValue.FromString(str.ToUpperInvariant()),
            "minusculo" or "min" => RuntimeValue.FromString(str.ToLowerInvariant()),
            "posicao" or "pos" when args.Length > 0 => RuntimeValue.FromInt(str.IndexOf(args[0].AsString(), StringComparison.OrdinalIgnoreCase)),
            "copiar" or "sub" when args.Length >= 2 => RuntimeValue.FromString(SafeSubstring(str, (int)args[0].AsInt(), (int)args[1].AsInt())),
            "copiar" or "sub" when args.Length == 1 => RuntimeValue.FromString(SafeSubstring(str, (int)args[0].AsInt())),
            _ => RuntimeValue.Null
        };
    }

    private static string SafeSubstring(string str, int start, int length = -1)
    {
        if (string.IsNullOrEmpty(str) || start >= str.Length || start < 0) return string.Empty;
        if (length < 0) return str[start..];
        var end = Math.Min(start + length, str.Length);
        return str[start..end];
    }

    private void ExecuteBuiltinCall(int builtinId, int argCount)
    {
        // Collect arguments from stack
        var args = new RuntimeValue[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        // TODO: Implement builtin function dispatch by ID
        Push(RuntimeValue.Null);
    }

    private RuntimeValue CallBuiltinFunction(string name, RuntimeValue[] args)
    {
        // Basic builtin functions
        switch (name.ToLowerInvariant())
        {
            case "txt":
                // txt(value) - convert to string
                // txt(value, n) - get first n characters
                // txt(value, start, length) - get substring from position start with length
                if (args.Length >= 3)
                {
                    var str = args[0].AsString();
                    var start = (int)args[1].AsInt();
                    var length = (int)args[2].AsInt();
                    if (start < 0) start = 0;
                    if (start >= str.Length) return RuntimeValue.FromString("");
                    if (length <= 0) return RuntimeValue.FromString("");
                    var maxLen = str.Length - start;
                    if (length > maxLen) length = maxLen;
                    return RuntimeValue.FromString(str.Substring(start, length));
                }
                if (args.Length >= 2)
                {
                    var str = args[0].AsString();
                    var n = (int)args[1].AsInt();
                    if (n <= 0) return RuntimeValue.FromString("");
                    return RuntimeValue.FromString(str.Length <= n ? str : str[..n]);
                }
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString() : "");

            case "txt1":
                // txt1(text) - get first word (before first space)
                if (args.Length > 0)
                {
                    var str = args[0].AsString().TrimStart();
                    var spaceIdx = str.IndexOf(' ');
                    return RuntimeValue.FromString(spaceIdx >= 0 ? str[..spaceIdx] : str);
                }
                return RuntimeValue.FromString("");

            case "txt2":
                // txt2(text) - get rest after first word (after first space)
                if (args.Length > 0)
                {
                    var str = args[0].AsString().TrimStart();
                    var spaceIdx = str.IndexOf(' ');
                    if (spaceIdx >= 0 && spaceIdx + 1 < str.Length)
                        return RuntimeValue.FromString(str[(spaceIdx + 1)..].TrimStart());
                }
                return RuntimeValue.FromString("");

            case "num":
                return RuntimeValue.FromInt(args.Length > 0 ? args[0].AsInt() : 0);

            case "real":
                return RuntimeValue.FromDouble(args.Length > 0 ? args[0].AsDouble() : 0.0);

            case "tam":
            case "strlen":
                if (args.Length > 0)
                {
                    if (args[0].Type == RuntimeValueType.Array)
                        return RuntimeValue.FromInt(args[0].Length);
                    return RuntimeValue.FromInt(args[0].AsString().Length);
                }
                return RuntimeValue.Zero;

            case "nulo":
                return RuntimeValue.FromBool(args.Length > 0 && args[0].IsNull);

            case "vetor":
            case "array":
                // Create array with optional size
                var size = args.Length > 0 ? (int)args[0].AsInt() : 0;
                return RuntimeValue.CreateArray(size);

            case "escreva":
            case "escreve":
            case "print":
            case "echo":
                return ExecuteEscreva(args);

            case "escrevaln":
            case "println":
                return ExecuteEscrevaLn(args);

            case "leia":
            case "ler":
            case "read":
            case "input":
                return ExecuteLeia();

            // Math functions
            case "intabs":
            case "abs":
                return RuntimeValue.FromInt(args.Length > 0 ? Math.Abs(args[0].AsInt()) : 0);

            case "intmax":
            case "max":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(Math.Max(args[0].AsInt(), args[1].AsInt()));
                return args.Length > 0 ? RuntimeValue.FromInt(args[0].AsInt()) : RuntimeValue.Zero;

            case "intmin":
            case "min":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(Math.Min(args[0].AsInt(), args[1].AsInt()));
                return args.Length > 0 ? RuntimeValue.FromInt(args[0].AsInt()) : RuntimeValue.Zero;

            case "intdiv":
            case "div":
                if (args.Length >= 2 && args[1].AsInt() != 0)
                    return RuntimeValue.FromInt(args[0].AsInt() / args[1].AsInt());
                return RuntimeValue.Zero;

            case "intmod":
            case "mod":
                if (args.Length >= 2 && args[1].AsInt() != 0)
                    return RuntimeValue.FromInt(args[0].AsInt() % args[1].AsInt());
                return RuntimeValue.Zero;

            case "intmedia":
            case "avg":
                if (args.Length > 0)
                {
                    long sum = 0;
                    foreach (var arg in args)
                        sum += arg.AsInt();
                    return RuntimeValue.FromInt(sum / args.Length);
                }
                return RuntimeValue.Zero;

            case "intsoma":
            case "sum":
                {
                    long sum = 0;
                    foreach (var arg in args)
                        sum += arg.AsInt();
                    return RuntimeValue.FromInt(sum);
                }

            case "matsin":
            case "sin":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Sin(args[0].AsDouble()) : 0.0);

            case "matcos":
            case "cos":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Cos(args[0].AsDouble()) : 1.0);

            case "mattan":
            case "tan":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Tan(args[0].AsDouble()) : 0.0);

            case "matasin":
            case "asin":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Asin(args[0].AsDouble()) : 0.0);

            case "matacos":
            case "acos":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Acos(args[0].AsDouble()) : Math.PI / 2);

            case "matatan":
            case "atan":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Atan(args[0].AsDouble()) : 0.0);

            case "matatan2":
            case "atan2":
                return RuntimeValue.FromDouble(args.Length >= 2 ? Math.Atan2(args[0].AsDouble(), args[1].AsDouble()) : 0.0);

            case "matsqrt":
            case "sqrt":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Sqrt(args[0].AsDouble()) : 0.0);

            case "matpow":
            case "pow":
                return RuntimeValue.FromDouble(args.Length >= 2 ? Math.Pow(args[0].AsDouble(), args[1].AsDouble()) : 0.0);

            case "matlog":
            case "log":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Log(args[0].AsDouble()) : 0.0);

            case "matlog10":
            case "log10":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Log10(args[0].AsDouble()) : 0.0);

            case "matexp":
            case "exp":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Exp(args[0].AsDouble()) : 1.0);

            case "matfloor":
            case "floor":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Floor(args[0].AsDouble()) : 0.0);

            case "matceil":
            case "ceil":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Ceiling(args[0].AsDouble()) : 0.0);

            case "matround":
            case "round":
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Round(args[0].AsDouble()) : 0.0);

            case "matrand":
            case "rand":
            case "random":
                // rand(min, max) returns random int in range, rand() returns random double 0-1
                if (args.Length >= 2)
                {
                    var min = (int)args[0].AsInt();
                    var max = (int)args[1].AsInt();
                    if (min > max) (min, max) = (max, min); // Swap if reversed
                    return RuntimeValue.FromInt(_random.Next(min, max + 1)); // +1 because Next is exclusive
                }
                if (args.Length == 1)
                    return RuntimeValue.FromInt(_random.Next((int)args[0].AsInt() + 1));
                return RuntimeValue.FromDouble(_random.NextDouble());

            case "matrandint":
            case "randint":
                if (args.Length >= 2)
                {
                    var min = (int)args[0].AsInt();
                    var max = (int)args[1].AsInt();
                    if (min > max) (min, max) = (max, min); // Swap if reversed
                    return RuntimeValue.FromInt(_random.Next(min, max + 1)); // +1 because Next is exclusive
                }
                if (args.Length == 1)
                    return RuntimeValue.FromInt(_random.Next((int)args[0].AsInt() + 1));
                return RuntimeValue.FromInt(_random.Next());

            case "matpi":
            case "pi":
                return RuntimeValue.FromDouble(Math.PI);

            case "mate":
            case "e":
                return RuntimeValue.FromDouble(Math.E);

            // Text functions
            case "txtlen":
            case "len":
            case "length":
                return RuntimeValue.FromInt(args.Length > 0 ? args[0].AsString().Length : 0);

            case "txtsub":
            case "substr":
            case "substring":
                if (args.Length >= 3)
                {
                    var str = args[0].AsString();
                    var start = (int)args[1].AsInt();
                    var len = (int)args[2].AsInt();
                    if (start < 0) start = 0;
                    if (start >= str.Length) return RuntimeValue.FromString("");
                    if (start + len > str.Length) len = str.Length - start;
                    return RuntimeValue.FromString(str.Substring(start, len));
                }
                if (args.Length >= 2)
                {
                    var str = args[0].AsString();
                    var start = (int)args[1].AsInt();
                    if (start < 0) start = 0;
                    if (start >= str.Length) return RuntimeValue.FromString("");
                    return RuntimeValue.FromString(str.Substring(start));
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtmai":
            case "upper":
            case "toupper":
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString().ToUpperInvariant() : "");

            case "txtmin":
            case "lower":
            case "tolower":
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString().ToLowerInvariant() : "");

            case "txttrim":
            case "trim":
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString().Trim() : "");

            case "txtltrim":
            case "ltrim":
            case "trimleft":
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString().TrimStart() : "");

            case "txtrtrim":
            case "rtrim":
            case "trimright":
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString().TrimEnd() : "");

            case "txtpos":
            case "indexof":
            case "pos":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(args[0].AsString().IndexOf(args[1].AsString(), StringComparison.Ordinal));
                return RuntimeValue.FromInt(-1);

            case "txtlastpos":
            case "lastindexof":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(args[0].AsString().LastIndexOf(args[1].AsString(), StringComparison.Ordinal));
                return RuntimeValue.FromInt(-1);

            case "txtreplace":
            case "txttroca":
            case "replace":
                if (args.Length >= 3)
                    return RuntimeValue.FromString(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()));
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtproc":
                // txtproc(text, search) - returns position of search in text, or 0 if not found
                // IntMUD returns 1-based position or 0 if not found
                if (args.Length >= 2)
                {
                    var idx = args[0].AsString().IndexOf(args[1].AsString(), StringComparison.OrdinalIgnoreCase);
                    return RuntimeValue.FromInt(idx >= 0 ? idx + 1 : 0);
                }
                return RuntimeValue.Zero;

            case "txtnulo":
            case "txtnulo2":
                // txtnulo(value) - returns "" if value is null/empty, otherwise the string value
                if (args.Length > 0 && !args[0].IsNull)
                {
                    var str = args[0].AsString();
                    return RuntimeValue.FromString(string.IsNullOrEmpty(str) ? "" : str);
                }
                return RuntimeValue.FromString("");

            case "txtremove":
                // txtremove(text, chars) - removes all occurrences of chars from text
                if (args.Length >= 2)
                {
                    var text = args[0].AsString();
                    var charsToRemove = args[1].AsString();
                    foreach (var c in charsToRemove)
                    {
                        text = text.Replace(c.ToString(), "");
                    }
                    return RuntimeValue.FromString(text);
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtmaimin":
                // txtmaimin(text) - alternate caps: HeLlO wOrLd
                if (args.Length > 0)
                {
                    var text = args[0].AsString();
                    var result = new char[text.Length];
                    bool upper = true;
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (char.IsLetter(text[i]))
                        {
                            result[i] = upper ? char.ToUpper(text[i]) : char.ToLower(text[i]);
                            upper = !upper;
                        }
                        else
                        {
                            result[i] = text[i];
                        }
                    }
                    return RuntimeValue.FromString(new string(result));
                }
                return RuntimeValue.FromString("");

            case "txtmaiini":
                // txtmaiini(text) - capitalize first letter of each word
                if (args.Length > 0)
                {
                    var text = args[0].AsString();
                    if (string.IsNullOrEmpty(text)) return RuntimeValue.FromString("");
                    var words = text.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (words[i].Length > 0)
                            words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i][1..].ToLower() : "");
                    }
                    return RuntimeValue.FromString(string.Join(" ", words));
                }
                return RuntimeValue.FromString("");

            case "txtconv":
                // txtconv(text, from, to) - convert characters from one set to another
                if (args.Length >= 3)
                {
                    var text = args[0].AsString();
                    var fromChars = args[1].AsString();
                    var toChars = args[2].AsString();
                    var result = new char[text.Length];
                    for (int i = 0; i < text.Length; i++)
                    {
                        var idx = fromChars.IndexOf(text[i]);
                        result[i] = idx >= 0 && idx < toChars.Length ? toChars[idx] : text[i];
                    }
                    return RuntimeValue.FromString(new string(result));
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txthex":
                // txthex(number) - convert number to hexadecimal string
                if (args.Length > 0)
                    return RuntimeValue.FromString(args[0].AsInt().ToString("X"));
                return RuntimeValue.FromString("0");

            case "txtdec":
                // txtdec(hex_string) - convert hexadecimal string to number
                if (args.Length > 0)
                {
                    var hex = args[0].AsString().TrimStart('0', 'x', 'X');
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var num))
                        return RuntimeValue.FromInt(num);
                }
                return RuntimeValue.Zero;

            case "txtcod":
            case "txtchr":
                // txtcod(char) or txtchr(code) - get ASCII code of char or char from code
                if (args.Length > 0)
                {
                    if (args[0].Type == RuntimeValueType.String)
                    {
                        var s = args[0].AsString();
                        return RuntimeValue.FromInt(s.Length > 0 ? (int)s[0] : 0);
                    }
                    else
                    {
                        var code = (int)args[0].AsInt();
                        return RuntimeValue.FromString(code > 0 && code < 65536 ? ((char)code).ToString() : "");
                    }
                }
                return RuntimeValue.Zero;

            case "txtstartswith":
            case "startswith":
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(args[0].AsString().StartsWith(args[1].AsString(), StringComparison.Ordinal));
                return RuntimeValue.False;

            case "txtendswith":
            case "endswith":
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(args[0].AsString().EndsWith(args[1].AsString(), StringComparison.Ordinal));
                return RuntimeValue.False;

            case "txtcontains":
            case "contains":
                if (args.Length >= 2)
                    return RuntimeValue.FromBool(args[0].AsString().Contains(args[1].AsString(), StringComparison.Ordinal));
                return RuntimeValue.False;

            case "txtconcat":
            case "concat":
                return RuntimeValue.FromString(string.Concat(args.Select(a => a.AsString())));

            case "txtjoin":
            case "join":
                if (args.Length >= 2)
                {
                    var separator = args[0].AsString();
                    return RuntimeValue.FromString(string.Join(separator, args.Skip(1).Select(a => a.AsString())));
                }
                return RuntimeValue.FromString(string.Concat(args.Select(a => a.AsString())));

            case "txtsplit":
            case "split":
                if (args.Length >= 2)
                {
                    var parts = args[0].AsString().Split(args[1].AsString());
                    var arr = RuntimeValue.CreateArray(parts.Length);
                    for (int i = 0; i < parts.Length; i++)
                        arr.SetIndex(i, RuntimeValue.FromString(parts[i]));
                    return arr;
                }
                return RuntimeValue.CreateArray(0);

            case "txtchar":
            case "char":
            case "chr":
                if (args.Length > 0)
                    return RuntimeValue.FromString(((char)args[0].AsInt()).ToString());
                return RuntimeValue.FromString("");

            case "txtord":
            case "ord":
            case "asc":
                if (args.Length > 0)
                {
                    var str = args[0].AsString();
                    return RuntimeValue.FromInt(str.Length > 0 ? str[0] : 0);
                }
                return RuntimeValue.Zero;

            case "txtrepeat":
            case "repeat":
                if (args.Length >= 2)
                {
                    var str = args[0].AsString();
                    var count = (int)args[1].AsInt();
                    if (count <= 0) return RuntimeValue.FromString("");
                    return RuntimeValue.FromString(string.Concat(Enumerable.Repeat(str, count)));
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtreverse":
            case "reverse":
                if (args.Length > 0)
                {
                    var chars = args[0].AsString().ToCharArray();
                    Array.Reverse(chars);
                    return RuntimeValue.FromString(new string(chars));
                }
                return RuntimeValue.FromString("");

            case "txtpadleft":
            case "padleft":
            case "lpad":
                if (args.Length >= 2)
                {
                    var str = args[0].AsString();
                    var totalWidth = (int)args[1].AsInt();
                    var padChar = args.Length >= 3 && args[2].AsString().Length > 0 ? args[2].AsString()[0] : ' ';
                    return RuntimeValue.FromString(str.PadLeft(totalWidth, padChar));
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtpadright":
            case "padright":
            case "rpad":
                if (args.Length >= 2)
                {
                    var str = args[0].AsString();
                    var totalWidth = (int)args[1].AsInt();
                    var padChar = args.Length >= 3 && args[2].AsString().Length > 0 ? args[2].AsString()[0] : ' ';
                    return RuntimeValue.FromString(str.PadRight(totalWidth, padChar));
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            // Array functions
            case "arrlen":
            case "arrlength":
            case "count":
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Array)
                    return RuntimeValue.FromInt(args[0].Length);
                return RuntimeValue.Zero;

            case "arrpush":
            case "push":
            case "inserir":
                if (args.Length >= 2 && args[0].Type == RuntimeValueType.Array)
                {
                    args[0].ArrayPush(args[1]);
                    return RuntimeValue.FromInt(args[0].Length);
                }
                return RuntimeValue.Zero;

            case "arrpop":
            case "pop":
            case "remover":
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Array && args[0].Length > 0)
                    return args[0].ArrayPop();
                return RuntimeValue.Null;

            case "arrshift":
            case "shift":
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Array && args[0].Length > 0)
                    return args[0].ArrayShift();
                return RuntimeValue.Null;

            case "arrunshift":
            case "unshift":
                if (args.Length >= 2 && args[0].Type == RuntimeValueType.Array)
                {
                    args[0].ArrayUnshift(args[1]);
                    return RuntimeValue.FromInt(args[0].Length);
                }
                return RuntimeValue.Zero;

            case "arrindexof":
                if (args.Length >= 2 && args[0].Type == RuntimeValueType.Array)
                {
                    var arr = args[0];
                    var searchVal = args[1];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr.GetIndex(i).Equals(searchVal))
                            return RuntimeValue.FromInt(i);
                    }
                    return RuntimeValue.FromInt(-1);
                }
                return RuntimeValue.FromInt(-1);

            case "arrcontains":
                if (args.Length >= 2 && args[0].Type == RuntimeValueType.Array)
                {
                    var arr = args[0];
                    var searchVal = args[1];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr.GetIndex(i).Equals(searchVal))
                            return RuntimeValue.True;
                    }
                    return RuntimeValue.False;
                }
                return RuntimeValue.False;

            case "arrclear":
            case "clear":
            case "limpar":
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Array)
                {
                    args[0].ArrayClear();
                    return RuntimeValue.Zero;
                }
                return RuntimeValue.Zero;

            case "arrreverse":
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Array)
                {
                    args[0].ArrayReverse();
                    return args[0];
                }
                return RuntimeValue.Null;

            // Type checking functions
            case "isnull":
            case "enulo":
                return RuntimeValue.FromBool(args.Length > 0 && args[0].IsNull);

            case "isnum":
            case "enumero":
                return RuntimeValue.FromBool(args.Length > 0 && (args[0].Type == RuntimeValueType.Integer || args[0].Type == RuntimeValueType.Double));

            case "istext":
            case "etexto":
                return RuntimeValue.FromBool(args.Length > 0 && args[0].Type == RuntimeValueType.String);

            case "isarray":
            case "evetor":
                return RuntimeValue.FromBool(args.Length > 0 && args[0].Type == RuntimeValueType.Array);

            case "isobject":
            case "eobjeto":
                return RuntimeValue.FromBool(args.Length > 0 && args[0].Type == RuntimeValueType.Object);

            case "typeof":
            case "tipode":
                if (args.Length > 0)
                {
                    return RuntimeValue.FromString(args[0].Type switch
                    {
                        RuntimeValueType.Null => "null",
                        RuntimeValueType.Integer => "int",
                        RuntimeValueType.Double => "real",
                        RuntimeValueType.String => "txt",
                        RuntimeValueType.Array => "vetor",
                        RuntimeValueType.Object => "objeto",
                        RuntimeValueType.Boolean => "bool",
                        _ => "desconhecido"
                    });
                }
                return RuntimeValue.FromString("null");

            case "criar":
            case "create":
            case "new":
                // criar(className, args...) - create a new instance of a class
                if (args.Length > 0)
                {
                    var classNameArg = args[0].AsString();
                    var ctorArgs = args.Length > 1 ? args[1..] : Array.Empty<RuntimeValue>();
                    return CreateObject(classNameArg, ctorArgs);
                }
                return RuntimeValue.Null;

            case "apagar":
            case "delete":
                // apagar(object) - call 'fim' destructor then remove from registry
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object)
                {
                    var objToDelete = args[0].AsObject() as BytecodeRuntimeObject;
                    if (objToDelete != null)
                    {
                        // Call destructor 'fim' if it exists (like C++ apagar → fim)
                        var (destructor, destructorUnit) = objToDelete.GetMethodWithUnit("fim");
                        if (destructor != null && destructorUnit != null)
                        {
                            try
                            {
                                ExecuteFunctionWithThis(destructor, objToDelete, destructorUnit, Array.Empty<RuntimeValue>());
                            }
                            catch { /* Ignore errors in destructor */ }
                        }
                        GlobalObjectRegistry.Unregister(objToDelete);
                    }
                }
                return RuntimeValue.Null;

            case "ref":
                // ref(object) - returns reference to object, or null
                return args.Length > 0 ? args[0] : RuntimeValue.Null;

            // Additional text functions from original IntMUD
            case "txtsublin":
                // txtsublin(text, lineNum) - get specific line from text (1-based)
                if (args.Length >= 2)
                {
                    var lines = args[0].AsString().Split('\n');
                    var lineNum = (int)args[1].AsInt() - 1; // Convert to 0-based
                    if (lineNum >= 0 && lineNum < lines.Length)
                        return RuntimeValue.FromString(lines[lineNum].TrimEnd('\r'));
                }
                return RuntimeValue.FromString("");

            case "txtfim":
                // txtfim(text, n) - get last n characters
                if (args.Length >= 2)
                {
                    var str = args[0].AsString();
                    var n = (int)args[1].AsInt();
                    if (n <= 0) return RuntimeValue.FromString("");
                    if (n >= str.Length) return RuntimeValue.FromString(str);
                    return RuntimeValue.FromString(str[^n..]);
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtcor":
                // txtcor(text, color) - return text with color codes (pass-through for now)
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txte":
                // txte(text, search) - check if text contains search (returns 1 or 0)
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(args[0].AsString().Contains(args[1].AsString(), StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                return RuntimeValue.Zero;

            case "txts":
                // txts(text, search) - search text, return position (1-based) or 0
                if (args.Length >= 2)
                {
                    var idx = args[0].AsString().IndexOf(args[1].AsString(), StringComparison.OrdinalIgnoreCase);
                    return RuntimeValue.FromInt(idx >= 0 ? idx + 1 : 0);
                }
                return RuntimeValue.Zero;

            case "txtrev":
            case "txtreverso":
                // txtrev(text) - reverse text
                if (args.Length > 0)
                {
                    var chars = args[0].AsString().ToCharArray();
                    Array.Reverse(chars);
                    return RuntimeValue.FromString(new string(chars));
                }
                return RuntimeValue.FromString("");

            case "txtcopiamai":
                // txtcopiamai(text) - copy and uppercase
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString().ToUpperInvariant() : "");

            case "txtrepete":
                // txtrepete(text, n) - repeat text n times
                if (args.Length >= 2)
                {
                    var str = args[0].AsString();
                    var count = (int)args[1].AsInt();
                    if (count <= 0) return RuntimeValue.FromString("");
                    return RuntimeValue.FromString(string.Concat(Enumerable.Repeat(str, count)));
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtnum":
                // txtnum(number, width) - format number with leading zeros
                if (args.Length >= 2)
                {
                    var num = args[0].AsInt();
                    var width = (int)args[1].AsInt();
                    return RuntimeValue.FromString(num.ToString().PadLeft(width, '0'));
                }
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsInt().ToString()) : RuntimeValue.FromString("0");

            case "txtprocmai":
                // txtprocmai(text, search) - case-insensitive search, returns 1-based position
                if (args.Length >= 2)
                {
                    var idx = args[0].AsString().IndexOf(args[1].AsString(), StringComparison.OrdinalIgnoreCase);
                    return RuntimeValue.FromInt(idx >= 0 ? idx + 1 : 0);
                }
                return RuntimeValue.Zero;

            case "txtprocdif":
                // txtprocdif(text, search) - case-sensitive search, returns 1-based position
                if (args.Length >= 2)
                {
                    var idx = args[0].AsString().IndexOf(args[1].AsString(), StringComparison.Ordinal);
                    return RuntimeValue.FromInt(idx >= 0 ? idx + 1 : 0);
                }
                return RuntimeValue.Zero;

            case "txtproclin":
                // txtproclin(text, lineSearch) - search for line containing text
                if (args.Length >= 2)
                {
                    var lines = args[0].AsString().Split('\n');
                    var search = args[1].AsString();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(search, StringComparison.OrdinalIgnoreCase))
                            return RuntimeValue.FromInt(i + 1); // 1-based
                    }
                }
                return RuntimeValue.Zero;

            case "txttrocamai":
                // txttrocamai(text, search, replace) - case-insensitive replace
                if (args.Length >= 3)
                    return RuntimeValue.FromString(args[0].AsString().Replace(args[1].AsString(), args[2].AsString(), StringComparison.OrdinalIgnoreCase));
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txttrocadif":
                // txttrocadif(text, search, replace) - case-sensitive replace
                if (args.Length >= 3)
                    return RuntimeValue.FromString(args[0].AsString().Replace(args[1].AsString(), args[2].AsString(), StringComparison.Ordinal));
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

            case "txtsepara":
                // txtsepara(text, separator) - split text into array
                if (args.Length >= 2)
                {
                    var parts = args[0].AsString().Split(args[1].AsString());
                    var arr = RuntimeValue.CreateArray(parts.Length);
                    for (int i = 0; i < parts.Length; i++)
                        arr.SetIndex(i, RuntimeValue.FromString(parts[i]));
                    return arr;
                }
                return RuntimeValue.CreateArray(0);

            case "txtbit":
                // txtbit(value, bit) - check if bit is set
                if (args.Length >= 2)
                {
                    var val = args[0].AsInt();
                    var bit = (int)args[1].AsInt();
                    return RuntimeValue.FromInt((val & (1L << bit)) != 0 ? 1 : 0);
                }
                return RuntimeValue.Zero;

            case "txtbith":
                // txtbith(value, bit) - set bit
                if (args.Length >= 2)
                {
                    var val = args[0].AsInt();
                    var bit = (int)args[1].AsInt();
                    return RuntimeValue.FromInt(val | (1L << bit));
                }
                return args.Length > 0 ? RuntimeValue.FromInt(args[0].AsInt()) : RuntimeValue.Zero;

            // Additional math functions from original IntMUD
            case "intpos":
                // intpos(text, search) - alias for txtproc
                if (args.Length >= 2)
                {
                    var idx = args[0].AsString().IndexOf(args[1].AsString(), StringComparison.OrdinalIgnoreCase);
                    return RuntimeValue.FromInt(idx >= 0 ? idx + 1 : 0);
                }
                return RuntimeValue.Zero;

            case "intsub":
                // intsub(text, start, len) - substring and convert to int
                if (args.Length >= 3)
                {
                    var str = args[0].AsString();
                    var start = (int)args[1].AsInt();
                    var len = (int)args[2].AsInt();
                    if (start < 0) start = 0;
                    if (start >= str.Length) return RuntimeValue.Zero;
                    if (start + len > str.Length) len = str.Length - start;
                    var sub = str.Substring(start, len);
                    return RuntimeValue.FromInt(long.TryParse(sub, out var num) ? num : 0);
                }
                return RuntimeValue.Zero;

            case "intsublin":
                // intsublin(text, lineNum) - get line and convert to int
                if (args.Length >= 2)
                {
                    var lines = args[0].AsString().Split('\n');
                    var lineNum = (int)args[1].AsInt() - 1;
                    if (lineNum >= 0 && lineNum < lines.Length)
                    {
                        var line = lines[lineNum].Trim();
                        return RuntimeValue.FromInt(long.TryParse(line, out var num) ? num : 0);
                    }
                }
                return RuntimeValue.Zero;

            case "intchr":
                // intchr(char) - get ASCII code of character
                if (args.Length > 0)
                {
                    var str = args[0].AsString();
                    return RuntimeValue.FromInt(str.Length > 0 ? (int)str[0] : 0);
                }
                return RuntimeValue.Zero;

            case "inttotal":
                // inttotal(array) - sum all elements in array
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Array)
                {
                    long total = 0;
                    for (int i = 0; i < args[0].Length; i++)
                        total += args[0].GetIndex(i).AsInt();
                    return RuntimeValue.FromInt(total);
                }
                return RuntimeValue.Zero;

            case "intbit":
                // intbit(value, bit) - check if bit is set
                if (args.Length >= 2)
                {
                    var val = args[0].AsInt();
                    var bit = (int)args[1].AsInt();
                    return RuntimeValue.FromInt((val & (1L << bit)) != 0 ? 1 : 0);
                }
                return RuntimeValue.Zero;

            case "intbith":
                // intbith(value, bit) - set bit
                if (args.Length >= 2)
                {
                    var val = args[0].AsInt();
                    var bit = (int)args[1].AsInt();
                    return RuntimeValue.FromInt(val | (1L << bit));
                }
                return args.Length > 0 ? RuntimeValue.FromInt(args[0].AsInt()) : RuntimeValue.Zero;

            case "matrad":
                // matrad(degrees) - convert degrees to radians
                return RuntimeValue.FromDouble(args.Length > 0 ? args[0].AsDouble() * Math.PI / 180.0 : 0.0);

            case "matdeg":
                // matdeg(radians) - convert radians to degrees
                return RuntimeValue.FromDouble(args.Length > 0 ? args[0].AsDouble() * 180.0 / Math.PI : 0.0);

            case "matraiz":
                // matraiz(value) - square root (alias for sqrt)
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Sqrt(args[0].AsDouble()) : 0.0);

            case "matcima":
                // matcima(value) - ceiling (alias for ceil)
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Ceiling(args[0].AsDouble()) : 0.0);

            case "matbaixo":
                // matbaixo(value) - floor (alias for floor)
                return RuntimeValue.FromDouble(args.Length > 0 ? Math.Floor(args[0].AsDouble()) : 0.0);

            case "mathpow":
                // mathpow(base, exp) - power (alias for pow)
                return RuntimeValue.FromDouble(args.Length >= 2 ? Math.Pow(args[0].AsDouble(), args[1].AsDouble()) : 0.0);

            // Object navigation functions
            case "objantes":
                // objantes(obj) - get previous object in per-class linked list
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object && args[0].AsObject() is BytecodeRuntimeObject prevTarget)
                    return prevTarget.PreviousObject != null ? RuntimeValue.FromObject(prevTarget.PreviousObject) : RuntimeValue.Null;
                return RuntimeValue.Null;

            case "objdepois":
                // objdepois(obj) - get next object in per-class linked list
                if (args.Length > 0 && args[0].Type == RuntimeValueType.Object && args[0].AsObject() is BytecodeRuntimeObject nextTarget)
                    return nextTarget.NextObject != null ? RuntimeValue.FromObject(nextTarget.NextObject) : RuntimeValue.Null;
                return RuntimeValue.Null;

            // Variable exchange functions
            case "vartroca":
                return ExecuteVarTroca(args, encoded: false);

            case "vartrocacod":
                return ExecuteVarTroca(args, encoded: true);

            // Args function
            case "args":
                // args() - return array of all arguments
                {
                    if (_callStack.Count > 0)
                    {
                        var currentFrame = _callStack.Peek();
                        if (currentFrame.Arguments != null)
                        {
                            var arr = RuntimeValue.CreateArray(currentFrame.Arguments.Length);
                            for (int i = 0; i < currentFrame.Arguments.Length; i++)
                                arr.SetIndex(i, currentFrame.Arguments[i]);
                            return arr;
                        }
                    }
                    return RuntimeValue.CreateArray(0);
                }

            // String formatting
            case "formato":
            case "format":
                // formato(format, args...) - string formatting
                if (args.Length > 0)
                {
                    var fmt = args[0].AsString();
                    var fmtArgs = args.Skip(1).Select(a => (object)a.AsString()).ToArray();
                    try
                    {
                        return RuntimeValue.FromString(string.Format(fmt, fmtArgs));
                    }
                    catch
                    {
                        return RuntimeValue.FromString(fmt);
                    }
                }
                return RuntimeValue.FromString("");

            // Time functions
            case "tempo":
            case "time":
                // tempo() - current timestamp in seconds
                return RuntimeValue.FromInt(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            case "tempoms":
            case "timems":
                // tempoms() - current timestamp in milliseconds
                return RuntimeValue.FromInt(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            case "data":
            case "date":
                // data() - current date as string
                return RuntimeValue.FromString(DateTime.Now.ToString("yyyy-MM-dd"));

            case "hora":
            case "hour":
                // hora() - current hour (0-23)
                return RuntimeValue.FromInt(DateTime.Now.Hour);

            case "minuto":
            case "minute":
                // minuto() - current minute (0-59)
                return RuntimeValue.FromInt(DateTime.Now.Minute);

            case "segundo":
            case "second":
                // segundo() - current second (0-59)
                return RuntimeValue.FromInt(DateTime.Now.Second);

            case "dia":
            case "day":
                // dia() - current day of month (1-31)
                return RuntimeValue.FromInt(DateTime.Now.Day);

            case "mes":
            case "month":
                // mes() - current month (1-12)
                return RuntimeValue.FromInt(DateTime.Now.Month);

            case "ano":
            case "year":
                // ano() - current year
                return RuntimeValue.FromInt(DateTime.Now.Year);

            case "diasemana":
            case "weekday":
                // diasemana() - day of week (0=Sunday, 6=Saturday)
                return RuntimeValue.FromInt((int)DateTime.Now.DayOfWeek);

            default:
                // Unknown function - return null
                return RuntimeValue.Null;
        }
    }

    /// <summary>
    /// Character normalization table for vartroca pattern matching.
    /// Matches C++ TABELA_COMPARAVAR (tabNOMES2): lowercase, accent-normalize,
    /// underscore→space, unknown chars→themselves.
    /// </summary>
    private static readonly char[] VarTrocaNormTable = BuildVarTrocaNormTable();

    private static char[] BuildVarTrocaNormTable()
    {
        var table = new char[256];
        // Initialize with identity mapping
        for (int i = 0; i < 256; i++)
            table[i] = (char)i;
        // Uppercase → lowercase
        for (int c = 'A'; c <= 'Z'; c++)
            table[c] = (char)(c + 32);
        // Underscore → space (matches tabNOMES2)
        table['_'] = ' ';
        table[' '] = ' ';
        // @ stays as @
        table['@'] = '@';
        // Accented characters → base letter (Latin-1)
        table[0xC0] = 'a'; table[0xC1] = 'a'; table[0xC2] = 'a'; table[0xC3] = 'a'; // ÀÁÂÃ
        table[0xC4] = 'a'; table[0xC5] = 'a'; // ÄÅ
        table[0xC7] = (char)0xE7; // Ç → ç
        table[0xC8] = 'e'; table[0xC9] = 'e'; table[0xCA] = 'e'; table[0xCB] = 'e'; // ÈÉÊË
        table[0xCC] = 'i'; table[0xCD] = 'i'; table[0xCE] = 'i'; table[0xCF] = 'i'; // ÌÍÎÏ
        table[0xD2] = 'o'; table[0xD3] = 'o'; table[0xD4] = 'o'; table[0xD5] = 'o'; // ÒÓÔÕ
        table[0xD6] = 'o'; // Ö
        table[0xD9] = 'u'; table[0xDA] = 'u'; table[0xDB] = 'u'; table[0xDC] = 'u'; // ÙÚÛÜ
        table[0xE0] = 'a'; table[0xE1] = 'a'; table[0xE2] = 'a'; table[0xE3] = 'a'; // àáâã
        table[0xE4] = 'a'; table[0xE5] = 'a'; // äå
        table[0xE8] = 'e'; table[0xE9] = 'e'; table[0xEA] = 'e'; table[0xEB] = 'e'; // èéêë
        table[0xEC] = 'i'; table[0xED] = 'i'; table[0xEE] = 'i'; table[0xEF] = 'i'; // ìíîï
        table[0xF2] = 'o'; table[0xF3] = 'o'; table[0xF4] = 'o'; table[0xF5] = 'o'; // òóôõ
        table[0xF6] = 'o'; // ö
        table[0xF9] = 'u'; table[0xFA] = 'u'; table[0xFB] = 'u'; table[0xFC] = 'u'; // ùúûü
        return table;
    }

    private static char NormChar(char c)
    {
        return c < 256 ? VarTrocaNormTable[c] : c;
    }

    /// <summary>
    /// vartroca(text, pattern, var_prefix, probability, spacing) - C++ compatible variable substitution.
    /// Scans text for pattern prefix, then matches characters after prefix against sorted variable/function
    /// names from the current object's class hierarchy, replacing with their values.
    /// </summary>
    private RuntimeValue ExecuteVarTroca(RuntimeValue[] args, bool encoded)
    {
        if (args.Length < 3)
            return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

        // Get the current object from the call stack
        BytecodeRuntimeObject? currentObj = null;
        if (_callStack.Count > 0)
            currentObj = _callStack.Peek().ThisObject;
        if (currentObj == null)
            return RuntimeValue.FromString(args[0].AsString());

        var text = args[0].AsString();
        var patternStr = args[1].AsString();
        var varPrefix = args[2].AsString();
        int probability = args.Length >= 4 ? (int)args[3].AsInt() : 100;
        int spacing = args.Length >= 5 ? (int)args[4].AsInt() : 0;
        if (spacing < 0) spacing = 0;

        // If probability is 0 or less, no replacements possible
        if (probability <= 0)
            return RuntimeValue.FromString(text);

        // Normalize the pattern
        var normalizedPattern = new char[patternStr.Length];
        for (int i = 0; i < patternStr.Length; i++)
            normalizedPattern[i] = NormChar(patternStr[i]);

        // Build sorted list of member names from the class hierarchy
        // Each entry has: normalized name, original name, type (var/func/const), defining unit
        var members = BuildSortedMemberList(currentObj, varPrefix);
        if (members.Count == 0)
            return RuntimeValue.FromString(text);

        // Scan text and perform replacements
        var result = new System.Text.StringBuilder(text.Length);
        int pos = 0;
        int spacingCounter = 0;
        var random = new Random();

        while (pos < text.Length)
        {
            // Try to match pattern at current position
            bool patternMatched = true;
            if (normalizedPattern.Length == 0)
            {
                // Empty pattern - always matches (every position is a candidate)
                patternMatched = true;
            }
            else
            {
                for (int i = 0; i < normalizedPattern.Length; i++)
                {
                    if (pos + i >= text.Length || NormChar(text[pos + i]) != normalizedPattern[i])
                    {
                        patternMatched = false;
                        break;
                    }
                }
            }

            if (!patternMatched)
            {
                result.Append(text[pos]);
                pos++;
                continue;
            }

            // Pattern matched - search for a matching variable name
            int afterPattern = pos + normalizedPattern.Length;
            var match = FindLongestMatch(members, text, afterPattern, encoded, probability, random);

            if (match.Index < 0 || (match.Index >= 0 && spacingCounter > 0))
            {
                // No match found, or spacing constraint active
                if (match.Index >= 0 && spacingCounter > 0)
                    spacingCounter--;

                if (normalizedPattern.Length > 0)
                {
                    result.Append(text[pos]);
                    pos++;
                }
                else
                {
                    // Empty pattern with no match - just copy char
                    result.Append(text[pos]);
                    pos++;
                }
                continue;
            }

            // Found a match - reset spacing counter
            spacingCounter = spacing;

            // Get the replacement value
            var member = members[match.Index];
            string replacement = GetMemberStringValue(currentObj, member, text, afterPattern, match.MatchLength);

            result.Append(replacement);
            pos = afterPattern + match.MatchLength;
        }

        return RuntimeValue.FromString(result.ToString());
    }

    private struct VarTrocaMember
    {
        public string NormalizedName; // For binary search comparison
        public string OriginalName;   // Original member name
        public char MemberType;       // 'v'=variable, 'f'=function, 'c'=constant
        public BytecodeCompiledUnit DefiningUnit;
    }

    private struct VarTrocaMatch
    {
        public int Index;       // Index in member list (-1 = no match)
        public int MatchLength; // How many chars in text were matched
    }

    /// <summary>
    /// Build a sorted list of members from the object's class hierarchy,
    /// filtered by the variable name prefix.
    /// </summary>
    private static List<VarTrocaMember> BuildSortedMemberList(BytecodeRuntimeObject obj, string varPrefix)
    {
        var members = new List<VarTrocaMember>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Iterate class hierarchy (most derived first)
        foreach (var unit in obj.ClassHierarchy)
        {
            // Add variables
            foreach (var variable in unit.Variables)
            {
                var nameAfterPrefix = GetNameAfterPrefix(variable.Name, varPrefix);
                if (nameAfterPrefix == null) continue;
                if (!seen.Add(variable.Name)) continue;

                members.Add(new VarTrocaMember
                {
                    NormalizedName = NormalizeString(nameAfterPrefix),
                    OriginalName = variable.Name,
                    MemberType = 'v',
                    DefiningUnit = unit
                });
            }

            // Add functions
            foreach (var (funcName, _) in unit.Functions)
            {
                var nameAfterPrefix = GetNameAfterPrefix(funcName, varPrefix);
                if (nameAfterPrefix == null) continue;
                if (!seen.Add(funcName)) continue;

                members.Add(new VarTrocaMember
                {
                    NormalizedName = NormalizeString(nameAfterPrefix),
                    OriginalName = funcName,
                    MemberType = 'f',
                    DefiningUnit = unit
                });
            }

            // Add constants
            foreach (var (constName, _) in unit.Constants)
            {
                var nameAfterPrefix = GetNameAfterPrefix(constName, varPrefix);
                if (nameAfterPrefix == null) continue;
                if (!seen.Add(constName)) continue;

                members.Add(new VarTrocaMember
                {
                    NormalizedName = NormalizeString(nameAfterPrefix),
                    OriginalName = constName,
                    MemberType = 'c',
                    DefiningUnit = unit
                });
            }
        }

        // Sort by normalized name for binary search
        members.Sort((a, b) => string.Compare(a.NormalizedName, b.NormalizedName, StringComparison.Ordinal));
        return members;
    }

    /// <summary>
    /// Get the part of the name after the prefix, or null if it doesn't match.
    /// </summary>
    private static string? GetNameAfterPrefix(string name, string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return name;

        if (name.Length < prefix.Length)
            return null;

        // Case-insensitive prefix comparison
        for (int i = 0; i < prefix.Length; i++)
        {
            if (NormChar(name[i]) != NormChar(prefix[i]))
                return null;
        }

        return name[prefix.Length..];
    }

    /// <summary>
    /// Normalize a string using the vartroca comparison table.
    /// </summary>
    private static string NormalizeString(string s)
    {
        var chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
            chars[i] = NormChar(s[i]);
        return new string(chars);
    }

    /// <summary>
    /// Find the longest matching variable name in the sorted member list.
    /// Uses binary search with progressive character matching, similar to C++ algorithm.
    /// </summary>
    private static VarTrocaMatch FindLongestMatch(
        List<VarTrocaMember> members, string text, int textPos, bool encoded,
        int probability, Random random)
    {
        if (textPos >= text.Length || members.Count == 0)
            return new VarTrocaMatch { Index = -1, MatchLength = 0 };

        int ini = 0;
        int fim = members.Count - 1;
        int bestIndex = -1;
        int bestLength = 0;
        int charOffset = 0;

        while (textPos + charOffset <= text.Length && ini <= fim)
        {
            // Get the normalized character from text at current position
            char textChar;
            if (textPos + charOffset < text.Length)
                textChar = NormChar(text[textPos + charOffset]);
            else
                break; // End of text

            if (textChar == '\0')
                break;

            // Binary search phase 1: find first member with this char at position charOffset
            int xini = -1, xfim = fim;
            {
                int lo = ini, hi = fim;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    char memberChar = charOffset < members[mid].NormalizedName.Length
                        ? members[mid].NormalizedName[charOffset]
                        : '\0';

                    if (memberChar == textChar)
                    {
                        xini = mid;
                        hi = mid - 1;
                    }
                    else if (memberChar < textChar)
                    {
                        lo = mid + 1;
                    }
                    else
                    {
                        xfim = hi = mid - 1;
                    }
                }
            }

            if (xini < 0)
                break; // No member matches at this character position

            // Binary search phase 2: find last member with this char at position charOffset
            {
                int lo = xini, hi = xfim;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    char memberChar = charOffset < members[mid].NormalizedName.Length
                        ? members[mid].NormalizedName[charOffset]
                        : '\0';

                    if (memberChar == textChar)
                    {
                        xfim = mid;
                        lo = mid + 1;
                    }
                    else if (memberChar < textChar)
                    {
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }
            }

            charOffset++;

            // Check if xini's name is a complete match (all chars consumed)
            if (charOffset == members[xini].NormalizedName.Length)
            {
                // Apply probability check
                if (probability >= 100 || random.Next(100) < probability)
                {
                    bestIndex = xini;
                    bestLength = charOffset;
                }
            }

            // Advance common characters between xini and xfim
            while (textPos + charOffset < text.Length)
            {
                if (charOffset >= members[xini].NormalizedName.Length ||
                    charOffset >= members[xfim].NormalizedName.Length)
                    break;

                char c1 = members[xini].NormalizedName[charOffset];
                char c2 = members[xfim].NormalizedName[charOffset];
                if (c1 != c2)
                    break;

                char tc = NormChar(text[textPos + charOffset]);
                if (tc != c1)
                    break;

                charOffset++;

                // Check again if xini's name is complete
                if (charOffset == members[xini].NormalizedName.Length)
                {
                    if (probability >= 100 || random.Next(100) < probability)
                    {
                        bestIndex = xini;
                        bestLength = charOffset;
                    }
                }
            }

            // Check if we should continue searching
            if (textPos + charOffset >= text.Length)
                break;
            if (charOffset >= members[xfim].NormalizedName.Length)
                break;

            ini = xini;
            fim = xfim;
        }

        return new VarTrocaMatch { Index = bestIndex, MatchLength = bestLength };
    }

    /// <summary>
    /// Get the string value of a matched member for vartroca substitution.
    /// </summary>
    private string GetMemberStringValue(BytecodeRuntimeObject obj, VarTrocaMember member,
        string text, int afterPattern, int matchLength)
    {
        switch (member.MemberType)
        {
            case 'v':
            {
                // Simple variable - get its string value
                var value = obj.GetField(member.OriginalName);
                return value.AsString();
            }
            case 'c':
            {
                // Constant - get its value
                var constant = obj.GetConstant(member.OriginalName);
                if (constant == null) return "";
                return constant.Type switch
                {
                    ConstantType.Int => constant.IntValue.ToString(),
                    ConstantType.Double => constant.DoubleValue.ToString(),
                    ConstantType.String => constant.StringValue,
                    ConstantType.Expression => EvaluateConstantExpression(obj, member, text, afterPattern, matchLength),
                    _ => ""
                };
            }
            case 'f':
            {
                // Function - call it with the matched text suffix as argument
                var suffix = text.Substring(afterPattern, matchLength);
                return CallVarTrocaFunction(obj, member.OriginalName, suffix);
            }
            default:
                return "";
        }
    }

    /// <summary>
    /// Evaluate a constant expression for vartroca.
    /// Expression constants reference functions on the object that need to be called.
    /// </summary>
    private string EvaluateConstantExpression(BytecodeRuntimeObject obj, VarTrocaMember member,
        string text, int afterPattern, int matchLength)
    {
        // For expression constants, they typically reference a function call like _tela.msg(arg0)
        // For vartroca purposes, we'll try to get the value directly from the object
        var value = obj.GetField(member.OriginalName);
        if (!value.IsNull)
            return value.AsString();
        return "";
    }

    /// <summary>
    /// Call a function on the object for vartroca substitution.
    /// The function receives the matched text suffix as an argument.
    /// </summary>
    private string CallVarTrocaFunction(BytecodeRuntimeObject obj, string funcName, string textArg)
    {
        var (function, definingUnit) = obj.GetMethodWithUnit(funcName);
        if (function == null || definingUnit == null)
            return "";

        try
        {
            var funcArgs = new RuntimeValue[] { RuntimeValue.FromString(textArg) };
            var result = CallObjectMethodDirect(obj, function, definingUnit, funcArgs);
            return result.AsString();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Call a method on a BytecodeRuntimeObject, pushing a call frame with 'this' set.
    /// Used by vartroca to invoke functions during substitution.
    /// </summary>
    private RuntimeValue CallObjectMethodDirect(BytecodeRuntimeObject obj, BytecodeCompiledFunction function,
        BytecodeCompiledUnit classUnit, RuntimeValue[] arguments)
    {
        if (_callStack.Count >= MaxCallDepth)
            throw new RuntimeException("Call stack overflow");

        RuntimeValue[]? savedLocals = null;
        int savedIp = _ip;
        if (_callStack.Count > 0)
        {
            savedLocals = new RuntimeValue[MaxLocals];
            Array.Copy(_locals, savedLocals, MaxLocals);
        }

        var frame = new CallFrame
        {
            Function = function,
            ReturnAddress = _ip,
            LocalsBase = 0,
            StackBase = _sp,
            Arguments = arguments,
            ThisObject = obj
        };
        _callStack.Push(frame);

        Array.Clear(_locals, 0, _locals.Length);
        _ip = 0;

        try
        {
            var result = ExecuteBytecodeLoop(function.Bytecode, classUnit.StringPool, arguments, frame);

            if (savedLocals != null)
            {
                Array.Copy(savedLocals, _locals, MaxLocals);
                _ip = savedIp;
            }
            return result;
        }
        catch
        {
            if (savedLocals != null)
            {
                Array.Copy(savedLocals, _locals, MaxLocals);
                _ip = savedIp;
            }
            if (_callStack.Count > 0) _callStack.Pop();
            throw;
        }
    }

    /// <summary>
    /// escreva(args...) - Write values to output without newline.
    /// </summary>
    private RuntimeValue ExecuteEscreva(RuntimeValue[] args)
    {
        var output = string.Join("", args.Select(a => a.AsString()));
        _outputBuffer.Add(output);
        WriteOutput?.Invoke(output);
        return RuntimeValue.FromInt(output.Length);
    }

    /// <summary>
    /// escrevaln(args...) - Write values to output with newline.
    /// </summary>
    private RuntimeValue ExecuteEscrevaLn(RuntimeValue[] args)
    {
        var output = string.Join("", args.Select(a => a.AsString()));
        _outputBuffer.Add(output + Environment.NewLine);
        WriteOutput?.Invoke(output + Environment.NewLine);
        return RuntimeValue.FromInt(output.Length + Environment.NewLine.Length);
    }

    /// <summary>
    /// leia() - Read a line of input.
    /// </summary>
    private RuntimeValue ExecuteLeia()
    {
        var input = ReadInput?.Invoke() ?? "";
        return RuntimeValue.FromString(input);
    }

    private RuntimeValue CreateObject(string className, RuntimeValue[] arguments)
    {
        // Look up the class unit
        BytecodeCompiledUnit? classUnit = null;

        // First check if it's the current unit
        if (string.Equals(_unit.ClassName, className, StringComparison.OrdinalIgnoreCase))
        {
            classUnit = _unit;
        }
        // Then check loaded units
        else if (_loadedUnits.TryGetValue(className, out var loaded))
        {
            classUnit = loaded;
        }

        if (classUnit == null)
        {
            throw new RuntimeException($"Class '{className}' not found");
        }

        // Gather base class units for inheritance
        var baseUnits = new List<BytecodeCompiledUnit>();
        foreach (var baseClassName in classUnit.BaseClasses)
        {
            if (_loadedUnits.TryGetValue(baseClassName, out var baseUnit))
            {
                baseUnits.Add(baseUnit);
            }
        }

        // Create the object instance
        var obj = new BytecodeRuntimeObject(classUnit, baseUnits);

        // Register the object in the global registry (for $classname syntax)
        GlobalObjectRegistry.Register(obj);

        // Call constructor if it exists (try "ini" first, then "inicializar")
        var (constructor, definingUnit) = obj.GetMethodWithUnit("ini");
        if (constructor == null)
        {
            (constructor, definingUnit) = obj.GetMethodWithUnit("inicializar");
        }

        if (constructor != null && definingUnit != null)
        {
            ExecuteFunctionWithThis(constructor, obj, definingUnit, arguments);
        }

        return RuntimeValue.FromObject(obj);
    }

    private string GetTypeName(RuntimeValue value) => value.Type switch
    {
        RuntimeValueType.Null => "nulo",
        RuntimeValueType.Integer => "int",
        RuntimeValueType.Double => "real",
        RuntimeValueType.String => "txt",
        RuntimeValueType.Object => "ref",
        RuntimeValueType.Boolean => "int",
        RuntimeValueType.Array => "vetor",
        RuntimeValueType.ClassReference => "classe",
        _ => "?"
    };

    private bool IsInstanceOf(RuntimeValue value, string className)
    {
        if (value.Type == RuntimeValueType.Object && value.AsObject() is BytecodeRuntimeObject runtimeObj)
        {
            return runtimeObj.IsInstanceOf(className);
        }
        return false;
    }

    private RuntimeValue LoadClass(string className)
    {
        // $classname returns the first object of that class
        var obj = GlobalObjectRegistry.GetFirstObject(className);
        if (obj != null)
        {
            return RuntimeValue.FromObject(obj);
        }

        // If no object exists, return a ClassReference for static method calls.
        // This allows calling classe:função even when no object of that class exists.
        // In IntMUD C++, classe:função is a static call executed with this=null.
        if (_loadedUnits.TryGetValue(className, out var unit))
        {
            return RuntimeValue.FromClassReference(unit);
        }

        return RuntimeValue.Null;
    }

    private RuntimeValue LoadClassMember(string className, string memberName)
    {
        // Get current 'this' object - used for calling parent class methods (like super() in OOP)
        var currentFrame = _callStack.Count > 0 ? _callStack.Peek() : default;
        var thisObj = currentFrame.ThisObject;

        // Try to load a constant from the class
        // First check current unit
        if (_unit.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
        {
            if (_unit.Constants.TryGetValue(memberName, out var constant))
            {
                return EvaluateConstant(constant);
            }
            // Check if it's a function - call with current 'this' object if available
            if (_unit.Functions.TryGetValue(memberName, out var function))
            {
                if (thisObj != null)
                    return ExecuteFunctionWithThis(function, thisObj, _unit, Array.Empty<RuntimeValue>());
                else
                    return ExecuteStaticMethodCall(function, _unit, Array.Empty<RuntimeValue>());
            }
        }

        // Then check loaded units (for accessing constants/functions from other classes)
        if (_loadedUnits.TryGetValue(className, out var unit))
        {
            if (unit.Constants.TryGetValue(memberName, out var constant))
            {
                return EvaluateConstant(constant);
            }
            // Check if it's a function - call with current 'this' object if available
            // This enables calling parent class methods like jogador:ini from jogolocal
            if (unit.Functions.TryGetValue(memberName, out var function))
            {
                if (thisObj != null)
                    return ExecuteFunctionWithThis(function, thisObj, unit, Array.Empty<RuntimeValue>());
                else
                    return ExecuteStaticMethodCall(function, unit, Array.Empty<RuntimeValue>());
            }
        }

        return RuntimeValue.Null;
    }

    private void StoreClassMember(string className, string memberName, RuntimeValue value)
    {
        // Store a value to a class member (static variable)
        // For now, we store in globals with a qualified name
        var qualifiedName = $"{className}:{memberName}";
        _globals[qualifiedName] = value;
    }

    /// <summary>
    /// Evaluate a constant value, handling expression constants that need runtime evaluation.
    /// </summary>
    private RuntimeValue EvaluateConstant(IntMud.Compiler.Bytecode.CompiledConstant constant)
    {
        return constant.Type switch
        {
            ConstantType.Int => RuntimeValue.FromInt(constant.IntValue),
            ConstantType.Double => RuntimeValue.FromDouble(constant.DoubleValue),
            ConstantType.String => RuntimeValue.FromString(constant.StringValue),
            ConstantType.Expression => EvaluateExpressionBytecode(constant.ExpressionBytecode!),
            _ => RuntimeValue.Null
        };
    }

    /// <summary>
    /// Execute an expression constant with a specific 'this' object and unit context.
    /// Used for evaluating iniclasse constants during class initialization.
    /// </summary>
    public RuntimeValue ExecuteExpressionConstant(
        IntMud.Compiler.Bytecode.CompiledConstant constant,
        BytecodeRuntimeObject thisObject,
        BytecodeCompiledUnit unit,
        RuntimeValue[]? arguments = null)
    {
        if (constant.Type != ConstantType.Expression || constant.ExpressionBytecode == null)
        {
            return EvaluateConstant(constant);
        }

        // Push a call frame with the 'this' object and arguments to establish context
        _callStack.Push(new CallFrame
        {
            ThisObject = thisObject,
            Arguments = arguments ?? Array.Empty<RuntimeValue>()
        });

        try
        {
            // Use the correct string pool from the defining unit
            return EvaluateExpressionBytecode(constant.ExpressionBytecode, unit.StringPool);
        }
        finally
        {
            if (_callStack.Count > 0)
                _callStack.Pop();
        }
    }

    /// <summary>
    /// Execute expression bytecode in the current context and return the result.
    /// Used for evaluating constant expressions at runtime.
    /// </summary>
    private RuntimeValue EvaluateExpressionBytecode(byte[] bytecode, List<string>? overrideStringPool = null)
    {
        // Get current frame for context (args, this, etc.)
        var frame = _callStack.Count > 0 ? _callStack.Peek() : default;
        var stringPool = overrideStringPool ?? _unit.StringPool;
        var savedIp = _ip;
        var savedSp = _sp;

        try
        {
            _ip = 0;
            while (_ip < bytecode.Length)
            {
                var op = (BytecodeOp)bytecode[_ip++];

                switch (op)
                {
                    case BytecodeOp.Nop:
                        break;

                    case BytecodeOp.Pop:
                        if (_sp > savedSp)
                            _sp--;
                        break;

                    case BytecodeOp.Dup:
                        if (_sp <= savedSp)
                            throw new RuntimeException("Stack underflow in expression");
                        Push(_valueStack[_sp - 1]);
                        break;

                    case BytecodeOp.Swap:
                        if (_sp - savedSp < 2)
                            throw new RuntimeException("Stack underflow in expression");
                        (_valueStack[_sp - 1], _valueStack[_sp - 2]) = (_valueStack[_sp - 2], _valueStack[_sp - 1]);
                        break;

                    case BytecodeOp.PushNull:
                        Push(RuntimeValue.Null);
                        break;

                    case BytecodeOp.PushInt:
                        Push(RuntimeValue.FromInt(ReadInt32Expr(bytecode)));
                        break;

                    case BytecodeOp.PushDouble:
                        Push(RuntimeValue.FromDouble(ReadDoubleExpr(bytecode)));
                        break;

                    case BytecodeOp.PushString:
                        var strIdx = ReadUInt16Expr(bytecode);
                        Push(RuntimeValue.FromString(stringPool[strIdx]));
                        break;

                    case BytecodeOp.PushTrue:
                        Push(RuntimeValue.True);
                        break;

                    case BytecodeOp.PushFalse:
                        Push(RuntimeValue.False);
                        break;

                    case BytecodeOp.LoadArg:
                        // LoadArg uses 1 byte for the argument index (matching emitter)
                        var argIdx = bytecode[_ip++];
                        if (frame.Arguments != null && argIdx < frame.Arguments.Length)
                            Push(frame.Arguments[argIdx]);
                        else
                            Push(RuntimeValue.Null);
                        break;

                    case BytecodeOp.StoreArg:
                        // StoreArg uses 1 byte for the argument index (matching emitter)
                        var storeArgIdxExpr = bytecode[_ip++];
                        if (frame.Arguments != null && storeArgIdxExpr < frame.Arguments.Length)
                            frame.Arguments[storeArgIdxExpr] = Pop();
                        else
                            Pop(); // Discard value if arg doesn't exist
                        break;

                    case BytecodeOp.LoadThis:
                        if (frame.ThisObject != null)
                            Push(RuntimeValue.FromObject(frame.ThisObject));
                        else
                            Push(RuntimeValue.Null);
                        break;

                    case BytecodeOp.LoadField:
                        {
                            var fieldName = stringPool[ReadUInt16Expr(bytecode)];
                            var obj = Pop();  // Pop the object from stack
                            var fieldValue = LoadField(obj, fieldName);
                            Push(fieldValue);
                        }
                        break;

                    case BytecodeOp.LoadFieldDynamic:
                        {
                            var dynFieldName = Pop().AsString();
                            var obj = Pop();  // Pop the object from stack
                            Push(LoadField(obj, dynFieldName));
                        }
                        break;

                    case BytecodeOp.StoreField:
                        {
                            var fieldName = stringPool[ReadUInt16Expr(bytecode)];
                            var storeValue = Pop();
                            var obj = Pop();  // Pop the object from stack
                            StoreField(obj, fieldName, storeValue);
                        }
                        break;

                    case BytecodeOp.StoreFieldDynamic:
                        {
                            var dynFieldName = Pop().AsString();
                            var obj = Pop();  // Pop the object from stack
                            var storeValue = Pop();
                            StoreField(obj, dynFieldName, storeValue);
                        }
                        break;

                    case BytecodeOp.LoadClass:
                        var classIdx = ReadUInt16Expr(bytecode);
                        var className = stringPool[classIdx];
                        Push(LoadClass(className));
                        break;

                    case BytecodeOp.LoadClassDynamic:
                        className = Pop().AsString();
                        Push(LoadClass(className));
                        break;

                    case BytecodeOp.LoadClassMember:
                        classIdx = ReadUInt16Expr(bytecode);
                        var memberIdx = ReadUInt16Expr(bytecode);
                        className = stringPool[classIdx];
                        var memberName = stringPool[memberIdx];
                        Push(LoadClassMember(className, memberName));
                        break;

                    case BytecodeOp.LoadClassMemberDynamic:
                        memberName = Pop().AsString();
                        className = Pop().AsString();
                        Push(LoadClassMember(className, memberName));
                        break;

                    // Arithmetic (using operator overloads)
                    case BytecodeOp.Add:
                        var b = Pop();
                        var a = Pop();
                        Push(a + b);
                        break;

                    case BytecodeOp.Sub:
                        b = Pop();
                        a = Pop();
                        Push(a - b);
                        break;

                    case BytecodeOp.Mul:
                        b = Pop();
                        a = Pop();
                        Push(a * b);
                        break;

                    case BytecodeOp.Div:
                        b = Pop();
                        a = Pop();
                        Push(a / b);
                        break;

                    case BytecodeOp.Mod:
                        b = Pop();
                        a = Pop();
                        Push(a % b);
                        break;

                    // Bitwise (using operator overloads)
                    case BytecodeOp.BitAnd:
                        b = Pop();
                        a = Pop();
                        Push(a & b);
                        break;

                    case BytecodeOp.BitOr:
                        b = Pop();
                        a = Pop();
                        Push(a | b);
                        break;

                    case BytecodeOp.BitXor:
                        b = Pop();
                        a = Pop();
                        Push(a ^ b);
                        break;

                    case BytecodeOp.BitNot:
                        Push(~Pop());
                        break;

                    case BytecodeOp.Shl:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.FromInt(a.AsInt() << (int)b.AsInt()));
                        break;

                    case BytecodeOp.Shr:
                        b = Pop();
                        a = Pop();
                        Push(RuntimeValue.FromInt(a.AsInt() >> (int)b.AsInt()));
                        break;

                    // Comparison (using operator overloads)
                    case BytecodeOp.Eq:
                        b = Pop();
                        a = Pop();
                        Push(a == b);
                        break;

                    case BytecodeOp.Ne:
                        b = Pop();
                        a = Pop();
                        Push(a != b);
                        break;

                    case BytecodeOp.Lt:
                        b = Pop();
                        a = Pop();
                        Push(a < b);
                        break;

                    case BytecodeOp.Le:
                        b = Pop();
                        a = Pop();
                        Push(a <= b);
                        break;

                    case BytecodeOp.Gt:
                        b = Pop();
                        a = Pop();
                        Push(a > b);
                        break;

                    case BytecodeOp.Ge:
                        b = Pop();
                        a = Pop();
                        Push(a >= b);
                        break;

                    // Logical
                    case BytecodeOp.Not:
                        Push(RuntimeValue.FromBool(!Pop().IsTruthy));
                        break;

                    case BytecodeOp.Neg:
                        Push(-Pop());
                        break;

                    // Jumps for conditional expressions (using relative offsets like main interpreter)
                    case BytecodeOp.Jump:
                        var jumpOffset = ReadInt16Expr(bytecode);
                        _ip += jumpOffset;
                        break;

                    case BytecodeOp.JumpIfFalse:
                        jumpOffset = ReadInt16Expr(bytecode);
                        if (!Pop().IsTruthy)
                            _ip += jumpOffset;
                        break;

                    case BytecodeOp.JumpIfTrue:
                        jumpOffset = ReadInt16Expr(bytecode);
                        if (Pop().IsTruthy)
                            _ip += jumpOffset;
                        break;

                    // Function calls - needed for expressions like criar(arg0)
                    // Note: We must save/restore _ip around calls because ExecuteFunction modifies _ip
                    case BytecodeOp.Call:
                        var funcName = stringPool[ReadUInt16Expr(bytecode)];
                        var argCount = bytecode[_ip++];
                        {
                            var savedExprIp = _ip;
                            ExecuteCall(funcName, argCount);
                            _ip = savedExprIp;
                        }
                        break;

                    case BytecodeOp.CallMethod:
                        var methodName = stringPool[ReadUInt16Expr(bytecode)];
                        argCount = bytecode[_ip++];
                        {
                            var savedExprIp = _ip;
                            ExecuteMethodCall(methodName, argCount);
                            _ip = savedExprIp;
                        }
                        break;

                    case BytecodeOp.CallMethodDynamic:
                        var dynamicMethodName = Pop().AsString();
                        argCount = bytecode[_ip++];
                        {
                            var savedExprIp = _ip;
                            ExecuteMethodCall(dynamicMethodName, argCount);
                            _ip = savedExprIp;
                        }
                        break;

                    case BytecodeOp.CallDynamic:
                        var dynamicFuncName = Pop().AsString();
                        argCount = bytecode[_ip++];
                        {
                            var savedExprIp = _ip;
                            ExecuteCall(dynamicFuncName, argCount);
                            _ip = savedExprIp;
                        }
                        break;

                    case BytecodeOp.CallBuiltin:
                        var builtinId = ReadUInt16Expr(bytecode);
                        argCount = bytecode[_ip++];
                        {
                            var savedExprIp = _ip;
                            ExecuteBuiltinCall(builtinId, argCount);
                            _ip = savedExprIp;
                        }
                        break;

                    case BytecodeOp.ReturnValue:
                        // Return the top of the stack
                        return _sp > savedSp ? Pop() : RuntimeValue.Null;

                    case BytecodeOp.Return:
                        return RuntimeValue.Null;

                    default:
                        throw new RuntimeException($"Unsupported opcode in constant expression: {op}");
                }
            }

            // If we reach here, return top of stack or null
            return _sp > savedSp ? Pop() : RuntimeValue.Null;
        }
        finally
        {
            _ip = savedIp;
            // Restore stack pointer if needed
            if (_sp > savedSp)
                _sp = savedSp;
        }
    }

    private int ReadInt32Expr(byte[] bytecode)
    {
        var value = BitConverter.ToInt32(bytecode, _ip);
        _ip += 4;
        return value;
    }

    private double ReadDoubleExpr(byte[] bytecode)
    {
        var value = BitConverter.ToDouble(bytecode, _ip);
        _ip += 8;
        return value;
    }

    private ushort ReadUInt16Expr(byte[] bytecode)
    {
        var value = (ushort)(bytecode[_ip] | (bytecode[_ip + 1] << 8));
        _ip += 2;
        return value;
    }

    private short ReadInt16Expr(byte[] bytecode)
    {
        var value = BitConverter.ToInt16(bytecode, _ip);
        _ip += 2;
        return value;
    }
}

/// <summary>
/// Represents a call frame on the call stack.
/// </summary>
internal struct CallFrame
{
    public BytecodeCompiledFunction Function;
    public int ReturnAddress;
    public int LocalsBase;
    public int StackBase;
    public RuntimeValue[] Arguments;
    public BytecodeRuntimeObject? ThisObject;
}

/// <summary>
/// Exception thrown during runtime execution.
/// </summary>
public sealed class RuntimeException : Exception
{
    public RuntimeException(string message) : base(message) { }
    public RuntimeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when the terminate instruction is executed.
/// </summary>
public sealed class TerminateException : Exception
{
    public TerminateException() : base("Program terminated") { }
}
