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
            return;
        }

        // Handle array element assignment with numeric field names (arr.0 = x, arr.[i] = x)
        if (obj.Type == RuntimeValueType.Array && int.TryParse(fieldName, out var index))
        {
            obj.SetIndex(index, value);
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

        // Try to find function in current unit
        if (_unit.Functions.TryGetValue(funcName, out var function))
        {
            // Propagate 'this' reference from current call frame if available
            var currentFrame = _callStack.Count > 0 ? _callStack.Peek() : default;
            RuntimeValue result;
            if (currentFrame.ThisObject != null)
            {
                result = ExecuteFunctionWithThis(function, currentFrame.ThisObject, args);
            }
            else
            {
                result = ExecuteFunction(function, args);
            }
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
                return RuntimeValue.FromDouble(_random.NextDouble());

            case "matrandint":
            case "randint":
                if (args.Length >= 2)
                    return RuntimeValue.FromInt(_random.Next((int)args[0].AsInt(), (int)args[1].AsInt()));
                if (args.Length == 1)
                    return RuntimeValue.FromInt(_random.Next((int)args[0].AsInt()));
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
            case "replace":
                if (args.Length >= 3)
                    return RuntimeValue.FromString(args[0].AsString().Replace(args[1].AsString(), args[2].AsString()));
                return args.Length > 0 ? RuntimeValue.FromString(args[0].AsString()) : RuntimeValue.FromString("");

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

            default:
                // Unknown function - return null
                return RuntimeValue.Null;
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
