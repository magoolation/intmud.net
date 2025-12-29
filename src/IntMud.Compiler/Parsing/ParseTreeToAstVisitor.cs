using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using IntMud.Compiler.Ast;

namespace IntMud.Compiler.Parsing;

/// <summary>
/// Converts ANTLR4 parse tree to AST nodes.
/// </summary>
internal class ParseTreeToAstVisitor : IntMudParserBaseVisitor<AstNode>
{
    private readonly string? _sourceFile;

    public ParseTreeToAstVisitor(string? sourceFile)
    {
        _sourceFile = sourceFile;
    }

    private void SetLocation(AstNode node, ParserRuleContext context)
    {
        node.SourceFile = _sourceFile;
        node.Line = context.Start.Line;
        node.Column = context.Start.Column + 1;
    }

    public override AstNode VisitCompilationUnit(IntMudParser.CompilationUnitContext context)
    {
        var unit = new CompilationUnitNode();
        SetLocation(unit, context);

        foreach (var option in context.fileOption())
        {
            unit.Options.Add((FileOptionNode)Visit(option));
        }

        foreach (var classDef in context.classDefinition())
        {
            unit.Classes.Add((ClassDefinitionNode)Visit(classDef));
        }

        return unit;
    }

    public override AstNode VisitFileOption(IntMudParser.FileOptionContext context)
    {
        var name = context.GetChild(0).GetText();
        var value = context.GetChild(2).GetText();

        // Remove quotes from string values
        if (value.StartsWith('"') && value.EndsWith('"'))
            value = value[1..^1];

        var node = new FileOptionNode { Name = name, Value = value };
        SetLocation(node, context);
        return node;
    }

    public override AstNode VisitClassDefinition(IntMudParser.ClassDefinitionContext context)
    {
        var name = GetIdentifier(context.identifier());
        var node = new ClassDefinitionNode { Name = name };
        SetLocation(node, context);

        // Handle inheritance
        var inherit = context.inheritClause();
        if (inherit != null)
        {
            foreach (var id in inherit.identifierList().identifier())
            {
                node.BaseClasses.Add(GetIdentifier(id));
            }
        }

        // Handle members
        foreach (var member in context.classMember())
        {
            var memberNode = Visit(member);
            if (memberNode != null)
                node.Members.Add(memberNode);
        }

        return node;
    }

    public override AstNode VisitVariableDeclaration(IntMudParser.VariableDeclarationContext context)
    {
        var modifiers = VariableModifiers.None;
        foreach (var mod in context.modifier())
        {
            if (mod.COMUM() != null) modifiers |= VariableModifiers.Comum;
            if (mod.SAV() != null) modifiers |= VariableModifiers.Sav;
        }

        var typeName = context.variableType().GetText();
        var name = GetIdentifier(context.identifier());

        var node = new VariableDeclarationNode
        {
            Modifiers = modifiers,
            TypeName = typeName,
            Name = name
        };
        SetLocation(node, context);

        // Vector size
        var vectorSize = context.vectorSize();
        if (vectorSize != null)
        {
            node.VectorSize = int.Parse(vectorSize.DECIMAL_NUMBER().GetText());
        }

        // Initializer
        var expr = context.expression();
        if (expr != null)
        {
            node.Initializer = VisitExpression(expr);
        }

        return node;
    }

    public override AstNode VisitFunctionDefinition(IntMudParser.FunctionDefinitionContext context)
    {
        var name = GetIdentifier(context.identifier());
        var node = new FunctionDefinitionNode { Name = name };
        SetLocation(node, context);

        foreach (var stmt in context.statement())
        {
            var stmtNode = VisitStatement(stmt);
            if (stmtNode != null)
                node.Body.Add(stmtNode);
        }

        return node;
    }

    public override AstNode VisitVarFuncDefinition(IntMudParser.VarFuncDefinitionContext context)
    {
        var name = GetIdentifier(context.identifier());
        var node = new VarFuncDefinitionNode { Name = name };
        SetLocation(node, context);

        foreach (var stmt in context.statement())
        {
            var stmtNode = VisitStatement(stmt);
            if (stmtNode != null)
                node.Body.Add(stmtNode);
        }

        return node;
    }

