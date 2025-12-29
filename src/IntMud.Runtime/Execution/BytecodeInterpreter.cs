using IntMud.Runtime.Values;
using BytecodeCompiledUnit = IntMud.Compiler.Bytecode.CompiledUnit;
using BytecodeCompiledFunction = IntMud.Compiler.Bytecode.CompiledFunction;
using BytecodeOp = IntMud.Compiler.Bytecode.BytecodeOp;
using ConstantType = IntMud.Compiler.Bytecode.ConstantType;

namespace IntMud.Runtime.Execution;

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

                    case BytecodeOp.LoadArg:
                        var argIdx = bytecode[_ip++];
                        Push(argIdx < arguments.Length ? arguments[argIdx] : RuntimeValue.Null);
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
                        Pop(); // Just discard for now
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
        // Handle BytecodeRuntimeObject
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is BytecodeRuntimeObject runtimeObj)
        {
            return runtimeObj.GetField(fieldName);
        }

        // Handle special built-in properties
        return fieldName.ToLowerInvariant() switch
        {
            // String properties
            "tamanho" or "tam" when obj.Type == RuntimeValueType.String => RuntimeValue.FromInt(obj.AsString().Length),
            "maiusculo" or "mai" when obj.Type == RuntimeValueType.String => RuntimeValue.FromString(obj.AsString().ToUpperInvariant()),
            "minusculo" or "min" when obj.Type == RuntimeValueType.String => RuntimeValue.FromString(obj.AsString().ToLowerInvariant()),

            // Array properties
            "tamanho" or "tam" when obj.Type == RuntimeValueType.Array => RuntimeValue.FromInt(obj.Length),

            _ => RuntimeValue.Null
        };
    }

    private void StoreField(RuntimeValue obj, string fieldName, RuntimeValue value)
    {
        // Handle BytecodeRuntimeObject
        if (obj.Type == RuntimeValueType.Object && obj.AsObject() is BytecodeRuntimeObject runtimeObj)
        {
            runtimeObj.SetField(fieldName, value);
        }
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

        // Try to find function in current unit
        if (_unit.Functions.TryGetValue(funcName, out var function))
        {
            var result = ExecuteFunction(function, args);
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

        // Method not found
        Push(RuntimeValue.Null);
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

        // Initialize locals
        Array.Clear(_locals, 0, _locals.Length);

        // Execute bytecode
        _ip = 0;
        var bytecode = function.Bytecode;
        // Use the defining unit's string pool - important for inherited methods
        var stringPool = definingUnit.StringPool;

        try
        {
            return ExecuteBytecodeLoop(bytecode, stringPool, arguments, frame);
        }
        catch
        {
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

                case BytecodeOp.LoadArg:
                    var argIdx = bytecode[_ip++];
                    Push(argIdx < arguments.Length ? arguments[argIdx] : RuntimeValue.Null);
                    break;

                case BytecodeOp.LoadArgCount:
                    Push(RuntimeValue.FromInt(arguments.Length));
                    break;

                case BytecodeOp.LoadThis:
                    Push(frame.ThisObject != null
                        ? RuntimeValue.FromObject(frame.ThisObject)
                        : RuntimeValue.Null);
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

                case BytecodeOp.Call:
                    var funcName = stringPool[ReadUInt16(bytecode)];
                    argCount = bytecode[_ip++];
                    ExecuteCall(funcName, argCount);
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
                return RuntimeValue.FromString(args.Length > 0 ? args[0].AsString() : "");

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

            default:
                // Unknown function - return null
                return RuntimeValue.Null;
        }
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

        // Call constructor if it exists
        var (constructor, definingUnit) = obj.GetMethodWithUnit("inicializar");
        if (constructor != null && definingUnit != null)
        {
            ExecuteFunctionWithThis(constructor, obj, definingUnit, arguments);
        }
        else if (arguments.Length > 0)
        {
            // If no constructor but arguments were passed, it's an error
            throw new RuntimeException($"Class '{className}' does not have a constructor but arguments were provided");
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
        _ => "?"
    };

    private bool IsInstanceOf(RuntimeValue value, string className)
    {
        // TODO: Implement instanceof check
        return false;
    }

    private RuntimeValue LoadClass(string className)
    {
        // TODO: Implement class loading
        return RuntimeValue.Null;
    }

    private RuntimeValue LoadClassMember(string className, string memberName)
    {
        // Try to load a constant from the class
        if (_unit.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
        {
            if (_unit.Constants.TryGetValue(memberName, out var constant))
            {
                return constant.Type switch
                {
                    ConstantType.Int => RuntimeValue.FromInt(constant.IntValue),
                    ConstantType.Double => RuntimeValue.FromDouble(constant.DoubleValue),
                    ConstantType.String => RuntimeValue.FromString(constant.StringValue),
                    _ => RuntimeValue.Null
                };
            }
        }

        return RuntimeValue.Null;
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
