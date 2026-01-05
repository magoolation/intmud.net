using System.Buffers.Binary;
using System.Text;

namespace IntMud.Compiler.Bytecode;

/// <summary>
/// Bytecode instruction opcodes for the IntMUD virtual machine.
/// </summary>
public enum BytecodeOp : byte
{
    // Stack Operations
    Nop = 0,
    Pop = 1,
    Dup = 2,
    Swap = 3,

    // Constants
    PushNull = 10,
    PushInt = 11,       // followed by 4-byte int
    PushDouble = 12,    // followed by 8-byte double
    PushString = 13,    // followed by 2-byte string pool index
    PushTrue = 14,
    PushFalse = 15,

    // Variables
    LoadLocal = 20,     // followed by 2-byte local index
    StoreLocal = 21,    // followed by 2-byte local index
    LoadField = 22,     // followed by 2-byte string pool index (field name)
    StoreField = 23,    // followed by 2-byte string pool index (field name)
    LoadGlobal = 24,    // followed by 2-byte string pool index (var name)
    StoreGlobal = 25,   // followed by 2-byte string pool index (var name)
    LoadArg = 26,       // followed by 1-byte arg index
    LoadArgCount = 27,
    LoadThis = 28,

    // Array/Index Operations
    LoadIndex = 30,
    StoreIndex = 31,
    LoadFieldDynamic = 32,  // field name is on stack (as string)
    StoreFieldDynamic = 33, // field name and value on stack

    // Dynamic Identifier Operations
    Concat = 34,            // concatenate two strings on stack
    LoadDynamic = 35,       // variable name on stack, resolves local/field/global
    StoreDynamic = 36,      // variable name and value on stack

    // Arithmetic
    Add = 40,
    Sub = 41,
    Mul = 42,
    Div = 43,
    Mod = 44,
    Neg = 45,
    Inc = 46,
    Dec = 47,

    // Bitwise
    BitAnd = 50,
    BitOr = 51,
    BitXor = 52,
    BitNot = 53,
    Shl = 54,
    Shr = 55,

    // Comparison
    Eq = 60,
    Ne = 61,
    Lt = 62,
    Le = 63,
    Gt = 64,
    Ge = 65,
    StrictEq = 66,
    StrictNe = 67,

    // Logical
    And = 70,
    Or = 71,
    Not = 72,

    // Control Flow
    Jump = 80,          // followed by 2-byte relative offset
    JumpIfTrue = 81,    // followed by 2-byte relative offset
    JumpIfFalse = 82,   // followed by 2-byte relative offset
    JumpIfNull = 83,    // followed by 2-byte relative offset
    JumpIfNotNull = 84, // followed by 2-byte relative offset

    // Function Calls
    Call = 90,          // followed by 2-byte string pool index (function name) + 1-byte arg count
    CallMethod = 91,    // followed by 2-byte string pool index (method name) + 1-byte arg count
    CallBuiltin = 92,   // followed by 2-byte builtin function id + 1-byte arg count
    Return = 93,
    ReturnValue = 94,

    // Object Operations
    New = 100,          // followed by 2-byte string pool index (class name)
    Delete = 101,
    TypeOf = 102,
    InstanceOf = 103,   // followed by 2-byte string pool index (class name)

    // Class/Static References
    LoadClass = 110,    // followed by 2-byte string pool index (class name)
    LoadClassMember = 111, // followed by 2-byte (class) + 2-byte (member)

    // Special
    Terminate = 120,
    Debug = 121,
    Line = 122,         // followed by 2-byte line number (for debugging)
}

/// <summary>
/// Emits bytecode instructions to a buffer.
/// </summary>
public sealed class BytecodeEmitter
{
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _writer;
    private readonly List<string> _stringPool;
    private readonly Dictionary<string, int> _stringPoolIndex = new();
    private readonly Stack<List<int>> _breakTargets = new();
    private readonly Stack<List<int>> _continueTargets = new();
    private readonly List<LineInfo> _lineInfo = new();
    private int _currentLine;

    public BytecodeEmitter(List<string> stringPool)
    {
        _stringPool = stringPool;
        _writer = new BinaryWriter(_stream);
    }