    public override AstNode VisitConstantDefinition(IntMudParser.ConstantDefinitionContext context)
    {
        var name = GetIdentifier(context.identifier());
        var value = VisitExpression(context.expression());
        var node = new ConstantDefinitionNode { Name = name, Value = value };
        SetLocation(node, context);
        return node;
    }

    public override AstNode VisitVarConstDefinition(IntMudParser.VarConstDefinitionContext context)
    {
        var name = GetIdentifier(context.identifier());
        var value = VisitExpression(context.expression());
        var node = new VarConstDefinitionNode { Name = name, Value = value };
        SetLocation(node, context);
        return node;
    }

    private StatementNode? VisitStatement(IntMudParser.StatementContext context)
    {
        // Handle variable declarations inside statements by creating LocalVariableDeclarationNode
        var varDecl = context.variableDeclaration();
        if (varDecl != null)
        {
            return VisitLocalVariableDeclaration(varDecl);
        }

        return Visit(context) as StatementNode;
    }

    private LocalVariableDeclarationNode VisitLocalVariableDeclaration(IntMudParser.VariableDeclarationContext context)
    {
        var modifiers = VariableModifiers.None;
        foreach (var mod in context.modifier())
        {
            if (mod.COMUM() != null) modifiers |= VariableModifiers.Comum;
            if (mod.SAV() != null) modifiers |= VariableModifiers.Sav;
        }

        var typeName = context.variableType().GetText();
        var name = GetIdentifier(context.identifier());

        var node = new LocalVariableDeclarationNode
        {
            Modifiers = modifiers,
            TypeName = typeName,
            Name = name
        };
        SetLocation(node, context);

        // Vector size
        var vectorSize = context.vectorSize();
        if (vectorSize != null)
        {
            node.VectorSize = int.Parse(vectorSize.DECIMAL_NUMBER().GetText());
        }

        // Initializer
        var expr = context.expression();
        if (expr != null)
        {
            node.Initializer = VisitExpression(expr);
        }

        return node;
    }

    public override AstNode VisitRefVarDeclaration(IntMudParser.RefVarDeclarationContext context)
    {
        var name = GetIdentifier(context.identifier());
        var value = VisitExpression(context.expression());
        var node = new RefVarDeclarationNode { Name = name, Value = value };
        SetLocation(node, context);
        return node;
    }

    public override AstNode VisitIfStatement(IntMudParser.IfStatementContext context)
    {
        var condition = VisitExpression(context.expression());
        var node = new IfStatementNode { Condition = condition };
        SetLocation(node, context);

        // Then body
        foreach (var stmt in context.statement())
        {
            var stmtNode = VisitStatement(stmt);
            if (stmtNode != null)
                node.ThenBody.Add(stmtNode);
        }

        // Else-if clauses
        foreach (var elseIf in context.elseIfClause())
        {
            var clause = new ElseIfClause
            {
                Condition = VisitExpression(elseIf.expression())
            };
            foreach (var stmt in elseIf.statement())
            {
                var stmtNode = VisitStatement(stmt);
                if (stmtNode != null)
                    clause.Body.Add(stmtNode);
            }
            node.ElseIfClauses.Add(clause);
        }

        // Else clause
        var elseClause = context.elseClause();
        if (elseClause != null)
        {
            foreach (var stmt in elseClause.statement())
            {
                var stmtNode = VisitStatement(stmt);
                if (stmtNode != null)
                    node.ElseBody.Add(stmtNode);
            }
        }

        return node;
    }

    public override AstNode VisitWhileStatement(IntMudParser.WhileStatementContext context)
    {
        var condition = VisitExpression(context.expression());
        var node = new WhileStatementNode { Condition = condition };
        SetLocation(node, context);

        foreach (var stmt in context.statement())
        {
            var stmtNode = VisitStatement(stmt);
            if (stmtNode != null)
                node.Body.Add(stmtNode);
        }

        return node;
    }

