using IntMud.Compiler.Ast;

namespace IntMud.Compiler.Bytecode;

/// <summary>
/// Compiles AST nodes to bytecode.
/// </summary>
public sealed class BytecodeCompiler : IAstVisitor<object?>
{
    private readonly CompiledUnit _unit;
    private readonly BytecodeEmitter _emitter;
    private readonly CompilerScope _globalScope;
    private CompilerScope _currentScope;
    private CompiledFunction? _currentFunction;

    public BytecodeCompiler(string className)
    {
        _unit = new CompiledUnit { ClassName = className };
        _emitter = new BytecodeEmitter(_unit.StringPool);
        _globalScope = new CompilerScope(null);
        _currentScope = _globalScope;
    }

    /// <summary>
    /// Compile an AST to a compiled unit.
    /// </summary>
    public static CompiledUnit Compile(CompilationUnitNode ast)
    {
        if (ast.Classes.Count == 0)
            throw new CompilerException("No class definition found");

        var classNode = ast.Classes[0];
        var compiler = new BytecodeCompiler(classNode.Name);
        compiler._unit.SourceFile = ast.SourceFile;

        // Process file options
        foreach (var option in ast.Options)
        {
            option.Accept(compiler);
        }

        // Compile all classes (usually just one per file)
        foreach (var cls in ast.Classes)
        {
            cls.Accept(compiler);
        }

        return compiler._unit;
    }

    public object? VisitCompilationUnit(CompilationUnitNode node)
    {
        foreach (var option in node.Options)
        {
            option.Accept(this);
        }

        foreach (var cls in node.Classes)
        {
            cls.Accept(this);
        }

        return null;
    }

    public object? VisitFileOption(FileOptionNode node)
    {
        // File options are handled at a higher level (loader)
        return null;
    }

    public object? VisitClassDefinition(ClassDefinitionNode node)
    {
        _unit.BaseClasses.AddRange(node.BaseClasses);

        foreach (var member in node.Members)
        {
            member.Accept(this);
        }

        return null;
    }

    public object? VisitVariableDeclaration(VariableDeclarationNode node)
    {
        var variable = new CompiledVariable
        {
            Name = node.Name,
            TypeName = node.TypeName,
            ArraySize = node.VectorSize,
            IsCommon = node.Modifiers.HasFlag(VariableModifiers.Comum),
            IsSaved = node.Modifiers.HasFlag(VariableModifiers.Sav),
            Offset = _unit.VariableDataSize
        };

        // Calculate size based on type (simplified - actual size depends on type registry)
        variable.Size = GetTypeSize(node.TypeName, node.VectorSize);
        _unit.VariableDataSize += variable.Size;
        _unit.Variables.Add(variable);

        // Add to global scope
        _globalScope.DefineVariable(node.Name, _unit.Variables.Count - 1);

        return null;
    }

    public object? VisitFunctionDefinition(FunctionDefinitionNode node)
    {
        // Create new function
        var function = new CompiledFunction
        {
            Name = node.Name,
            SourceFile = _unit.SourceFile,
            StartLine = node.Line
        };

        _currentFunction = function;

        // Create new scope for function body
        _currentScope = new CompilerScope(_globalScope);

        // Reset emitter for new function
        var emitter = new BytecodeEmitter(_unit.StringPool);

        // Compile function body
        foreach (var stmt in node.Body)
        {
            CompileStatement(stmt, emitter);
        }

        // Ensure function ends with a return
        emitter.EmitReturn();

        function.Bytecode = emitter.GetBytecode();
        function.LineInfo.AddRange(emitter.GetLineInfo());
        function.LocalVariables.AddRange(
            _currentScope.GetLocalVariables().Select(lv => new CompiledVariable
            {
                Name = lv.Name,
                TypeName = lv.TypeName,
                Offset = lv.Index,
                Size = GetTypeSize(lv.TypeName, 0)
            }));

        _unit.Functions[node.Name] = function;

        // Restore scope
        _currentScope = _globalScope;
        _currentFunction = null;

        return null;
    }

    public object? VisitConstantDefinition(ConstantDefinitionNode node)
    {
        var constant = new CompiledConstant { Name = node.Name };

        if (node.Value is NumericLiteralNode numLit)
        {
            if (numLit.IsInteger)
            {
                constant.Type = ConstantType.Int;
                constant.IntValue = (int)numLit.Value;
            }
            else
            {
                constant.Type = ConstantType.Double;
                constant.DoubleValue = numLit.Value;
            }
        }
        else if (node.Value is StringLiteralNode strLit)
        {
            constant.Type = ConstantType.String;
            constant.StringValue = strLit.Value;
        }
        else if (node.Value is NullLiteralNode)
        {
            constant.Type = ConstantType.Null;
        }
        else
        {
            throw new CompilerException($"Constant '{node.Name}' must have a literal value", node.Line);
        }

        _unit.Constants[node.Name] = constant;
        _globalScope.DefineConstant(node.Name);
        return null;
    }