    /// <summary>
    /// Current position in the bytecode stream.
    /// </summary>
    public int Position => (int)_stream.Position;

    /// <summary>
    /// Get the emitted bytecode.
    /// </summary>
    public byte[] GetBytecode() => _stream.ToArray();

    /// <summary>
    /// Get line information for debugging.
    /// </summary>
    public List<LineInfo> GetLineInfo() => _lineInfo;

    /// <summary>
    /// Add a string to the pool and return its index.
    /// </summary>
    public int AddString(string value)
    {
        if (_stringPoolIndex.TryGetValue(value, out var index))
            return index;

        index = _stringPool.Count;
        _stringPool.Add(value);
        _stringPoolIndex[value] = index;
        return index;
    }

    /// <summary>
    /// Set the current source line for debugging.
    /// </summary>
    public void SetLine(int line)
    {
        if (line != _currentLine && line > 0)
        {
            _currentLine = line;
            _lineInfo.Add(new LineInfo { Offset = Position, Line = line });
        }
    }

    // Stack Operations
    public void EmitNop() => _writer.Write((byte)BytecodeOp.Nop);
    public void EmitPop() => _writer.Write((byte)BytecodeOp.Pop);
    public void EmitDup() => _writer.Write((byte)BytecodeOp.Dup);
    public void EmitSwap() => _writer.Write((byte)BytecodeOp.Swap);

    // Constants
    public void EmitPushNull() => _writer.Write((byte)BytecodeOp.PushNull);
    public void EmitPushTrue() => _writer.Write((byte)BytecodeOp.PushTrue);
    public void EmitPushFalse() => _writer.Write((byte)BytecodeOp.PushFalse);

    public void EmitPushInt(int value)
    {
        _writer.Write((byte)BytecodeOp.PushInt);
        _writer.Write(value);
    }

    public void EmitPushDouble(double value)
    {
        _writer.Write((byte)BytecodeOp.PushDouble);
        _writer.Write(value);
    }

    public void EmitPushString(string value)
    {
        var index = AddString(value);
        _writer.Write((byte)BytecodeOp.PushString);
        _writer.Write((ushort)index);
    }

    // Variables
    public void EmitLoadLocal(int index)
    {
        _writer.Write((byte)BytecodeOp.LoadLocal);
        _writer.Write((ushort)index);
    }

    public void EmitStoreLocal(int index)
    {
        _writer.Write((byte)BytecodeOp.StoreLocal);
        _writer.Write((ushort)index);
    }

    public void EmitLoadField(string name)
    {
        var index = AddString(name);
        _writer.Write((byte)BytecodeOp.LoadField);
        _writer.Write((ushort)index);
    }

    public void EmitStoreField(string name)
    {
        var index = AddString(name);
        _writer.Write((byte)BytecodeOp.StoreField);
        _writer.Write((ushort)index);
    }

    public void EmitLoadGlobal(string name)
    {
        var index = AddString(name);
        _writer.Write((byte)BytecodeOp.LoadGlobal);
        _writer.Write((ushort)index);
    }

    public void EmitStoreGlobal(string name)
    {
        var index = AddString(name);
        _writer.Write((byte)BytecodeOp.StoreGlobal);
        _writer.Write((ushort)index);
    }

    public void EmitLoadArg(int index)
    {
        _writer.Write((byte)BytecodeOp.LoadArg);
        _writer.Write((byte)index);
    }

    public void EmitLoadArgCount() => _writer.Write((byte)BytecodeOp.LoadArgCount);
    public void EmitLoadThis() => _writer.Write((byte)BytecodeOp.LoadThis);

    // Array/Index
    public void EmitLoadIndex() => _writer.Write((byte)BytecodeOp.LoadIndex);
    public void EmitStoreIndex() => _writer.Write((byte)BytecodeOp.StoreIndex);

    // Dynamic field access (field name on stack)
    public void EmitLoadFieldDynamic() => _writer.Write((byte)BytecodeOp.LoadFieldDynamic);
    public void EmitStoreFieldDynamic() => _writer.Write((byte)BytecodeOp.StoreFieldDynamic);