    public override AstNode VisitForStatement(IntMudParser.ForStatementContext context)
    {
        var expressions = context.expression();
        var node = new ForStatementNode
        {
            Initializer = VisitExpression(expressions[0]),
            Condition = VisitExpression(expressions[1]),
            Increment = VisitExpression(expressions[2])
        };
        SetLocation(node, context);

        foreach (var stmt in context.statement())
        {
            var stmtNode = VisitStatement(stmt);
            if (stmtNode != null)
                node.Body.Add(stmtNode);
        }

        return node;
    }

    public override AstNode VisitForeachStatement(IntMudParser.ForeachStatementContext context)
    {
        var variableName = context.identifier().GetText();
        var collection = VisitExpression(context.expression());
        var node = new ForEachStatementNode
        {
            VariableName = variableName,
            Collection = collection
        };
        SetLocation(node, context);

        foreach (var stmt in context.statement())
        {
            var stmtNode = VisitStatement(stmt);
            if (stmtNode != null)
                node.Body.Add(stmtNode);
        }

        return node;
    }

    public override AstNode VisitSwitchStatement(IntMudParser.SwitchStatementContext context)
    {
        var expr = VisitExpression(context.expression());
        var node = new SwitchStatementNode { Expression = expr };
        SetLocation(node, context);

        foreach (var caseClause in context.caseClause())
        {
            var clause = (CaseClauseNode)Visit(caseClause);
            node.Cases.Add(clause);
        }

        var defaultClause = context.defaultClause();
        if (defaultClause != null)
        {
            var clause = new CaseClauseNode { Label = null };
            foreach (var stmt in defaultClause.statement())
            {
                var stmtNode = VisitStatement(stmt);
                if (stmtNode != null)
                    clause.Body.Add(stmtNode);
            }
            node.DefaultCase = clause;
        }

        return node;
    }

    public override AstNode VisitCaseClause(IntMudParser.CaseClauseContext context)
    {
        var label = context.STRING().GetText();
        // Remove quotes
        label = label[1..^1];

        var node = new CaseClauseNode { Label = label };
        SetLocation(node, context);

        foreach (var stmt in context.statement())
        {
            var stmtNode = VisitStatement(stmt);
            if (stmtNode != null)
                node.Body.Add(stmtNode);
        }

        return node;
    }

    public override AstNode VisitReturnStatement(IntMudParser.ReturnStatementContext context)
    {
        var node = new ReturnStatementNode();
        SetLocation(node, context);

        var expr = context.expression();
        if (expr != null)
            node.Value = VisitExpression(expr);

        return node;
    }

    public override AstNode VisitExitStatement(IntMudParser.ExitStatementContext context)
    {
        var node = new ExitStatementNode();
        SetLocation(node, context);

        var expr = context.expression();
        if (expr != null)
            node.Condition = VisitExpression(expr);

        return node;
    }

    public override AstNode VisitContinueStatement(IntMudParser.ContinueStatementContext context)
    {
        var node = new ContinueStatementNode();
        SetLocation(node, context);

        var expr = context.expression();
        if (expr != null)
            node.Condition = VisitExpression(expr);

        return node;
    }

    public override AstNode VisitTerminateStatement(IntMudParser.TerminateStatementContext context)
    {
        var node = new TerminateStatementNode();
        SetLocation(node, context);
        return node;
    }

    public override AstNode VisitExpressionStatement(IntMudParser.ExpressionStatementContext context)
    {
        var node = new ExpressionStatementNode();
        SetLocation(node, context);

        foreach (var expr in context.expression())
        {
            node.Expressions.Add(VisitExpression(expr));
        }

        return node;
    }

    public ExpressionNode VisitExpression(IntMudParser.ExpressionContext context)
    {
        return VisitAssignmentExpression(context.assignmentExpression());
    }

    private ExpressionNode VisitAssignmentExpression(IntMudParser.AssignmentExpressionContext context)
    {
        var conditional = context.conditionalExpression();
        if (conditional != null)
            return VisitConditionalExpression(conditional);

        // Assignment
        var left = VisitPostfixExpression(context.leftHandSide().postfixExpression());
        var op = GetAssignmentOperator(context.assignmentOperator());
        var right = VisitAssignmentExpression(context.assignmentExpression());

        var node = new AssignmentExpressionNode
        {
            Left = left,
            Operator = op,
            Right = right
        };
        SetLocation(node, context);
        return node;
    }

