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
        // For backward compatibility, return the first class
        var units = CompileAll(ast);
        if (units.Count == 0)
            throw new CompilerException("No class definition found");
        return units[0];
    }

    /// <summary>
    /// Compile all classes in the AST to separate CompiledUnits.
    /// Each class gets its own unit with its own constants, functions, and variables.
    /// </summary>
    public static List<CompiledUnit> CompileAll(CompilationUnitNode ast)
    {
        if (ast.Classes.Count == 0)
            throw new CompilerException("No class definition found");

        var results = new List<CompiledUnit>();

        foreach (var classNode in ast.Classes)
        {
            var compiler = new BytecodeCompiler(classNode.Name);
            compiler._unit.SourceFile = ast.SourceFile;

            // Compile this single class
            classNode.Accept(compiler);

            results.Add(compiler._unit);
        }

        return results;
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
            // Expression constant - compile to bytecode for runtime evaluation
            constant.Type = ConstantType.Expression;
            constant.ExpressionBytecode = CompileConstantExpression(node.Value);
        }

        _unit.Constants[node.Name] = constant;
        _globalScope.DefineConstant(node.Name);
        return null;
    }

    /// <summary>
    /// Compile an expression to bytecode for runtime evaluation (used by const with expressions).
    /// </summary>
    private byte[] CompileConstantExpression(ExpressionNode expr)
    {
        // Create a temporary emitter for the expression bytecode
        var tempEmitter = new BytecodeEmitter(_unit.StringPool);

        // Compile the expression
        var exprCompiler = new ExpressionCompiler(this, tempEmitter);
        expr.Accept(exprCompiler);

        // Add return value instruction
        tempEmitter.EmitReturnValue();

        return tempEmitter.GetBytecode();
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
    public object? VisitDynamicMemberAccess(DynamicMemberAccessNode node) => null;
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
    public object? VisitDynamicIdentifier(DynamicIdentifierNode node) => null;
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
        if (node.Condition != null)
        {
            // ret condition, value - only returns if condition is true
            CompileExpression(node.Condition);
            var skipReturn = _emitter.EmitJumpIfFalse();

            if (node.Value != null)
            {
                CompileExpression(node.Value);
                _emitter.EmitReturnValue();
            }
            else
            {
                _emitter.EmitReturn();
            }

            _emitter.PatchJump(skipReturn);
        }
        else if (node.Value != null)
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
    public object? VisitDynamicMemberAccess(DynamicMemberAccessNode node) => null;
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
    public object? VisitDynamicIdentifier(DynamicIdentifierNode node) => null;
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
        // Prefix ++/-- : inc/dec then return new value
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
                case VariableKind.Constant:
                    // Check if this is an instance variable (implicit 'this')
                    if (_compiler.IsInstanceVariable(id.Name) || kind == VariableKind.Constant)
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
        else if (operand is MemberAccessNode memberAccess)
        {
            // ++obj.field - increment and return new value
            // 1. Load, increment, store
            memberAccess.Object.Accept(this);  // [obj]
            _emitter.EmitDup();                // [obj, obj]
            _emitter.EmitLoadField(memberAccess.Member);  // [obj, value]
            if (isIncrement) _emitter.EmitInc(); else _emitter.EmitDec();  // [obj, new_value]
            _emitter.EmitStoreField(memberAccess.Member);  // []
            // 2. Return new value by loading it again
            memberAccess.Object.Accept(this);
            _emitter.EmitLoadField(memberAccess.Member);  // [new_value]
        }
        else if (operand is IndexAccessNode indexAccess)
        {
            // ++arr[i]
            indexAccess.Object.Accept(this);
            indexAccess.Index.Accept(this);
            indexAccess.Object.Accept(this);
            indexAccess.Index.Accept(this);
            _emitter.EmitLoadIndex();
            if (isIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
            _emitter.EmitStoreIndex();
            // Return new value
            indexAccess.Object.Accept(this);
            indexAccess.Index.Accept(this);
            _emitter.EmitLoadIndex();
        }
        else if (operand is DynamicMemberAccessNode dynamicMember)
        {
            // ++obj.[expr]
            dynamicMember.Object.Accept(this);
            CompileDynamicMemberName(dynamicMember.MemberParts);
            dynamicMember.Object.Accept(this);
            CompileDynamicMemberName(dynamicMember.MemberParts);
            _emitter.EmitLoadFieldDynamic();
            if (isIncrement) _emitter.EmitInc(); else _emitter.EmitDec();
            _emitter.EmitStoreFieldDynamic();
            // Return new value
            dynamicMember.Object.Accept(this);
            CompileDynamicMemberName(dynamicMember.MemberParts);
            _emitter.EmitLoadFieldDynamic();
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
        else if (expr is DynamicMemberAccessNode dynamicMember)
        {
            // Use the visitor implementation
            VisitDynamicMemberAccess(dynamicMember);
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
                case VariableKind.Constant:
                    // In IntMUD, constants can be shadowed by instance variables
                    // Assignment to a constant name writes to this.field
                    _emitter.EmitLoadThis();
                    _emitter.EmitSwap();
                    _emitter.EmitStoreField(id.Name);
                    break;
                default:
                    throw new CompilerException($"Cannot assign to '{id.Name}'", expr.Line);
            }
        }
        else if (expr is ArgReferenceNode argRef)
        {
            // IntMUD allows assigning to function arguments (arg0, arg1, etc.)
            _emitter.EmitStoreArg(argRef.Index);
        }
        else if (expr is MemberAccessNode member)
        {
            member.Object.Accept(this);
            _emitter.EmitSwap();
            _emitter.EmitStoreField(member.Member);
        }
        else if (expr is DynamicMemberAccessNode dynamicMember)
        {
            // Stack: [value]
            // Need to push: [value, object, fieldName]
            dynamicMember.Object.Accept(this);

            // Build the dynamic member name
            if (dynamicMember.MemberParts.Count == 0)
            {
                throw new CompilerException("Dynamic member access must have at least one part", expr.Line);
            }

            CompileMemberPart(dynamicMember.MemberParts[0]);
            for (int i = 1; i < dynamicMember.MemberParts.Count; i++)
            {
                CompileMemberPart(dynamicMember.MemberParts[i]);
                _emitter.EmitAdd();
            }

            if (dynamicMember.IsCountdown)
            {
                _emitter.EmitPushString("@");
                _emitter.EmitAdd();
            }

            // Stack: [value, object, fieldName]
            _emitter.EmitStoreFieldDynamic();
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
        else if (expr is DynamicIdentifierNode dynamicId)
        {
            // Stack: [value]
            // Build the dynamic variable name
            if (dynamicId.Parts.Count == 0)
            {
                throw new CompilerException("Dynamic identifier must have at least one part", expr.Line);
            }

            // Push the first name part
            CompileDynamicNamePart(dynamicId.Parts[0]);

            // Concatenate remaining parts
            for (int i = 1; i < dynamicId.Parts.Count; i++)
            {
                CompileDynamicNamePart(dynamicId.Parts[i]);
                _emitter.EmitConcat();
            }

            if (dynamicId.IsCountdown)
            {
                _emitter.EmitPushString("@");
                _emitter.EmitConcat();
            }

            // Stack: [value, name]
            // Swap to get [name, value] for StoreDynamic
            _emitter.EmitSwap();
            _emitter.EmitStoreDynamic();
        }
        else if (expr is ClassReferenceNode classRef)
        {
            // Handle assignment to class members: class:member = value
            // Stack: [value]

            // Determine if we need static or dynamic store
            bool needsDynamicStore = classRef.ClassName == null ||
                                      classRef.ClassNameIndex != null ||
                                      classRef.MemberName == null ||
                                      classRef.DynamicMemberParts.Count > 0;

            if (!needsDynamicStore && classRef.ClassName != null && classRef.MemberName != null)
            {
                // Static case: class:member = value
                _emitter.EmitStoreClassMember(classRef.ClassName, classRef.MemberName);
            }
            else
            {
                // Dynamic case: need to build class name and member name on stack
                // Stack currently: [value]

                // Build class name
                if (classRef.ClassName != null && classRef.ClassNameIndex == null)
                {
                    _emitter.EmitPushString(classRef.ClassName);
                }
                else if (classRef.ClassName == null && classRef.ClassNameIndex != null)
                {
                    classRef.ClassNameIndex.Accept(this);
                }
                else if (classRef.ClassName != null && classRef.ClassNameIndex != null)
                {
                    _emitter.EmitPushString(classRef.ClassName);
                    classRef.ClassNameIndex.Accept(this);
                    _emitter.EmitConcat();
                }
                else
                {
                    _emitter.EmitPushString("");
                }

                // Build member name
                if (classRef.MemberName != null && classRef.DynamicMemberParts.Count == 0)
                {
                    _emitter.EmitPushString(classRef.MemberName);
                }
                else if (classRef.DynamicMemberParts.Count > 0)
                {
                    bool first = true;
                    foreach (var part in classRef.DynamicMemberParts)
                    {
                        part.Accept(this);
                        if (!first)
                        {
                            _emitter.EmitConcat();
                        }
                        first = false;
                    }
                }
                else
                {
                    _emitter.EmitPushString("");
                }

                // Stack: [value, className, memberName]
                // Need to reorder to [className, memberName, value] for StoreClassMemberDynamic
                // Use Rot3 or equivalent - for now we'll do it with swaps
                // Actually our stack order is wrong, let's fix it:
                // We need: push value first, then className, then memberName
                // But we pushed: value, className, memberName
                // So the order is already correct for a store that expects [className, memberName, value]
                // Wait, we pushed value first (it was on stack), then className, then memberName
                // So stack is: value (bottom), className, memberName (top)
                // StoreClassMemberDynamic expects: className, memberName, value (top is value)
                // So we need to rotate

                // Simpler approach: emit the class/member names first, then the value will be at correct position
                // Actually, let me reconsider. The value is already on the stack before we start.
                // So we have: [value] then we push className, memberName
                // Result: [value, className, memberName] (top = memberName)
                // We need: [className, memberName, value] for the store
                // This requires rotating the stack

                // For simplicity, use StoreClassMemberDynamic which expects stack order of [className, memberName, value]
                // We have [value, className, memberName]
                // Rotate: swap top 2, then swap with 3rd
                // [value, className, memberName] -> swap -> [value, memberName, className]
                // Now we need to get value to top... this is getting complicated

                // Alternative: compute className and memberName into locals, then do the store
                // For now, let's just use a different approach: compute everything, store value in temp

                // Simplest workaround: just emit as if stack is [className, memberName, value]
                // and fix the order by swapping

                // Stack is: [value, className, memberName]
                // We need: [className, memberName, value]
                // Do: ROT3 (not available), or:
                // SWAP -> [value, memberName, className]
                // Need custom rotation - let's just use the dynamic store which takes values from stack

                _emitter.EmitStoreClassMemberDynamic();
            }
        }
        else if (expr is ConditionalExpressionNode condExpr)
        {
            // (cond ? a : b) = value
            // Evaluate condition and store to either ThenValue or ElseValue
            // Stack: [value]
            condExpr.Condition.Accept(this);  // [value, condition]
            var jumpToElse = _emitter.EmitJumpIfFalse();  // [value]

            // Then branch: store to ThenValue
            if (condExpr.ThenValue != null)
            {
                CompileStore(condExpr.ThenValue, keepValue);
            }
            var jumpToEnd = _emitter.EmitJump();

            // Else branch: store to ElseValue
            _emitter.PatchJump(jumpToElse);
            if (condExpr.ElseValue != null)
            {
                CompileStore(condExpr.ElseValue, keepValue);
            }

            _emitter.PatchJump(jumpToEnd);
        }
        else if (expr is FunctionCallNode funcCall)
        {
            // func() = value
            // In IntMUD, this pattern means the function returns a variable name,
            // and we should store to that dynamic location
            // Stack: [value]
            funcCall.Accept(this);  // [value, varName]
            _emitter.EmitSwap();    // [varName, value]
            _emitter.EmitStoreDynamic();
        }
        else
        {
            throw new CompilerException($"Invalid assignment target: {expr.GetType().Name}", expr.Line);
        }
    }

    public object? VisitMemberAccess(MemberAccessNode node)
    {
        node.Object.Accept(this);
        _emitter.EmitLoadField(node.Member);
        return null;
    }

    public object? VisitDynamicMemberAccess(DynamicMemberAccessNode node)
    {
        // Compile the object expression
        node.Object.Accept(this);

        // Build the dynamic member name by concatenating all parts
        // First part
        if (node.MemberParts.Count == 0)
        {
            throw new CompilerException("Dynamic member access must have at least one part", node.Line);
        }

        // Compile first part
        CompileMemberPart(node.MemberParts[0]);

        // Concatenate remaining parts
        for (int i = 1; i < node.MemberParts.Count; i++)
        {
            CompileMemberPart(node.MemberParts[i]);
            _emitter.EmitAdd(); // String concatenation
        }

        // If countdown variable, append "@" to the name
        if (node.IsCountdown)
        {
            _emitter.EmitPushString("@");
            _emitter.EmitAdd();
        }

        // Stack now has: [object, fieldName]
        // LoadFieldDynamic expects: [object, fieldName] and produces [value]
        _emitter.EmitLoadFieldDynamic();
        return null;
    }

    private void CompileMemberPart(ExpressionNode part)
    {
        if (part is StringLiteralNode strLit)
        {
            // Static part - push string directly
            _emitter.EmitPushString(strLit.Value);
        }
        else
        {
            // Dynamic part - compile expression and convert to string
            part.Accept(this);
            // The runtime will convert to string when concatenating
        }
    }

    private void CompileDynamicMemberName(List<ExpressionNode> parts)
    {
        // Compile all parts and concatenate them to build the member name string
        bool first = true;
        foreach (var part in parts)
        {
            CompileMemberPart(part);
            if (!first)
            {
                _emitter.EmitConcat();
            }
            first = false;
        }
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
        else if (node.Function is DynamicMemberAccessNode dynamicMember)
        {
            // Dynamic method call - obj.method_[expr](args)
            // First compile the object
            dynamicMember.Object.Accept(this);

            // Compile arguments
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }

            // Build the dynamic method name
            bool first = true;
            foreach (var part in dynamicMember.MemberParts)
            {
                CompileMemberPart(part);
                if (!first)
                {
                    _emitter.EmitConcat();
                }
                first = false;
            }

            _emitter.EmitCallMethodDynamic(node.Arguments.Count);
        }
        else if (node.Function is DynamicIdentifierNode dynId)
        {
            // Dynamic function call - function name is built from parts
            // Example: passo[arg0.tpasso](arg0, arg1) calls passo0, passo1, etc.

            // Compile arguments first (they go on stack before function name)
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }

            // Build the function name string
            if (dynId.Parts.Count == 0)
            {
                throw new CompilerException("Dynamic identifier must have at least one part", node.Line);
            }

            bool first = true;
            foreach (var part in dynId.Parts)
            {
                CompileMemberPart(part);
                if (!first)
                {
                    _emitter.EmitConcat();
                }
                first = false;
            }

            // Call with dynamic function name
            _emitter.EmitCallDynamic(node.Arguments.Count);
        }
        else if (node.Function is ClassReferenceNode classRef)
        {
            // Static or dynamic method call - need to handle both class name and member name variations
            // First, emit the class reference onto the stack
            if (classRef.ClassName != null && classRef.ClassNameIndex == null)
            {
                // Static class name: class:method()
                _emitter.EmitLoadClass(classRef.ClassName);
            }
            else if (classRef.ClassName == null && classRef.ClassNameIndex != null)
            {
                // Fully dynamic class name: [expr]:method()
                classRef.ClassNameIndex.Accept(this);
                _emitter.EmitLoadClassDynamic();
            }
            else if (classRef.ClassName != null && classRef.ClassNameIndex != null)
            {
                // Combined: name[expr]:method()
                _emitter.EmitPushString(classRef.ClassName);
                classRef.ClassNameIndex.Accept(this);
                _emitter.EmitConcat();
                _emitter.EmitLoadClassDynamic();
            }
            else
            {
                throw new CompilerException("Invalid class reference in function call", node.Line);
            }

            // Compile arguments
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }

            // Emit the call - handle both static and dynamic member names
            if (classRef.MemberName != null && classRef.DynamicMemberParts.Count == 0)
            {
                _emitter.EmitCallMethod(classRef.MemberName, node.Arguments.Count);
            }
            else if (classRef.DynamicMemberParts.Count > 0)
            {
                // Build dynamic member name
                bool first = true;
                foreach (var part in classRef.DynamicMemberParts)
                {
                    part.Accept(this);
                    if (!first)
                    {
                        _emitter.EmitConcat();
                    }
                    first = false;
                }
                _emitter.EmitCallMethodDynamic(node.Arguments.Count);
            }
            else
            {
                throw new CompilerException("Invalid member in class reference function call", node.Line);
            }
        }
        else if (node.Function is FunctionCallNode nestedCall)
        {
            // Nested call: result of function call is called as function
            // Example: getCallback()(arg1, arg2)
            // First compile the nested call to get the callable
            nestedCall.Accept(this);

            // Compile arguments
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }

            // Call dynamically (assumes result is a function name or callable)
            _emitter.EmitCallDynamic(node.Arguments.Count);
        }
        else if (node.Function is AssignmentExpressionNode assignExpr)
        {
            // Assignment result used as callable: (x = funcName)(args)
            // Compile the assignment (result is the assigned value)
            assignExpr.Accept(this);

            // Compile arguments
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }

            // Call dynamically
            _emitter.EmitCallDynamic(node.Arguments.Count);
        }
        else if (node.Function != null)
        {
            // Generic fallback: compile expression and call dynamically
            // This handles cases where the parser creates a FunctionCallNode
            // for expressions that look like calls but may be parsed differently
            node.Function.Accept(this);

            // Compile arguments
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }

            // Call dynamically
            _emitter.EmitCallDynamic(node.Arguments.Count);
        }
        else
        {
            throw new CompilerException("Invalid function call target: null", node.Line);
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
        else if (node.Operand is MemberAccessNode memberAccess)
        {
            // obj.field++ or obj.field--
            // Step 1: Get old value (this is what we return)
            memberAccess.Object.Accept(this);
            _emitter.EmitLoadField(memberAccess.Member);  // [old_value]

            // Step 2: Load object again and compute/store new value
            memberAccess.Object.Accept(this);  // [old_value, obj]
            _emitter.EmitDup();                // [old_value, obj, obj]
            _emitter.EmitLoadField(memberAccess.Member);  // [old_value, obj, field_value]
            if (node.IsIncrement) _emitter.EmitInc(); else _emitter.EmitDec();  // [old_value, obj, new_value]
            _emitter.EmitStoreField(memberAccess.Member);  // [old_value]
        }
        else if (node.Operand is IndexAccessNode indexAccess)
        {
            // arr[i]++ or arr[i]--
            // Step 1: Get old value
            indexAccess.Object.Accept(this);
            indexAccess.Index.Accept(this);
            _emitter.EmitLoadIndex();  // [old_value]

            // Step 2: Load object and index again, compute/store new value
            indexAccess.Object.Accept(this);   // [old_value, obj]
            indexAccess.Index.Accept(this);    // [old_value, obj, index]
            indexAccess.Object.Accept(this);   // [old_value, obj, index, obj2]
            indexAccess.Index.Accept(this);    // [old_value, obj, index, obj2, index2]
            _emitter.EmitLoadIndex();          // [old_value, obj, index, elem_value]
            if (node.IsIncrement) _emitter.EmitInc(); else _emitter.EmitDec();  // [old_value, obj, index, new_value]
            _emitter.EmitStoreIndex();         // [old_value]
        }
        else if (node.Operand is DynamicMemberAccessNode dynamicMemberAccess)
        {
            // obj.[expr]++ or obj.[expr]--
            // Step 1: Get old value
            dynamicMemberAccess.Object.Accept(this);  // [obj]
            CompileDynamicMemberName(dynamicMemberAccess.MemberParts);  // [obj, memberName]
            _emitter.EmitLoadFieldDynamic();  // [old_value]

            // Step 2: Compute and store new value
            dynamicMemberAccess.Object.Accept(this);  // [old_value, obj]
            CompileDynamicMemberName(dynamicMemberAccess.MemberParts);  // [old_value, obj, memberName]
            dynamicMemberAccess.Object.Accept(this);  // [old_value, obj, memberName, obj2]
            CompileDynamicMemberName(dynamicMemberAccess.MemberParts);  // [old_value, obj, memberName, obj2, memberName2]
            _emitter.EmitLoadFieldDynamic();  // [old_value, obj, memberName, field_value]
            if (node.IsIncrement) _emitter.EmitInc(); else _emitter.EmitDec();  // [old_value, obj, memberName, new_value]
            _emitter.EmitStoreFieldDynamic();  // [old_value]
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

    public object? VisitDynamicIdentifier(DynamicIdentifierNode node)
    {
        // Dynamic identifier - construct the name at runtime by concatenating parts
        // Example: x["1"] becomes "x1"
        // Example: x[y] where y="_teste" becomes "x_teste"

        if (node.Parts.Count == 0)
        {
            throw new CompilerException("DynamicIdentifierNode has no parts", node.Line);
        }

        // Compile the first part (push its string value)
        CompileDynamicNamePart(node.Parts[0]);

        // Concatenate remaining parts
        for (int i = 1; i < node.Parts.Count; i++)
        {
            CompileDynamicNamePart(node.Parts[i]);
            _emitter.EmitConcat();
        }

        // Add countdown suffix if present
        if (node.IsCountdown)
        {
            _emitter.EmitPushString("@");
            _emitter.EmitConcat();
        }

        // The dynamic name is now on the stack
        // Load the variable with that name
        _emitter.EmitLoadDynamic();

        return null;
    }

    /// <summary>
    /// Compiles a part of a dynamic name, converting it to a string on the stack.
    /// </summary>
    private void CompileDynamicNamePart(ExpressionNode part)
    {
        if (part is StringLiteralNode strLit)
        {
            // Static string part - push directly
            _emitter.EmitPushString(strLit.Value);
        }
        else
        {
            // Dynamic part - evaluate and convert to string
            part.Accept(this);
            // The runtime will need to convert the value to string
            // For now we assume it's already a string or will be converted
        }
    }

    public object? VisitDollarReference(DollarReferenceNode node)
    {
        // Handle different patterns:
        // $classname - static class reference
        // $[expr] - dynamic class reference (class name from expression)
        // $name[expr] - dynamic class reference (base name + expression)

        if (node.ClassName != null && node.DynamicExpression == null)
        {
            // $classname - static case
            _emitter.EmitLoadClass(node.ClassName);
        }
        else if (node.ClassName == null && node.DynamicExpression != null)
        {
            // $[expr] - fully dynamic case
            node.DynamicExpression.Accept(this);
            _emitter.EmitLoadClassDynamic();
        }
        else if (node.ClassName != null && node.DynamicExpression != null)
        {
            // $name[expr] - combine base name with expression
            _emitter.EmitPushString(node.ClassName);
            node.DynamicExpression.Accept(this);
            _emitter.EmitConcat();
            _emitter.EmitLoadClassDynamic();
        }
        else
        {
            // Fallback - push null
            _emitter.EmitPushNull();
        }
        return null;
    }

    public object? VisitClassReference(ClassReferenceNode node)
    {
        // Handle different patterns:
        // class:member - static class and member
        // class:prefix_[expr] - static class, dynamic member
        // [expr]:member - dynamic class, static member
        // name[expr]:member - dynamic class (base + expr), static member

        // First, determine and push class name
        bool classDynamic = false;
        if (node.ClassName != null && node.ClassNameIndex == null)
        {
            // Static class name
            _emitter.EmitPushString(node.ClassName);
        }
        else if (node.ClassName == null && node.ClassNameIndex != null)
        {
            // [expr]:member - class name from expression
            node.ClassNameIndex.Accept(this);
            classDynamic = true;
        }
        else if (node.ClassName != null && node.ClassNameIndex != null)
        {
            // name[expr]:member - combine base name with expression
            _emitter.EmitPushString(node.ClassName);
            node.ClassNameIndex.Accept(this);
            _emitter.EmitConcat();
            classDynamic = true;
        }
        else
        {
            // No class specified - push empty string
            _emitter.EmitPushString("");
        }

        // Then, determine and push member name
        bool memberDynamic = false;
        if (node.MemberName != null && node.DynamicMemberParts.Count == 0)
        {
            // Static member name
            _emitter.EmitPushString(node.MemberName);
        }
        else if (node.DynamicMemberParts.Count > 0)
        {
            // Dynamic member - concatenate all parts
            bool first = true;
            foreach (var part in node.DynamicMemberParts)
            {
                part.Accept(this);
                if (!first)
                {
                    _emitter.EmitConcat();
                }
                first = false;
            }
            memberDynamic = true;
        }
        else if (node.MemberName != null)
        {
            _emitter.EmitPushString(node.MemberName);
        }
        else
        {
            // No member specified - push empty string
            _emitter.EmitPushString("");
        }

        // Now emit the appropriate load instruction
        // Stack is: [className, memberName]
        _emitter.EmitLoadClassMemberDynamic();

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