    public object? VisitVarFuncDefinition(VarFuncDefinitionNode node)
    {
        // VarFunc is compiled like a function but marked as virtual
        var function = new CompiledFunction
        {
            Name = node.Name,
            SourceFile = _unit.SourceFile,
            StartLine = node.Line,
            IsVirtual = true
        };

        _currentFunction = function;
        _currentScope = new CompilerScope(_globalScope);

        var emitter = new BytecodeEmitter(_unit.StringPool);

        foreach (var stmt in node.Body)
        {
            CompileStatement(stmt, emitter);
        }

        emitter.EmitReturn();

        function.Bytecode = emitter.GetBytecode();
        function.LineInfo.AddRange(emitter.GetLineInfo());

        _unit.Functions[node.Name] = function;

        _currentScope = _globalScope;
        _currentFunction = null;

        return null;
    }

    public object? VisitVarConstDefinition(VarConstDefinitionNode node)
    {
        // VarConst is a simple expression that returns a value
        var function = new CompiledFunction
        {
            Name = node.Name,
            SourceFile = _unit.SourceFile,
            StartLine = node.Line,
            IsVirtual = true
        };

        _currentFunction = function;
        _currentScope = new CompilerScope(_globalScope);

        var emitter = new BytecodeEmitter(_unit.StringPool);

        CompileExpression(node.Value, emitter);
        emitter.EmitReturnValue();

        function.Bytecode = emitter.GetBytecode();
        function.LineInfo.AddRange(emitter.GetLineInfo());

        _unit.Functions[node.Name] = function;

        _currentScope = _globalScope;
        _currentFunction = null;

        return null;
    }

    #region Statement Compilation

    private void CompileStatement(StatementNode stmt, BytecodeEmitter emitter)
    {
        emitter.SetLine(stmt.Line);
        stmt.Accept(new StatementCompiler(this, emitter));
    }

    public object? VisitIfStatement(IfStatementNode node) => null;
    public object? VisitWhileStatement(WhileStatementNode node) => null;
    public object? VisitForStatement(ForStatementNode node) => null;
    public object? VisitForEachStatement(ForEachStatementNode node) => null;
    public object? VisitSwitchStatement(SwitchStatementNode node) => null;
    public object? VisitCaseClause(CaseClauseNode node) => null;
    public object? VisitReturnStatement(ReturnStatementNode node) => null;
    public object? VisitExitStatement(ExitStatementNode node) => null;
    public object? VisitContinueStatement(ContinueStatementNode node) => null;
    public object? VisitTerminateStatement(TerminateStatementNode node) => null;
    public object? VisitExpressionStatement(ExpressionStatementNode node) => null;
    public object? VisitRefVarDeclaration(RefVarDeclarationNode node) => null;
    public object? VisitLocalVariableDeclaration(LocalVariableDeclarationNode node) => null;

    #endregion

    #region Expression Compilation

    private void CompileExpression(ExpressionNode expr, BytecodeEmitter emitter)
    {
        expr.Accept(new ExpressionCompiler(this, emitter));
    }

    public object? VisitBinaryExpression(BinaryExpressionNode node) => null;
    public object? VisitUnaryExpression(UnaryExpressionNode node) => null;
    public object? VisitConditionalExpression(ConditionalExpressionNode node) => null;
    public object? VisitNullCoalesceExpression(NullCoalesceExpressionNode node) => null;
    public object? VisitAssignmentExpression(AssignmentExpressionNode node) => null;
    public object? VisitMemberAccess(MemberAccessNode node) => null;
    public object? VisitIndexAccess(IndexAccessNode node) => null;
    public object? VisitFunctionCall(FunctionCallNode node) => null;
    public object? VisitPostfixIncrement(PostfixIncrementNode node) => null;
    public object? VisitIdentifier(IdentifierNode node) => null;
    public object? VisitNumericLiteral(NumericLiteralNode node) => null;
    public object? VisitStringLiteral(StringLiteralNode node) => null;
    public object? VisitNullLiteral(NullLiteralNode node) => null;
    public object? VisitThisReference(ThisReferenceNode node) => null;
    public object? VisitArgReference(ArgReferenceNode node) => null;
    public object? VisitArgsReference(ArgsReferenceNode node) => null;
    public object? VisitDollarReference(DollarReferenceNode node) => null;
    public object? VisitClassReference(ClassReferenceNode node) => null;
    public object? VisitNewExpression(NewExpressionNode node) => null;
    public object? VisitDeleteExpression(DeleteExpressionNode node) => null;

    #endregion

    #region Helpers

    internal CompilerScope CurrentScope => _currentScope;
    internal CompilerScope GlobalScope => _globalScope;
    internal CompiledUnit Unit => _unit;

