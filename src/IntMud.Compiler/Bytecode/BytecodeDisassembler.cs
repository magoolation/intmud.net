using System.Text;

namespace IntMud.Compiler.Bytecode;

/// <summary>
/// Disassembles bytecode into human-readable format.
/// </summary>
public static class BytecodeDisassembler
{
    /// <summary>
    /// Disassemble a compiled function.
    /// </summary>
    public static string Disassemble(CompiledFunction function, IReadOnlyList<string> stringPool)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Function: {function.Name}");
        sb.AppendLine($"  Source: {function.SourceFile ?? "unknown"}:{function.StartLine}");
        sb.AppendLine($"  Locals: {function.LocalVariables.Count}");
        sb.AppendLine($"  Virtual: {function.IsVirtual}");
        sb.AppendLine($"  Bytecode size: {function.Bytecode.Length} bytes");
        sb.AppendLine();

        sb.AppendLine("Instructions:");
        DisassembleBytecode(function.Bytecode, stringPool, sb);

        return sb.ToString();
    }

    /// <summary>
    /// Disassemble a compiled unit.
    /// </summary>
    public static string Disassemble(CompiledUnit unit)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Class: {unit.ClassName}");
        sb.AppendLine($"  Source: {unit.SourceFile ?? "unknown"}");
        sb.AppendLine($"  Base classes: {string.Join(", ", unit.BaseClasses)}");
        sb.AppendLine($"  Variables: {unit.Variables.Count}");
        sb.AppendLine($"  Functions: {unit.Functions.Count}");
        sb.AppendLine($"  Constants: {unit.Constants.Count}");
        sb.AppendLine($"  String pool: {unit.StringPool.Count} entries");
        sb.AppendLine();

        if (unit.Variables.Count > 0)
        {
            sb.AppendLine("Variables:");
            foreach (var variable in unit.Variables)
            {
                var modifiers = new List<string>();
                if (variable.IsCommon) modifiers.Add("comum");
                if (variable.IsSaved) modifiers.Add("sav");
                var modStr = modifiers.Count > 0 ? $" [{string.Join(", ", modifiers)}]" : "";
                var arrayStr = variable.ArraySize > 0 ? $"[{variable.ArraySize}]" : "";
                sb.AppendLine($"  {variable.TypeName} {variable.Name}{arrayStr}{modStr} @ offset {variable.Offset}");
            }
            sb.AppendLine();
        }

        if (unit.Constants.Count > 0)
        {
            sb.AppendLine("Constants:");
            foreach (var (name, constant) in unit.Constants)
            {
                var value = constant.Type switch
                {
                    ConstantType.Int => constant.IntValue.ToString(),
                    ConstantType.Double => constant.DoubleValue.ToString(),
                    ConstantType.String => $"\"{EscapeString(constant.StringValue ?? "")}\"",
                    ConstantType.Null => "nulo",
                    _ => "?"
                };
                sb.AppendLine($"  {name} = {value}");
            }
            sb.AppendLine();
        }

        foreach (var function in unit.Functions.Values)
        {
            sb.AppendLine(Disassemble(function, unit.StringPool));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Disassemble bytecode to a string builder.
    /// </summary>
    public static void DisassembleBytecode(byte[] bytecode, IReadOnlyList<string> stringPool, StringBuilder sb)
    {
        int offset = 0;
        while (offset < bytecode.Length)
        {
            var startOffset = offset;
            var op = (BytecodeOp)bytecode[offset++];

            sb.Append($"  {startOffset:D4}: {op,-16}");

            switch (op)
            {
                case BytecodeOp.PushInt:
                    var intValue = BitConverter.ToInt32(bytecode, offset);
                    offset += 4;
                    sb.Append($" {intValue}");
                    break;

                case BytecodeOp.PushDouble:
                    var doubleValue = BitConverter.ToDouble(bytecode, offset);
                    offset += 8;
                    sb.Append($" {doubleValue}");
                    break;

                case BytecodeOp.PushString:
                    var strIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var str = strIndex < stringPool.Count ? stringPool[strIndex] : "?";
                    sb.Append($" [{strIndex}] \"{EscapeString(str)}\"");
                    break;

                case BytecodeOp.LoadLocal:
                case BytecodeOp.StoreLocal:
                    var localIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    sb.Append($" #{localIndex}");
                    break;

                case BytecodeOp.LoadField:
                case BytecodeOp.StoreField:
                case BytecodeOp.LoadGlobal:
                case BytecodeOp.StoreGlobal:
                    var fieldIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var fieldName = fieldIndex < stringPool.Count ? stringPool[fieldIndex] : "?";
                    sb.Append($" [{fieldIndex}] {fieldName}");
                    break;

                case BytecodeOp.LoadArg:
                    var argIndex = bytecode[offset++];
                    sb.Append($" arg{argIndex}");
                    break;

                case BytecodeOp.StoreArg:
                    var storeArgIndex = bytecode[offset++];
                    sb.Append($" arg{storeArgIndex}");
                    break;

                case BytecodeOp.Jump:
                case BytecodeOp.JumpIfTrue:
                case BytecodeOp.JumpIfFalse:
                case BytecodeOp.JumpIfNull:
                case BytecodeOp.JumpIfNotNull:
                    var jumpOffset = BitConverter.ToInt16(bytecode, offset);
                    offset += 2;
                    var target = offset + jumpOffset;
                    sb.Append($" {jumpOffset:+#;-#;0} (-> {target:D4})");
                    break;

                case BytecodeOp.Call:
                    var funcIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var argCount = bytecode[offset++];
                    var funcName = funcIndex < stringPool.Count ? stringPool[funcIndex] : "?";
                    sb.Append($" [{funcIndex}] {funcName}({argCount})");
                    break;

                case BytecodeOp.CallMethod:
                    var methodIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var methodArgCount = bytecode[offset++];
                    var methodName = methodIndex < stringPool.Count ? stringPool[methodIndex] : "?";
                    sb.Append($" [{methodIndex}] .{methodName}({methodArgCount})");
                    break;

                case BytecodeOp.CallMethodDynamic:
                    var dynMethodArgCount = bytecode[offset++];
                    sb.Append($" .dynamic({dynMethodArgCount})");
                    break;

                case BytecodeOp.CallDynamic:
                    var dynCallArgCount = bytecode[offset++];
                    sb.Append($" dynamic({dynCallArgCount})");
                    break;

                case BytecodeOp.CallBuiltin:
                    var builtinId = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var builtinArgCount = bytecode[offset++];
                    sb.Append($" builtin#{builtinId}({builtinArgCount})");
                    break;

                case BytecodeOp.New:
                    var newClassIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var newArgCount = bytecode[offset++];
                    var newClassName = newClassIndex < stringPool.Count ? stringPool[newClassIndex] : "?";
                    sb.Append($" [{newClassIndex}] novo {newClassName}({newArgCount})");
                    break;

                case BytecodeOp.LoadClass:
                    var classIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var className = classIndex < stringPool.Count ? stringPool[classIndex] : "?";
                    sb.Append($" [{classIndex}] {className}");
                    break;

                case BytecodeOp.InstanceOf:
                    var instClassIndex = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var instClassName = instClassIndex < stringPool.Count ? stringPool[instClassIndex] : "?";
                    sb.Append($" [{instClassIndex}] {instClassName}");
                    break;

                case BytecodeOp.LoadClassMember:
                    var clsIdx = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var memIdx = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    var clsName = clsIdx < stringPool.Count ? stringPool[clsIdx] : "?";
                    var memName = memIdx < stringPool.Count ? stringPool[memIdx] : "?";
                    sb.Append($" {clsName}:{memName}");
                    break;

                case BytecodeOp.Line:
                    var lineNum = BitConverter.ToUInt16(bytecode, offset);
                    offset += 2;
                    sb.Append($" #{lineNum}");
                    break;

                // Instructions with no operands
                case BytecodeOp.Nop:
                case BytecodeOp.Pop:
                case BytecodeOp.Dup:
                case BytecodeOp.Swap:
                case BytecodeOp.PushNull:
                case BytecodeOp.PushTrue:
                case BytecodeOp.PushFalse:
                case BytecodeOp.LoadArgCount:
                case BytecodeOp.LoadThis:
                case BytecodeOp.LoadIndex:
                case BytecodeOp.StoreIndex:
                case BytecodeOp.LoadFieldDynamic:
                case BytecodeOp.StoreFieldDynamic:
                case BytecodeOp.Concat:
                case BytecodeOp.LoadDynamic:
                case BytecodeOp.StoreDynamic:
                case BytecodeOp.Add:
                case BytecodeOp.Sub:
                case BytecodeOp.Mul:
                case BytecodeOp.Div:
                case BytecodeOp.Mod:
                case BytecodeOp.Neg:
                case BytecodeOp.Inc:
                case BytecodeOp.Dec:
                case BytecodeOp.BitAnd:
                case BytecodeOp.BitOr:
                case BytecodeOp.BitXor:
                case BytecodeOp.BitNot:
                case BytecodeOp.Shl:
                case BytecodeOp.Shr:
                case BytecodeOp.Eq:
                case BytecodeOp.Ne:
                case BytecodeOp.Lt:
                case BytecodeOp.Le:
                case BytecodeOp.Gt:
                case BytecodeOp.Ge:
                case BytecodeOp.StrictEq:
                case BytecodeOp.StrictNe:
                case BytecodeOp.And:
                case BytecodeOp.Or:
                case BytecodeOp.Not:
                case BytecodeOp.Return:
                case BytecodeOp.ReturnValue:
                case BytecodeOp.Delete:
                case BytecodeOp.TypeOf:
                case BytecodeOp.Terminate:
                case BytecodeOp.Debug:
                    break;

                default:
                    sb.Append($" [UNKNOWN OP: {(byte)op}]");
                    break;
            }

            sb.AppendLine();
        }
    }

    private static string EscapeString(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