    // Dynamic identifier operations
    /// <summary>
    /// Concatenate two strings on stack.
    /// Stack: [str1, str2] -> [str1 + str2]
    /// </summary>
    public void EmitConcat() => _writer.Write((byte)BytecodeOp.Concat);

    /// <summary>
    /// Load a variable by dynamic name (name on stack).
    /// Resolves in order: local -> instance field -> global.
    /// Stack: [name] -> [value]
    /// </summary>
    public void EmitLoadDynamic() => _writer.Write((byte)BytecodeOp.LoadDynamic);

    /// <summary>
    /// Store a value to a variable by dynamic name (name and value on stack).
    /// Resolves in order: local -> instance field -> global.
    /// Stack: [name, value] -> []
    /// </summary>
    public void EmitStoreDynamic() => _writer.Write((byte)BytecodeOp.StoreDynamic);

    // Arithmetic
    public void EmitAdd() => _writer.Write((byte)BytecodeOp.Add);
    public void EmitSub() => _writer.Write((byte)BytecodeOp.Sub);
    public void EmitMul() => _writer.Write((byte)BytecodeOp.Mul);
    public void EmitDiv() => _writer.Write((byte)BytecodeOp.Div);
    public void EmitMod() => _writer.Write((byte)BytecodeOp.Mod);
    public void EmitNeg() => _writer.Write((byte)BytecodeOp.Neg);
    public void EmitInc() => _writer.Write((byte)BytecodeOp.Inc);
    public void EmitDec() => _writer.Write((byte)BytecodeOp.Dec);

    // Bitwise
    public void EmitBitAnd() => _writer.Write((byte)BytecodeOp.BitAnd);
    public void EmitBitOr() => _writer.Write((byte)BytecodeOp.BitOr);
    public void EmitBitXor() => _writer.Write((byte)BytecodeOp.BitXor);
    public void EmitBitNot() => _writer.Write((byte)BytecodeOp.BitNot);
    public void EmitShl() => _writer.Write((byte)BytecodeOp.Shl);
    public void EmitShr() => _writer.Write((byte)BytecodeOp.Shr);

    // Comparison
    public void EmitEq() => _writer.Write((byte)BytecodeOp.Eq);
    public void EmitNe() => _writer.Write((byte)BytecodeOp.Ne);
    public void EmitLt() => _writer.Write((byte)BytecodeOp.Lt);
    public void EmitLe() => _writer.Write((byte)BytecodeOp.Le);
    public void EmitGt() => _writer.Write((byte)BytecodeOp.Gt);
    public void EmitGe() => _writer.Write((byte)BytecodeOp.Ge);
    public void EmitStrictEq() => _writer.Write((byte)BytecodeOp.StrictEq);
    public void EmitStrictNe() => _writer.Write((byte)BytecodeOp.StrictNe);

    // Logical
    public void EmitAnd() => _writer.Write((byte)BytecodeOp.And);
    public void EmitOr() => _writer.Write((byte)BytecodeOp.Or);
    public void EmitNot() => _writer.Write((byte)BytecodeOp.Not);

    // Control Flow
    public int EmitJump()
    {
        _writer.Write((byte)BytecodeOp.Jump);
        var pos = Position;
        _writer.Write((short)0); // placeholder
        return pos;
    }

    public int EmitJumpIfTrue()
    {
        _writer.Write((byte)BytecodeOp.JumpIfTrue);
        var pos = Position;
        _writer.Write((short)0);
        return pos;
    }

    public int EmitJumpIfFalse()
    {
        _writer.Write((byte)BytecodeOp.JumpIfFalse);
        var pos = Position;
        _writer.Write((short)0);
        return pos;
    }

    public int EmitJumpIfNull()
    {
        _writer.Write((byte)BytecodeOp.JumpIfNull);
        var pos = Position;
        _writer.Write((short)0);
        return pos;
    }

    public int EmitJumpIfNotNull()
    {
        _writer.Write((byte)BytecodeOp.JumpIfNotNull);
        var pos = Position;
        _writer.Write((short)0);
        return pos;
    }