    /// <summary>
    /// Check if a name is a class instance variable (supports implicit 'this').
    /// </summary>
    internal bool IsInstanceVariable(string name)
    {
        return _unit.Variables.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetTypeSize(string typeName, int vectorSize)
    {
        var baseSize = typeName.ToLowerInvariant() switch
        {
            "int1" => 1,
            "int8" => 1,
            "uint8" => 1,
            "int16" => 2,
            "uint16" => 2,
            "int32" or "int" => 4,
            "uint32" => 4,
            "int64" => 8,
            "uint64" => 8,
            "real" => 4,
            "real2" => 8,
            "ref" => 8,
            _ when typeName.StartsWith("txt1", StringComparison.OrdinalIgnoreCase) => ParseTextSize(typeName, 64),
            _ when typeName.StartsWith("txt2", StringComparison.OrdinalIgnoreCase) => ParseTextSize(typeName, 256),
            _ => 8 // Default for complex types (pointer size)
        };

        return vectorSize > 0 ? baseSize * vectorSize : baseSize;
    }

    private static int ParseTextSize(string typeName, int defaultSize)
    {
        // Parse txt1(64) or txt2(256)
        var start = typeName.IndexOf('(');
        var end = typeName.IndexOf(')');
        if (start > 0 && end > start)
        {
            if (int.TryParse(typeName.AsSpan(start + 1, end - start - 1), out var size))
            {
                return size + 1; // +1 for length byte
            }
        }
        return defaultSize + 1;
    }

    #endregion
}

/// <summary>
/// Compiles statements to bytecode.
/// </summary>
internal sealed class StatementCompiler : IAstVisitor<object?>
{
    private readonly BytecodeCompiler _compiler;
    private readonly BytecodeEmitter _emitter;

    public StatementCompiler(BytecodeCompiler compiler, BytecodeEmitter emitter)
    {
        _compiler = compiler;
        _emitter = emitter;
    }

    public object? VisitIfStatement(IfStatementNode node)
    {
        // Compile condition
        CompileExpression(node.Condition);

        // Jump to else if false
        var jumpToElse = _emitter.EmitJumpIfFalse();

        // Compile then body
        foreach (var stmt in node.ThenBody)
        {
            _emitter.SetLine(stmt.Line);
            stmt.Accept(this);
        }

        if (node.ElseIfClauses.Count > 0 || node.ElseBody.Count > 0)
        {
            // Jump over else/elseif
            var jumpToEnd = _emitter.EmitJump();
            _emitter.PatchJump(jumpToElse);

            // Compile else-if clauses
            var elseIfJumps = new List<int>();
            foreach (var elseIf in node.ElseIfClauses)
            {
                CompileExpression(elseIf.Condition);
                var nextElseIf = _emitter.EmitJumpIfFalse();

                foreach (var stmt in elseIf.Body)
                {
                    _emitter.SetLine(stmt.Line);
                    stmt.Accept(this);
                }

                elseIfJumps.Add(_emitter.EmitJump());
                _emitter.PatchJump(nextElseIf);
            }

            // Compile else body
            foreach (var stmt in node.ElseBody)
            {
                _emitter.SetLine(stmt.Line);
                stmt.Accept(this);
            }

            // Patch all jumps to end
            _emitter.PatchJump(jumpToEnd);
            foreach (var jump in elseIfJumps)
            {
                _emitter.PatchJump(jump);
            }
        }
        else
        {
            _emitter.PatchJump(jumpToElse);
        }

        return null;
    }

    public object? VisitWhileStatement(WhileStatementNode node)
    {
        _emitter.PushLoopContext();

        var loopStart = _emitter.DefineLabel();

        // Compile condition
        CompileExpression(node.Condition);

        // Jump out if false
        var jumpToEnd = _emitter.EmitJumpIfFalse();

        // Compile body
        foreach (var stmt in node.Body)
        {
            _emitter.SetLine(stmt.Line);
            stmt.Accept(this);
        }

        // Jump back to start (backward jump)
        var jumpBack = _emitter.EmitJump();
        _emitter.PatchJumpTo(jumpBack, loopStart);

        _emitter.PatchJump(jumpToEnd);
        _emitter.PopLoopContext(loopStart);

        return null;
    }

    public object? VisitForStatement(ForStatementNode node)
    {
        // Compile initializer
        CompileExpression(node.Initializer);
        _emitter.EmitPop(); // Discard initializer result

        _emitter.PushLoopContext();

        var loopStart = _emitter.DefineLabel();

        // Compile condition
        CompileExpression(node.Condition);

        // Jump out if false
        var jumpToEnd = _emitter.EmitJumpIfFalse();

        // Compile body
        foreach (var stmt in node.Body)
        {
            _emitter.SetLine(stmt.Line);
            stmt.Accept(this);
        }

        var continueTarget = _emitter.DefineLabel();

        // Compile increment
        CompileExpression(node.Increment);
        _emitter.EmitPop(); // Discard increment result

        // Jump back to condition (backward jump)
        var jumpBack = _emitter.EmitJump();
        _emitter.PatchJumpTo(jumpBack, loopStart);

        _emitter.PatchJump(jumpToEnd);
        _emitter.PopLoopContext(continueTarget);

        return null;
    }

    public object? VisitForEachStatement(ForEachStatementNode node)
    {
        // Create a local variable for the loop variable
        var loopVarName = node.VariableName;
        var loopVarIndex = _compiler.CurrentScope.DefineLocal(loopVarName, "int32");

        // Create a hidden index variable for iteration
        var indexVarName = $"__foreach_idx_{loopVarName}";
        var indexVarIndex = _compiler.CurrentScope.DefineLocal(indexVarName, "int32");