    private ExpressionNode VisitConditionalExpression(IntMudParser.ConditionalExpressionContext context)
    {
        var nullCoalesce = VisitNullCoalesceExpression(context.nullCoalesceExpression());

        if (context.QUESTION() == null)
            return nullCoalesce;

        var node = new ConditionalExpressionNode { Condition = nullCoalesce };
        SetLocation(node, context);

        var expressions = context.expression();
        if (expressions.Length > 0)
            node.ThenValue = VisitExpression(expressions[0]);
        if (expressions.Length > 1)
            node.ElseValue = VisitExpression(expressions[1]);

        return node;
    }

    private ExpressionNode VisitNullCoalesceExpression(IntMudParser.NullCoalesceExpressionContext context)
    {
        var logicalOrs = context.logicalOrExpression();
        var result = VisitLogicalOrExpression(logicalOrs[0]);

        for (int i = 1; i < logicalOrs.Length; i++)
        {
            var right = VisitLogicalOrExpression(logicalOrs[i]);
            result = new NullCoalesceExpressionNode { Left = result, Right = right };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitLogicalOrExpression(IntMudParser.LogicalOrExpressionContext context)
    {
        var logicalAnds = context.logicalAndExpression();
        var result = VisitLogicalAndExpression(logicalAnds[0]);

        for (int i = 1; i < logicalAnds.Length; i++)
        {
            var right = VisitLogicalAndExpression(logicalAnds[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = BinaryOperator.Or,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitLogicalAndExpression(IntMudParser.LogicalAndExpressionContext context)
    {
        var bitwiseOrs = context.bitwiseOrExpression();
        var result = VisitBitwiseOrExpression(bitwiseOrs[0]);

        for (int i = 1; i < bitwiseOrs.Length; i++)
        {
            var right = VisitBitwiseOrExpression(bitwiseOrs[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = BinaryOperator.And,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitBitwiseOrExpression(IntMudParser.BitwiseOrExpressionContext context)
    {
        var bitwiseXors = context.bitwiseXorExpression();
        var result = VisitBitwiseXorExpression(bitwiseXors[0]);

        for (int i = 1; i < bitwiseXors.Length; i++)
        {
            var right = VisitBitwiseXorExpression(bitwiseXors[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = BinaryOperator.BitwiseOr,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitBitwiseXorExpression(IntMudParser.BitwiseXorExpressionContext context)
    {
        var bitwiseAnds = context.bitwiseAndExpression();
        var result = VisitBitwiseAndExpression(bitwiseAnds[0]);

        for (int i = 1; i < bitwiseAnds.Length; i++)
        {
            var right = VisitBitwiseAndExpression(bitwiseAnds[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = BinaryOperator.BitwiseXor,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitBitwiseAndExpression(IntMudParser.BitwiseAndExpressionContext context)
    {
        var equalities = context.equalityExpression();
        var result = VisitEqualityExpression(equalities[0]);

        for (int i = 1; i < equalities.Length; i++)
        {
            var right = VisitEqualityExpression(equalities[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = BinaryOperator.BitwiseAnd,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitEqualityExpression(IntMudParser.EqualityExpressionContext context)
    {
        var relationals = context.relationalExpression();
        var result = VisitRelationalExpression(relationals[0]);

        for (int i = 1; i < relationals.Length; i++)
        {
            var opToken = context.GetChild(i * 2 - 1);
            var op = opToken.GetText() switch
            {
                "==" => BinaryOperator.Equal,
                "===" => BinaryOperator.StrictEqual,
                "!=" => BinaryOperator.NotEqual,
                "!==" => BinaryOperator.StrictNotEqual,
                _ => throw new InvalidOperationException($"Unknown operator: {opToken.GetText()}")
            };

            var right = VisitRelationalExpression(relationals[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = op,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitRelationalExpression(IntMudParser.RelationalExpressionContext context)
    {
        var shifts = context.shiftExpression();
        var result = VisitShiftExpression(shifts[0]);

        for (int i = 1; i < shifts.Length; i++)
        {
            var opToken = context.GetChild(i * 2 - 1);
            var op = opToken.GetText() switch
            {
                "<" => BinaryOperator.LessThan,
                "<=" => BinaryOperator.LessOrEqual,
                ">" => BinaryOperator.GreaterThan,
                ">=" => BinaryOperator.GreaterOrEqual,
                _ => throw new InvalidOperationException($"Unknown operator: {opToken.GetText()}")
            };

            var right = VisitShiftExpression(shifts[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = op,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitShiftExpression(IntMudParser.ShiftExpressionContext context)
    {
        var additives = context.additiveExpression();
        var result = VisitAdditiveExpression(additives[0]);

        for (int i = 1; i < additives.Length; i++)
        {
            var opToken = context.GetChild(i * 2 - 1);
            var op = opToken.GetText() switch
            {
                "<<" => BinaryOperator.ShiftLeft,
                ">>" => BinaryOperator.ShiftRight,
                _ => throw new InvalidOperationException($"Unknown operator: {opToken.GetText()}")
            };

            var right = VisitAdditiveExpression(additives[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = op,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitAdditiveExpression(IntMudParser.AdditiveExpressionContext context)
    {
        var multiplicatives = context.multiplicativeExpression();
        var result = VisitMultiplicativeExpression(multiplicatives[0]);

        for (int i = 1; i < multiplicatives.Length; i++)
        {
            var opToken = context.GetChild(i * 2 - 1);
            var op = opToken.GetText() switch
            {
                "+" => BinaryOperator.Add,
                "-" => BinaryOperator.Subtract,
                _ => throw new InvalidOperationException($"Unknown operator: {opToken.GetText()}")
            };

            var right = VisitMultiplicativeExpression(multiplicatives[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = op,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitMultiplicativeExpression(IntMudParser.MultiplicativeExpressionContext context)
    {
        var unaries = context.unaryExpression();
        var result = VisitUnaryExpression(unaries[0]);

        for (int i = 1; i < unaries.Length; i++)
        {
            var opToken = context.GetChild(i * 2 - 1);
            var op = opToken.GetText() switch
            {
                "*" => BinaryOperator.Multiply,
                "/" => BinaryOperator.Divide,
                "%" => BinaryOperator.Modulo,
                _ => throw new InvalidOperationException($"Unknown operator: {opToken.GetText()}")
            };

            var right = VisitUnaryExpression(unaries[i]);
            result = new BinaryExpressionNode
            {
                Left = result,
                Operator = op,
                Right = right
            };
            SetLocation(result, context);
        }

        return result;
    }

    private ExpressionNode VisitUnaryExpression(IntMudParser.UnaryExpressionContext context)
    {
        if (context.PLUSPLUS() != null)
        {
            var operand = VisitUnaryExpression(context.unaryExpression());
            var node = new UnaryExpressionNode
            {
                Operator = UnaryOperator.PreIncrement,
                Operand = operand
            };
            SetLocation(node, context);
            return node;
        }

        if (context.MINUSMINUS() != null)
        {
            var operand = VisitUnaryExpression(context.unaryExpression());
            var node = new UnaryExpressionNode
            {
                Operator = UnaryOperator.PreDecrement,
                Operand = operand
            };
            SetLocation(node, context);
            return node;
        }

        if (context.MINUS() != null)
        {
            var operand = VisitUnaryExpression(context.unaryExpression());
            var node = new UnaryExpressionNode
            {
                Operator = UnaryOperator.Negate,
                Operand = operand
            };
            SetLocation(node, context);
            return node;
        }

        if (context.NOT() != null)
        {
            var operand = VisitUnaryExpression(context.unaryExpression());
            var node = new UnaryExpressionNode
            {
                Operator = UnaryOperator.Not,
                Operand = operand
            };
            SetLocation(node, context);
            return node;
        }

        if (context.TILDE() != null)
        {
            var operand = VisitUnaryExpression(context.unaryExpression());
            var node = new UnaryExpressionNode
            {
                Operator = UnaryOperator.BitwiseNot,
                Operand = operand
            };
            SetLocation(node, context);
            return node;
        }

        return VisitPostfixExpression(context.postfixExpression());
    }

    private ExpressionNode VisitPostfixExpression(IntMudParser.PostfixExpressionContext context)
    {
        var result = VisitPrimaryExpression(context.primaryExpression());

        foreach (var postfix in context.postfixOp())
        {
            result = ApplyPostfixOp(result, postfix);
        }

        return result;
    }

    private ExpressionNode ApplyPostfixOp(ExpressionNode expr, IntMudParser.PostfixOpContext context)
    {
        if (context.PLUSPLUS() != null)
        {
            var node = new PostfixIncrementNode { Operand = expr, IsIncrement = true };
            SetLocation(node, context);
            return node;
        }

        if (context.MINUSMINUS() != null)
        {
            var node = new PostfixIncrementNode { Operand = expr, IsIncrement = false };
            SetLocation(node, context);
            return node;
        }

        if (context.DOT() != null)
        {
            var identifier = context.identifier();
            if (identifier != null)
            {
                var memberName = GetIdentifier(identifier);
                ExpressionNode member = new MemberAccessNode { Object = expr, Member = memberName };
                SetLocation(member, context);

                // Check for function call
                var args = context.arguments();
                if (args != null)
                {
                    var call = new FunctionCallNode { Function = member };
                    SetLocation(call, context);

                    var argList = args.argumentList();
                    if (argList != null)
                    {
                        foreach (var argExpr in argList.expression())
                        {
                            call.Arguments.Add(VisitExpression(argExpr));
                        }
                    }
                    return call;
                }

                return member;
            }

            // Vector index access (e.g., v.0, v.1)
            var number = context.DECIMAL_NUMBER();
            if (number != null)
            {
                var index = new NumericLiteralNode
                {
                    Value = double.Parse(number.GetText()),
                    IsInteger = true
                };
                SetLocation(index, context);

                var node = new MemberAccessNode { Object = expr, Member = number.GetText() };
                SetLocation(node, context);
                return node;
            }
        }

        if (context.LBRACKET() != null)
        {
            var indexExpr = VisitExpression(context.expression());
            var node = new IndexAccessNode { Object = expr, Index = indexExpr };
            SetLocation(node, context);
            return node;
        }

        if (context.arguments() != null)
        {
            var call = new FunctionCallNode { Function = expr };
            SetLocation(call, context);

            var argList = context.arguments().argumentList();
            if (argList != null)
            {
                foreach (var argExpr in argList.expression())
                {
                    call.Arguments.Add(VisitExpression(argExpr));
                }
            }
            return call;
        }

        throw new InvalidOperationException("Unknown postfix operator");
    }

    private ExpressionNode VisitPrimaryExpression(IntMudParser.PrimaryExpressionContext context)
    {
        if (context.NULO() != null)
        {
            var node = new NullLiteralNode();
            SetLocation(node, context);
            return node;
        }

        if (context.ESTE() != null)
        {
            var node = new ThisReferenceNode();
            SetLocation(node, context);
            return node;
        }

        if (context.ARG() != null)
        {
            var text = context.ARG().GetText();
            var index = int.Parse(text[3..]);
            var node = new ArgReferenceNode { Index = index };
            SetLocation(node, context);
            return node;
        }

        if (context.ARGS() != null)
        {
            var node = new ArgsReferenceNode();
            SetLocation(node, context);
            return node;
        }

        if (context.DECIMAL_NUMBER() != null)
        {
            var text = context.DECIMAL_NUMBER().GetText();
            var value = double.Parse(text);
            var node = new NumericLiteralNode
            {
                Value = value,
                IsInteger = !text.Contains('.')
            };
            SetLocation(node, context);
            return node;
        }

        if (context.HEX_NUMBER() != null)
        {
            var text = context.HEX_NUMBER().GetText();
            var isNegative = text.StartsWith('-');
            var hexPart = isNegative ? text[3..] : text[2..];
            var value = Convert.ToInt64(hexPart, 16);
            if (isNegative) value = -value;

            var node = new NumericLiteralNode
            {
                Value = value,
                IsHexadecimal = true,
                IsInteger = true
            };
            SetLocation(node, context);
            return node;
        }

        if (context.STRING() != null)
        {
            var text = context.STRING().GetText();
            // Remove quotes and process escape sequences
            var value = ProcessString(text[1..^1]);
            var node = new StringLiteralNode { Value = value };
            SetLocation(node, context);
            return node;
        }

        if (context.LPAREN() != null)
        {
            return VisitExpression(context.expression());
        }

        if (context.classReference() != null)
        {
            var classRef = context.classReference();
            var className = GetIdentifier(classRef.identifier()[0]);
            var memberName = GetIdentifier(classRef.identifier()[1]);
            var node = new ClassReferenceNode { ClassName = className, MemberName = memberName };
            SetLocation(node, context);
            return node;
        }

        if (context.dollarReference() != null)
        {
            var dollarRef = context.dollarReference();
            var className = GetIdentifier(dollarRef.identifier());
            var node = new DollarReferenceNode { ClassName = className };
            SetLocation(node, context);
            return node;
        }

        if (context.newExpression() != null)
        {
            var newExpr = context.newExpression();
            var className = GetIdentifier(newExpr.identifier());
            var node = new NewExpressionNode { ClassName = className };
            SetLocation(node, context);

            // Handle constructor arguments
            var args = newExpr.arguments();
            if (args != null)
            {
                var argList = args.argumentList();
                if (argList != null)
                {
                    foreach (var argExpr in argList.expression())
                    {
                        node.Arguments.Add(VisitExpression(argExpr));
                    }
                }
            }
            return node;
        }

        if (context.deleteExpression() != null)
        {
            var deleteExpr = context.deleteExpression();
            var operand = VisitExpression(deleteExpr.expression());
            var node = new DeleteExpressionNode { Operand = operand };
            SetLocation(node, context);
            return node;
        }

        if (context.identifier() != null)
        {
            var name = GetIdentifier(context.identifier());
            var node = new IdentifierNode { Name = name };
            SetLocation(node, context);
            return node;
        }

        throw new InvalidOperationException("Unknown primary expression");
    }

    private static string GetIdentifier(IntMudParser.IdentifierContext context)
    {
        if (context.IDENTIFIER() != null)
            return context.IDENTIFIER().GetText();
        if (context.contextualKeyword() != null)
            return context.contextualKeyword().GetText();
        throw new InvalidOperationException("Invalid identifier");
    }

    private static AssignmentOperator GetAssignmentOperator(IntMudParser.AssignmentOperatorContext context)
    {
        if (context.EQ() != null) return AssignmentOperator.Assign;
        if (context.PLUSEQ() != null) return AssignmentOperator.AddAssign;
        if (context.MINUSEQ() != null) return AssignmentOperator.SubtractAssign;
        if (context.STAREQ() != null) return AssignmentOperator.MultiplyAssign;
        if (context.SLASHEQ() != null) return AssignmentOperator.DivideAssign;
        if (context.PERCENTEQ() != null) return AssignmentOperator.ModuloAssign;
        if (context.SHLEQ() != null) return AssignmentOperator.ShiftLeftAssign;
        if (context.SHREQ() != null) return AssignmentOperator.ShiftRightAssign;
        if (context.AMPEQ() != null) return AssignmentOperator.BitwiseAndAssign;
        if (context.PIPEEQ() != null) return AssignmentOperator.BitwiseOrAssign;
        if (context.CARETEQ() != null) return AssignmentOperator.BitwiseXorAssign;
        throw new InvalidOperationException("Unknown assignment operator");
    }

    private static string ProcessString(string input)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '\\' && i + 1 < input.Length)
            {
                i++;
                result.Append(input[i] switch
                {
                    'n' => '\n',
                    '"' => '"',
                    '\\' => '\\',
                    'b' => '\x1B',  // ANSI escape
                    'c' => '\x1B',  // Color code marker
                    'd' => '\x1B',  // Background color marker
                    _ => $"\\{input[i]}"  // Keep unknown escapes
                });
            }
            else
            {
                result.Append(input[i]);
            }
        }
        return result.ToString();
    }
}