    /// <summary>
    /// Patch a jump instruction at the given position to jump to the current position.
    /// </summary>
    public void PatchJump(int jumpPosition)
    {
        var offset = Position - jumpPosition - 2; // -2 for the offset bytes themselves
        var currentPos = _stream.Position;
        _stream.Position = jumpPosition;
        _writer.Write((short)offset);
        _stream.Position = currentPos;
    }

    /// <summary>
    /// Patch a jump instruction at the given position to jump to a specific target position.
    /// Used for backward jumps in loops.
    /// </summary>
    public void PatchJumpTo(int jumpPosition, int targetPosition)
    {
        var offset = targetPosition - jumpPosition - 2; // -2 for the offset bytes themselves
        var currentPos = _stream.Position;
        _stream.Position = jumpPosition;
        _writer.Write((short)offset);
        _stream.Position = currentPos;
    }

    /// <summary>
    /// Get the current position as a label for jump targets.
    /// </summary>
    public int DefineLabel() => Position;

    // Function Calls
    public void EmitCall(string functionName, int argCount)
    {
        var index = AddString(functionName);
        _writer.Write((byte)BytecodeOp.Call);
        _writer.Write((ushort)index);
        _writer.Write((byte)argCount);
    }

    public void EmitCallMethod(string methodName, int argCount)
    {
        var index = AddString(methodName);
        _writer.Write((byte)BytecodeOp.CallMethod);
        _writer.Write((ushort)index);
        _writer.Write((byte)argCount);
    }

    public void EmitCallBuiltin(int builtinId, int argCount)
    {
        _writer.Write((byte)BytecodeOp.CallBuiltin);
        _writer.Write((ushort)builtinId);
        _writer.Write((byte)argCount);
    }

    public void EmitReturn() => _writer.Write((byte)BytecodeOp.Return);
    public void EmitReturnValue() => _writer.Write((byte)BytecodeOp.ReturnValue);

    // Object Operations
    public void EmitNew(string className, int argCount = 0)
    {
        var index = AddString(className);
        _writer.Write((byte)BytecodeOp.New);
        _writer.Write((ushort)index);
        _writer.Write((byte)argCount);
    }

    public void EmitDelete() => _writer.Write((byte)BytecodeOp.Delete);
    public void EmitTypeOf() => _writer.Write((byte)BytecodeOp.TypeOf);

    public void EmitInstanceOf(string className)
    {
        var index = AddString(className);
        _writer.Write((byte)BytecodeOp.InstanceOf);
        _writer.Write((ushort)index);
    }

    // Class/Static References
    public void EmitLoadClass(string className)
    {
        var index = AddString(className);
        _writer.Write((byte)BytecodeOp.LoadClass);
        _writer.Write((ushort)index);
    }

    public void EmitLoadClassMember(string className, string memberName)
    {
        var classIndex = AddString(className);
        var memberIndex = AddString(memberName);
        _writer.Write((byte)BytecodeOp.LoadClassMember);
        _writer.Write((ushort)classIndex);
        _writer.Write((ushort)memberIndex);
    }

    // Special
    public void EmitTerminate() => _writer.Write((byte)BytecodeOp.Terminate);
    public void EmitDebug() => _writer.Write((byte)BytecodeOp.Debug);

    public void EmitLine(int line)
    {
        _writer.Write((byte)BytecodeOp.Line);
        _writer.Write((ushort)line);
    }

    // Break/Continue support
    public void PushLoopContext()
    {
        _breakTargets.Push(new List<int>());
        _continueTargets.Push(new List<int>());
    }

    public void PopLoopContext(int continueTarget)
    {
        var breaks = _breakTargets.Pop();
        var continues = _continueTargets.Pop();

        foreach (var pos in breaks)
        {
            PatchJump(pos);
        }

        foreach (var pos in continues)
        {
            var offset = continueTarget - pos - 2;
            var currentPos = _stream.Position;
            _stream.Position = pos;
            _writer.Write((short)offset);
            _stream.Position = currentPos;
        }
    }

    public void EmitBreak()
    {
        if (_breakTargets.Count > 0)
        {
            _breakTargets.Peek().Add(EmitJump());
        }
    }

    public void EmitContinue()
    {
        if (_continueTargets.Count > 0)
        {
            _continueTargets.Peek().Add(EmitJump());
        }
    }
}