        // Create a hidden variable for the collection
        var collectionVarName = $"__foreach_col_{loopVarName}";
        var collectionVarIndex = _compiler.CurrentScope.DefineLocal(collectionVarName, "ref");

        // Store collection in hidden variable
        CompileExpression(node.Collection);
        _emitter.EmitStoreLocal(collectionVarIndex);

        // Initialize index to 0
        _emitter.EmitPushInt(0);
        _emitter.EmitStoreLocal(indexVarIndex);

        _emitter.PushLoopContext();

        var loopStart = _emitter.DefineLabel();

        // Condition: index < tam(collection)
        _emitter.EmitLoadLocal(indexVarIndex);
        _emitter.EmitLoadLocal(collectionVarIndex);
        _emitter.EmitCall("tam", 1);
        _emitter.EmitLt();

        // Jump out if false
        var jumpToEnd = _emitter.EmitJumpIfFalse();

        // Get current element: loopVar = collection[index]
        _emitter.EmitLoadLocal(collectionVarIndex);
        _emitter.EmitLoadLocal(indexVarIndex);
        _emitter.EmitLoadIndex();
        _emitter.EmitStoreLocal(loopVarIndex);

        // Compile body
        foreach (var stmt in node.Body)
        {
            _emitter.SetLine(stmt.Line);
            stmt.Accept(this);
        }

        var continueTarget = _emitter.DefineLabel();

        // Increment index
        _emitter.EmitLoadLocal(indexVarIndex);
        _emitter.EmitPushInt(1);
        _emitter.EmitAdd();
        _emitter.EmitStoreLocal(indexVarIndex);

        // Jump back to condition (backward jump)
        var jumpBack = _emitter.EmitJump();
        _emitter.PatchJumpTo(jumpBack, loopStart);

        _emitter.PatchJump(jumpToEnd);
        _emitter.PopLoopContext(continueTarget);

        return null;
    }

    public object? VisitSwitchStatement(SwitchStatementNode node)
    {
        // Compile switch expression
        CompileExpression(node.Expression);

        var caseJumps = new List<(string? label, int jumpPos)>();
        var endJumps = new List<int>();

        // Emit comparisons and jumps for each case
        foreach (var caseClause in node.Cases)
        {
            if (caseClause.Label != null)
            {
                _emitter.EmitDup(); // Duplicate switch value for comparison
                _emitter.EmitPushString(caseClause.Label);
                _emitter.EmitEq();
                caseJumps.Add((caseClause.Label, _emitter.EmitJumpIfTrue()));
            }
        }

        // Jump to default case if no match
        var defaultJump = _emitter.EmitJump();

        // Emit case bodies
        foreach (var caseClause in node.Cases)
        {
            var matchingJump = caseJumps.FirstOrDefault(j => j.label == caseClause.Label);
            if (matchingJump.jumpPos != 0)
            {
                _emitter.PatchJump(matchingJump.jumpPos);
            }

            foreach (var stmt in caseClause.Body)
            {
                _emitter.SetLine(stmt.Line);
                stmt.Accept(this);
            }

            endJumps.Add(_emitter.EmitJump());
        }

        // Default case
        _emitter.PatchJump(defaultJump);
        if (node.DefaultCase != null)
        {
            foreach (var stmt in node.DefaultCase.Body)
            {
                _emitter.SetLine(stmt.Line);
                stmt.Accept(this);
            }
        }

        // Patch all end jumps
        foreach (var jump in endJumps)
        {
            _emitter.PatchJump(jump);
        }

        _emitter.EmitPop(); // Pop switch value

        return null;
    }

    public object? VisitCaseClause(CaseClauseNode node) => null;

    public object? VisitReturnStatement(ReturnStatementNode node)
    {
        if (node.Value != null)
        {
            CompileExpression(node.Value);
            _emitter.EmitReturnValue();
        }
        else
        {
            _emitter.EmitReturn();
        }
        return null;
    }

    public object? VisitExitStatement(ExitStatementNode node)
    {
        // Exit (sair) breaks out of loop
        if (node.Condition != null)
        {
            CompileExpression(node.Condition);
            var skipBreak = _emitter.EmitJumpIfFalse();
            _emitter.EmitBreak();
            _emitter.PatchJump(skipBreak);
        }
        else
        {
            _emitter.EmitBreak();
        }
        return null;
    }

    public object? VisitContinueStatement(ContinueStatementNode node)
    {
        if (node.Condition != null)
        {
            CompileExpression(node.Condition);
            var skipContinue = _emitter.EmitJumpIfFalse();
            _emitter.EmitContinue();
            _emitter.PatchJump(skipContinue);
        }
        else
        {
            _emitter.EmitContinue();
        }
        return null;
    }

    public object? VisitTerminateStatement(TerminateStatementNode node)
    {
        _emitter.EmitTerminate();
        return null;
    }

    public object? VisitExpressionStatement(ExpressionStatementNode node)
    {
        foreach (var expr in node.Expressions)
        {
            CompileExpression(expr);
            _emitter.EmitPop(); // Discard result
        }
        return null;
    }

    public object? VisitRefVarDeclaration(RefVarDeclarationNode node)
    {
        // RefVar creates a reference to another variable
        CompileExpression(node.Value);

        // Store as a local reference
        var index = _compiler.CurrentScope.DefineLocal(node.Name, "ref");
        _emitter.EmitStoreLocal(index);

        return null;
    }

    public object? VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        var index = _compiler.CurrentScope.DefineLocal(node.Name, node.TypeName);

        if (node.Initializer != null)
        {
            CompileExpression(node.Initializer);
            _emitter.EmitStoreLocal(index);
        }

        return null;
    }

    private void CompileExpression(ExpressionNode expr)
    {
        expr.Accept(new ExpressionCompiler(_compiler, _emitter));
    }

    // Empty implementations for expression visitor methods (not used in statement compiler)
    public object? VisitCompilationUnit(CompilationUnitNode node) => null;
    public object? VisitFileOption(FileOptionNode node) => null;
    public object? VisitClassDefinition(ClassDefinitionNode node) => null;
    public object? VisitVariableDeclaration(VariableDeclarationNode node) => null;
    public object? VisitFunctionDefinition(FunctionDefinitionNode node) => null;
    public object? VisitConstantDefinition(ConstantDefinitionNode node) => null;
    public object? VisitVarFuncDefinition(VarFuncDefinitionNode node) => null;
    public object? VisitVarConstDefinition(VarConstDefinitionNode node) => null;
    public object? VisitBinaryExpression(BinaryExpressionNode node) => null;
    public object? VisitUnaryExpression(UnaryExpressionNode node) => null;
    public object? VisitConditionalExpression(ConditionalExpressionNode node) => null;
    public object? VisitNullCoalesceExpression(NullCoalesceExpressionNode node) => null;
    public object? VisitAssignmentExpression(AssignmentExpressionNode node) => null;
    public object? VisitMemberAccess(MemberAccessNode node) => null;
    public object? VisitIndexAccess(IndexAccessNode node) => null;
    public object? VisitFunctionCall(FunctionCallNode node) => null;
    public object? VisitPostfixIncrement(PostfixIncrementNode node) => null;
    public object? VisitIdentifier(IdentifierNode node) => null;
    public object? VisitNumericLiteral(NumericLiteralNode node) => null;
    public object? VisitStringLiteral(StringLiteralNode node) => null;
    public object? VisitNullLiteral(NullLiteralNode node) => null;
    public object? VisitThisReference(ThisReferenceNode node) => null;
    public object? VisitArgReference(ArgReferenceNode node) => null;
    public object? VisitArgsReference(ArgsReferenceNode node) => null;
    public object? VisitDollarReference(DollarReferenceNode node) => null;
    public object? VisitClassReference(ClassReferenceNode node) => null;
    public object? VisitNewExpression(NewExpressionNode node) => null;
    public object? VisitDeleteExpression(DeleteExpressionNode node) => null;
}

/// <summary>
/// Compiles expressions to bytecode.
/// </summary>
internal sealed class ExpressionCompiler : IAstVisitor<object?>
{
    private readonly BytecodeCompiler _compiler;
    private readonly BytecodeEmitter _emitter;

    public ExpressionCompiler(BytecodeCompiler compiler, BytecodeEmitter emitter)
    {
        _compiler = compiler;
        _emitter = emitter;
    }

    public object? VisitBinaryExpression(BinaryExpressionNode node)
    {
        // Handle short-circuit evaluation for && and ||
        if (node.Operator == BinaryOperator.And)
        {
            node.Left.Accept(this);
            var skipRight = _emitter.EmitJumpIfFalse();
            node.Right.Accept(this);
            var skipFalse = _emitter.EmitJump();
            _emitter.PatchJump(skipRight);
            _emitter.EmitPushFalse();
            _emitter.PatchJump(skipFalse);
            return null;
        }

        if (node.Operator == BinaryOperator.Or)
        {
            node.Left.Accept(this);
            var skipRight = _emitter.EmitJumpIfTrue();
            node.Right.Accept(this);
            var skipTrue = _emitter.EmitJump();
            _emitter.PatchJump(skipRight);
            _emitter.EmitPushTrue();
            _emitter.PatchJump(skipTrue);
            return null;
        }

        // Normal binary operations
        node.Left.Accept(this);
        node.Right.Accept(this);

        switch (node.Operator)
        {
            case BinaryOperator.Add: _emitter.EmitAdd(); break;
            case BinaryOperator.Subtract: _emitter.EmitSub(); break;
            case BinaryOperator.Multiply: _emitter.EmitMul(); break;
            case BinaryOperator.Divide: _emitter.EmitDiv(); break;
            case BinaryOperator.Modulo: _emitter.EmitMod(); break;
            case BinaryOperator.LessThan: _emitter.EmitLt(); break;
            case BinaryOperator.LessOrEqual: _emitter.EmitLe(); break;
            case BinaryOperator.GreaterThan: _emitter.EmitGt(); break;
            case BinaryOperator.GreaterOrEqual: _emitter.EmitGe(); break;
            case BinaryOperator.Equal: _emitter.EmitEq(); break;
            case BinaryOperator.NotEqual: _emitter.EmitNe(); break;
            case BinaryOperator.StrictEqual: _emitter.EmitStrictEq(); break;
            case BinaryOperator.StrictNotEqual: _emitter.EmitStrictNe(); break;
            case BinaryOperator.BitwiseAnd: _emitter.EmitBitAnd(); break;
            case BinaryOperator.BitwiseOr: _emitter.EmitBitOr(); break;
            case BinaryOperator.BitwiseXor: _emitter.EmitBitXor(); break;
            case BinaryOperator.ShiftLeft: _emitter.EmitShl(); break;
            case BinaryOperator.ShiftRight: _emitter.EmitShr(); break;
            default:
                throw new CompilerException($"Unknown binary operator: {node.Operator}", node.Line);
        }

        return null;
    }

    public object? VisitUnaryExpression(UnaryExpressionNode node)
    {
        switch (node.Operator)
        {
            case UnaryOperator.PreIncrement:
                CompilePrefixIncDec(node.Operand, true);
                break;

            case UnaryOperator.PreDecrement:
                CompilePrefixIncDec(node.Operand, false);
                break;

            default:
                node.Operand.Accept(this);
                switch (node.Operator)
                {
                    case UnaryOperator.Negate: _emitter.EmitNeg(); break;
                    case UnaryOperator.Not: _emitter.EmitNot(); break;
                    case UnaryOperator.BitwiseNot: _emitter.EmitBitNot(); break;
                    default:
                        throw new CompilerException($"Unknown unary operator: {node.Operator}", node.Line);
                }
                break;
        }

        return null;
    }

    private void CompilePrefixIncDec(ExpressionNode operand, bool isIncrement)
    {
        // Load, increment/decrement, store, leave value on stack
        if (operand is IdentifierNode id)
        {
            var (kind, index) = _compiler.CurrentScope.ResolveVariable(id.Name);
            switch (kind)
            {
                case VariableKind.Local:
                    _emitter.EmitLoadLocal(index);
                    if (isIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
                    _emitter.EmitDup();
                    _emitter.EmitStoreLocal(index);
                    break;
                case VariableKind.Global:
                    // Check if this is an instance variable (implicit 'this')
                    if (_compiler.IsInstanceVariable(id.Name))
                    {
                        _emitter.EmitLoadThis();
                        _emitter.EmitLoadField(id.Name);
                        if (isIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
                        _emitter.EmitDup();
                        _emitter.EmitLoadThis();
                        _emitter.EmitSwap();
                        _emitter.EmitStoreField(id.Name);
                    }
                    else
                    {
                        _emitter.EmitLoadGlobal(id.Name);
                        if (isIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
                        _emitter.EmitDup();
                        _emitter.EmitStoreGlobal(id.Name);
                    }
                    break;
                default:
                    throw new CompilerException($"Cannot increment/decrement '{id.Name}'", operand.Line);
            }
        }
        else
        {
            throw new CompilerException("Increment/decrement requires a variable", operand.Line);
        }
    }

    public object? VisitConditionalExpression(ConditionalExpressionNode node)
    {
        node.Condition.Accept(this);

        var jumpToElse = _emitter.EmitJumpIfFalse();

        if (node.ThenValue != null)
            node.ThenValue.Accept(this);
        else
            _emitter.EmitPushNull();

        var jumpToEnd = _emitter.EmitJump();
        _emitter.PatchJump(jumpToElse);

        if (node.ElseValue != null)
            node.ElseValue.Accept(this);
        else
            _emitter.EmitPushNull();

        _emitter.PatchJump(jumpToEnd);

        return null;
    }

    public object? VisitNullCoalesceExpression(NullCoalesceExpressionNode node)
    {
        node.Left.Accept(this);
        _emitter.EmitDup();
        var jumpIfNotNull = _emitter.EmitJumpIfNotNull();

        _emitter.EmitPop(); // Remove null
        node.Right.Accept(this);

        _emitter.PatchJump(jumpIfNotNull);

        return null;
    }

    public object? VisitAssignmentExpression(AssignmentExpressionNode node)
    {
        if (node.Operator == AssignmentOperator.Assign)
        {
            // Simple assignment
            node.Right.Accept(this);
            CompileStore(node.Left, true);
        }
        else
        {
            // Compound assignment (+=, -=, etc.)
            CompileLoad(node.Left);
            node.Right.Accept(this);

            switch (node.Operator)
            {
                case AssignmentOperator.AddAssign: _emitter.EmitAdd(); break;
                case AssignmentOperator.SubtractAssign: _emitter.EmitSub(); break;
                case AssignmentOperator.MultiplyAssign: _emitter.EmitMul(); break;
                case AssignmentOperator.DivideAssign: _emitter.EmitDiv(); break;
                case AssignmentOperator.ModuloAssign: _emitter.EmitMod(); break;
                case AssignmentOperator.ShiftLeftAssign: _emitter.EmitShl(); break;
                case AssignmentOperator.ShiftRightAssign: _emitter.EmitShr(); break;
                case AssignmentOperator.BitwiseAndAssign: _emitter.EmitBitAnd(); break;
                case AssignmentOperator.BitwiseOrAssign: _emitter.EmitBitOr(); break;
                case AssignmentOperator.BitwiseXorAssign: _emitter.EmitBitXor(); break;
                default:
                    throw new CompilerException($"Unknown assignment operator: {node.Operator}", node.Line);
            }

            CompileStore(node.Left, true);
        }

        return null;
    }

    private void CompileLoad(ExpressionNode expr)
    {
        if (expr is IdentifierNode id)
        {
            var (kind, index) = _compiler.CurrentScope.ResolveVariable(id.Name);
            switch (kind)
            {
                case VariableKind.Local:
                    _emitter.EmitLoadLocal(index);
                    break;
                case VariableKind.Global:
                    // Check if this is an instance variable (implicit 'this')
                    if (_compiler.IsInstanceVariable(id.Name))
                    {
                        _emitter.EmitLoadThis();
                        _emitter.EmitLoadField(id.Name);
                    }
                    else
                    {
                        _emitter.EmitLoadGlobal(id.Name);
                    }
                    break;
                case VariableKind.Constant:
                    if (_compiler.Unit.Constants.TryGetValue(id.Name, out var constant))
                    {
                        switch (constant.Type)
                        {
                            case ConstantType.Int:
                                _emitter.EmitPushInt(constant.IntValue);
                                break;
                            case ConstantType.Double:
                                _emitter.EmitPushDouble(constant.DoubleValue);
                                break;
                            case ConstantType.String:
                                _emitter.EmitPushString(constant.StringValue!);
                                break;
                            case ConstantType.Null:
                                _emitter.EmitPushNull();
                                break;
                        }
                    }
                    break;
                default:
                    throw new CompilerException($"Unknown variable: {id.Name}", expr.Line);
            }
        }
        else if (expr is MemberAccessNode member)
        {
            member.Object.Accept(this);
            _emitter.EmitLoadField(member.Member);
        }
        else if (expr is IndexAccessNode index)
        {
            index.Object.Accept(this);
            index.Index.Accept(this);
            _emitter.EmitLoadIndex();
        }
        else
        {
            expr.Accept(this);
        }
    }

    private void CompileStore(ExpressionNode expr, bool keepValue)
    {
        if (keepValue)
            _emitter.EmitDup();

        if (expr is IdentifierNode id)
        {
            var (kind, index) = _compiler.CurrentScope.ResolveVariable(id.Name);
            switch (kind)
            {
                case VariableKind.Local:
                    _emitter.EmitStoreLocal(index);
                    break;
                case VariableKind.Global:
                    // Check if this is an instance variable (implicit 'this')
                    if (_compiler.IsInstanceVariable(id.Name))
                    {
                        _emitter.EmitLoadThis();
                        _emitter.EmitSwap();
                        _emitter.EmitStoreField(id.Name);
                    }
                    else
                    {
                        _emitter.EmitStoreGlobal(id.Name);
                    }
                    break;
                default:
                    throw new CompilerException($"Cannot assign to '{id.Name}'", expr.Line);
            }
        }
        else if (expr is MemberAccessNode member)
        {
            member.Object.Accept(this);
            _emitter.EmitSwap();
            _emitter.EmitStoreField(member.Member);
        }
        else if (expr is IndexAccessNode index)
        {
            index.Object.Accept(this);
            index.Index.Accept(this);
            // Stack: [value, obj, index]
            // Need to reorder for StoreIndex which expects [obj, index, value]
            // This is tricky - we need a rotate operation
            // For now, we'll just emit in the right order
            _emitter.EmitStoreIndex();
        }
        else
        {
            throw new CompilerException("Invalid assignment target", expr.Line);
        }
    }

    public object? VisitMemberAccess(MemberAccessNode node)
    {
        node.Object.Accept(this);
        _emitter.EmitLoadField(node.Member);
        return null;
    }

    public object? VisitIndexAccess(IndexAccessNode node)
    {
        node.Object.Accept(this);
        node.Index.Accept(this);
        _emitter.EmitLoadIndex();
        return null;
    }

    public object? VisitFunctionCall(FunctionCallNode node)
    {
        // Determine call type
        if (node.Function is IdentifierNode funcId)
        {
            // Direct function call - compile arguments then call
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }
            _emitter.EmitCall(funcId.Name, node.Arguments.Count);
        }
        else if (node.Function is MemberAccessNode memberAccess)
        {
            // Method call - compile object first, then arguments, then call
            memberAccess.Object.Accept(this);
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }
            _emitter.EmitCallMethod(memberAccess.Member, node.Arguments.Count);
        }
        else if (node.Function is ClassReferenceNode classRef)
        {
            // Static method call
            _emitter.EmitLoadClass(classRef.ClassName);
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }
            _emitter.EmitCallMethod(classRef.MemberName, node.Arguments.Count);
        }
        else
        {
            throw new CompilerException("Invalid function call target", node.Line);
        }

        return null;
    }

    public object? VisitPostfixIncrement(PostfixIncrementNode node)
    {
        // Load, duplicate (keep old value), increment/decrement, store
        if (node.Operand is IdentifierNode id)
        {
            var (kind, index) = _compiler.CurrentScope.ResolveVariable(id.Name);
            switch (kind)
            {
                case VariableKind.Local:
                    _emitter.EmitLoadLocal(index);
                    _emitter.EmitDup();
                    if (node.IsIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
                    _emitter.EmitStoreLocal(index);
                    break;
                case VariableKind.Global:
                    // Check if this is an instance variable (implicit 'this')
                    if (_compiler.IsInstanceVariable(id.Name))
                    {
                        _emitter.EmitLoadThis();
                        _emitter.EmitLoadField(id.Name);
                        _emitter.EmitDup();
                        if (node.IsIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
                        _emitter.EmitLoadThis();
                        _emitter.EmitSwap();
                        _emitter.EmitStoreField(id.Name);
                    }
                    else
                    {
                        _emitter.EmitLoadGlobal(id.Name);
                        _emitter.EmitDup();
                        if (node.IsIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
                        _emitter.EmitStoreGlobal(id.Name);
                    }
                    break;
                default:
                    throw new CompilerException($"Cannot increment/decrement '{id.Name}'", node.Line);
            }
        }
        else
        {
            throw new CompilerException("Postfix increment/decrement requires a variable", node.Line);
        }

        return null;
    }

    public object? VisitIdentifier(IdentifierNode node)
    {
        CompileLoad(node);
        return null;
    }

    public object? VisitNumericLiteral(NumericLiteralNode node)
    {
        if (node.IsInteger)
        {
            _emitter.EmitPushInt((int)node.Value);
        }
        else
        {
            _emitter.EmitPushDouble(node.Value);
        }
        return null;
    }

    public object? VisitStringLiteral(StringLiteralNode node)
    {
        _emitter.EmitPushString(node.Value);
        return null;
    }

    public object? VisitNullLiteral(NullLiteralNode node)
    {
        _emitter.EmitPushNull();
        return null;
    }

    public object? VisitThisReference(ThisReferenceNode node)
    {
        _emitter.EmitLoadThis();
        return null;
    }

    public object? VisitArgReference(ArgReferenceNode node)
    {
        _emitter.EmitLoadArg(node.Index);
        return null;
    }

    public object? VisitArgsReference(ArgsReferenceNode node)
    {
        _emitter.EmitLoadArgCount();
        return null;
    }

    public object? VisitDollarReference(DollarReferenceNode node)
    {
        // $classname loads the class object
        _emitter.EmitLoadClass(node.ClassName);
        return null;
    }

    public object? VisitClassReference(ClassReferenceNode node)
    {
        // classname:member loads a static member
        _emitter.EmitLoadClassMember(node.ClassName, node.MemberName);
        return null;
    }

    public object? VisitNewExpression(NewExpressionNode node)
    {
        // Compile arguments first (push onto stack)
        foreach (var arg in node.Arguments)
        {
            arg.Accept(this);
        }

        // Emit new instruction with class name and argument count
        _emitter.EmitNew(node.ClassName, node.Arguments.Count);
        return null;
    }

    public object? VisitDeleteExpression(DeleteExpressionNode node)
    {
        // Compile the operand (object to delete)
        node.Operand.Accept(this);

        // Emit delete instruction
        _emitter.EmitDelete();
        return null;
    }

    // Empty implementations for non-expression visitor methods
    public object? VisitCompilationUnit(CompilationUnitNode node) => null;
    public object? VisitFileOption(FileOptionNode node) => null;
    public object? VisitClassDefinition(ClassDefinitionNode node) => null;
    public object? VisitVariableDeclaration(VariableDeclarationNode node) => null;
    public object? VisitFunctionDefinition(FunctionDefinitionNode node) => null;
    public object? VisitConstantDefinition(ConstantDefinitionNode node) => null;
    public object? VisitVarFuncDefinition(VarFuncDefinitionNode node) => null;
    public object? VisitVarConstDefinition(VarConstDefinitionNode node) => null;
    public object? VisitIfStatement(IfStatementNode node) => null;
    public object? VisitWhileStatement(WhileStatementNode node) => null;
    public object? VisitForStatement(ForStatementNode node) => null;
    public object? VisitForEachStatement(ForEachStatementNode node) => null;
    public object? VisitSwitchStatement(SwitchStatementNode node) => null;
    public object? VisitCaseClause(CaseClauseNode node) => null;
    public object? VisitReturnStatement(ReturnStatementNode node) => null;
    public object? VisitExitStatement(ExitStatementNode node) => null;
    public object? VisitContinueStatement(ContinueStatementNode node) => null;
    public object? VisitTerminateStatement(TerminateStatementNode node) => null;
    public object? VisitExpressionStatement(ExpressionStatementNode node) => null;
    public object? VisitRefVarDeclaration(RefVarDeclarationNode node) => null;
    public object? VisitLocalVariableDeclaration(LocalVariableDeclarationNode node) => null;
}
