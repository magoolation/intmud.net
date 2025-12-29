using IntMud.Compiler.Ast;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Execution;

/// <summary>
/// Interprets and executes IntMUD AST nodes.
/// </summary>
public class AstInterpreter
{
    private readonly ExecutionContext _context;
    private readonly IBuiltinFunctionProvider? _builtins;

    public AstInterpreter(ExecutionContext context, IBuiltinFunctionProvider? builtins = null)
    {
        _context = context;
        _builtins = builtins;
    }

    /// <summary>
    /// Execute a list of statements.
    /// </summary>
    public ExecutionResult ExecuteStatements(IEnumerable<StatementNode> statements)
    {
        foreach (var statement in statements)
        {
            _context.IncrementInstructionCount();

            if (_context.IsTerminated)
                return ExecutionResult.Terminate;

            var result = ExecuteStatement(statement);
            if (!result.IsNormal)
                return result;
        }

        return ExecutionResult.None;
    }

    /// <summary>
    /// Execute a single statement.
    /// </summary>
    public ExecutionResult ExecuteStatement(StatementNode statement)
    {
        return statement switch
        {
            LocalVariableDeclarationNode varDecl => ExecuteLocalVariableDeclaration(varDecl),
            RefVarDeclarationNode refVar => ExecuteRefVarDeclaration(refVar),
            IfStatementNode ifStmt => ExecuteIfStatement(ifStmt),
            WhileStatementNode whileStmt => ExecuteWhileStatement(whileStmt),
            ForStatementNode forStmt => ExecuteForStatement(forStmt),
            SwitchStatementNode switchStmt => ExecuteSwitchStatement(switchStmt),
            ReturnStatementNode returnStmt => ExecuteReturnStatement(returnStmt),
            ExitStatementNode exitStmt => ExecuteExitStatement(exitStmt),
            ContinueStatementNode continueStmt => ExecuteContinueStatement(continueStmt),
            TerminateStatementNode => ExecuteTerminateStatement(),
            ExpressionStatementNode exprStmt => ExecuteExpressionStatement(exprStmt),
            _ => throw new ExecutionException($"Unknown statement type: {statement.GetType().Name}",
                statement.SourceFile, statement.Line, statement.Column)
        };
    }

    private ExecutionResult ExecuteLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        var value = node.Initializer != null
            ? EvaluateExpression(node.Initializer)
            : RuntimeValue.Null;

        _context.DeclareLocalVariable(node.Name, value);
        return ExecutionResult.None;
    }

    private ExecutionResult ExecuteRefVarDeclaration(RefVarDeclarationNode node)
    {
        var value = EvaluateExpression(node.Value);
        _context.DeclareLocalVariable(node.Name, value);
        return ExecutionResult.None;
    }

    private ExecutionResult ExecuteIfStatement(IfStatementNode node)
    {
        // Check main condition
        var condition = EvaluateExpression(node.Condition);
        if (condition.IsTruthy)
        {
            using var scope = _context.PushScope();
            return ExecuteStatements(node.ThenBody);
        }

        // Check else-if clauses
        foreach (var elseIf in node.ElseIfClauses)
        {
            var elseIfCondition = EvaluateExpression(elseIf.Condition);
            if (elseIfCondition.IsTruthy)
            {
                using var scope = _context.PushScope();
                return ExecuteStatements(elseIf.Body);
            }
        }

        // Execute else body
        if (node.ElseBody.Count > 0)
        {
            using var scope = _context.PushScope();
            return ExecuteStatements(node.ElseBody);
        }

        return ExecutionResult.None;
    }

    private ExecutionResult ExecuteWhileStatement(WhileStatementNode node)
    {
        while (true)
        {
            _context.IncrementInstructionCount();

            var condition = EvaluateExpression(node.Condition);
            if (!condition.IsTruthy)
                break;

            using var scope = _context.PushScope();
            var result = ExecuteStatements(node.Body);

            if (result.IsExit)
                break;
            if (result.IsContinue)
                continue;
            if (result.IsReturn || result.IsTerminate)
                return result;
        }

        return ExecutionResult.None;
    }

    private ExecutionResult ExecuteForStatement(ForStatementNode node)
    {
        // Execute initializer
        EvaluateExpression(node.Initializer);

        while (true)
        {
            _context.IncrementInstructionCount();

            // Check condition
            var condition = EvaluateExpression(node.Condition);
            if (!condition.IsTruthy)
                break;

            // Execute body
            using var scope = _context.PushScope();
            var result = ExecuteStatements(node.Body);

            if (result.IsExit)
                break;
            if (result.IsContinue)
            {
                EvaluateExpression(node.Increment);
                continue;
            }
            if (result.IsReturn || result.IsTerminate)
                return result;

            // Execute increment
            EvaluateExpression(node.Increment);
        }

        return ExecutionResult.None;
    }

    private ExecutionResult ExecuteSwitchStatement(SwitchStatementNode node)
    {
        var value = EvaluateExpression(node.Expression).AsString();
        var found = false;
        var executeRest = false;

        foreach (var caseClause in node.Cases)
        {
            if (!executeRest && caseClause.Label != null && !string.Equals(value, caseClause.Label, StringComparison.Ordinal))
                continue;

            found = true;
            executeRest = true;  // Fall through behavior

            using var scope = _context.PushScope();
            var result = ExecuteStatements(caseClause.Body);

            if (result.IsExit)
                return ExecutionResult.None;  // Exit just exits the switch
            if (result.IsReturn || result.IsTerminate)
                return result;
        }

        // Execute default case
        if (!found && node.DefaultCase != null)
        {
            using var scope = _context.PushScope();
            var result = ExecuteStatements(node.DefaultCase.Body);

            if (result.IsReturn || result.IsTerminate)
                return result;
        }

        return ExecutionResult.None;
    }

    private ExecutionResult ExecuteReturnStatement(ReturnStatementNode node)
    {
        var value = node.Value != null
            ? EvaluateExpression(node.Value)
            : RuntimeValue.Null;
        return ExecutionResult.Return(value);
    }

    private ExecutionResult ExecuteExitStatement(ExitStatementNode node)
    {
        if (node.Condition != null)
        {
            var condition = EvaluateExpression(node.Condition);
            if (!condition.IsTruthy)
                return ExecutionResult.None;
        }
        return ExecutionResult.Exit;
    }

    private ExecutionResult ExecuteContinueStatement(ContinueStatementNode node)
    {
        if (node.Condition != null)
        {
            var condition = EvaluateExpression(node.Condition);
            if (!condition.IsTruthy)
                return ExecutionResult.None;
        }
        return ExecutionResult.Continue;
    }

    private ExecutionResult ExecuteTerminateStatement()
    {
        _context.Terminate();
        return ExecutionResult.Terminate;
    }

    private ExecutionResult ExecuteExpressionStatement(ExpressionStatementNode node)
    {
        foreach (var expr in node.Expressions)
        {
            EvaluateExpression(expr);
        }
        return ExecutionResult.None;
    }

    /// <summary>
    /// Evaluate an expression and return its value.
    /// </summary>
    public RuntimeValue EvaluateExpression(ExpressionNode expression)
    {
        _context.IncrementInstructionCount();

        return expression switch
        {
            NullLiteralNode => RuntimeValue.Null,
            NumericLiteralNode num => EvaluateNumericLiteral(num),
            StringLiteralNode str => RuntimeValue.FromString(str.Value),
            IdentifierNode id => EvaluateIdentifier(id),
            ThisReferenceNode => EvaluateThis(),
            ArgReferenceNode arg => _context.GetArgument(arg.Index),
            ArgsReferenceNode => RuntimeValue.FromInt(_context.ArgumentCount),
            DollarReferenceNode dollar => EvaluateDollarReference(dollar),
            ClassReferenceNode classRef => EvaluateClassReference(classRef),
            BinaryExpressionNode binary => EvaluateBinaryExpression(binary),
            UnaryExpressionNode unary => EvaluateUnaryExpression(unary),
            ConditionalExpressionNode cond => EvaluateConditionalExpression(cond),
            NullCoalesceExpressionNode nullCoalesce => EvaluateNullCoalesceExpression(nullCoalesce),
            AssignmentExpressionNode assign => EvaluateAssignmentExpression(assign),
            MemberAccessNode member => EvaluateMemberAccess(member),
            IndexAccessNode index => EvaluateIndexAccess(index),
            FunctionCallNode call => EvaluateFunctionCall(call),
            PostfixIncrementNode postfix => EvaluatePostfixIncrement(postfix),
            _ => throw new ExecutionException($"Unknown expression type: {expression.GetType().Name}",
                expression.SourceFile, expression.Line, expression.Column)
        };
    }

    private RuntimeValue EvaluateNumericLiteral(NumericLiteralNode node)
    {
        if (node.IsInteger)
            return RuntimeValue.FromInt((long)node.Value);
        return RuntimeValue.FromDouble(node.Value);
    }

    private RuntimeValue EvaluateIdentifier(IdentifierNode node)
    {
        // Check local variables first
        if (_context.HasLocalVariable(node.Name))
            return _context.GetLocalVariable(node.Name);

        // Check object variables
        if (_context.CurrentObject is RuntimeObject obj)
        {
            var variable = obj.Class.LookupVariable(node.Name);
            if (variable != null)
            {
                if (variable.IsComum)
                {
                    // Comum variables are shared, stored on class
                    // For now, return object's value
                }
                return obj.GetVariable(node.Name);
            }

            // Check for function (call without arguments)
            var func = obj.Class.LookupFunction(node.Name);
            if (func != null)
            {
                return CallFunction(obj, func, Array.Empty<RuntimeValue>());
            }

            // Check for constant
            var constant = obj.Class.LookupMember(node.Name) as CompiledConstant;
            if (constant != null)
            {
                return EvaluateExpression(constant.Value);
            }
        }

        // Check builtin functions
        if (_builtins != null && _builtins.HasFunction(node.Name))
        {
            return _builtins.Call(node.Name, Array.Empty<RuntimeValue>(), _context);
        }

        return RuntimeValue.Null;
    }

    private RuntimeValue EvaluateThis()
    {
        if (_context.CurrentObject is RuntimeObject obj)
            return RuntimeValue.FromObject(obj);
        return RuntimeValue.Null;
    }

    private RuntimeValue EvaluateDollarReference(DollarReferenceNode node)
    {
        var obj = _context.ClassRegistry?.GetFirstObject(node.ClassName);
        if (obj != null)
            return RuntimeValue.FromObject(obj);
        return RuntimeValue.Null;
    }

    private RuntimeValue EvaluateClassReference(ClassReferenceNode node)
    {
        // className:memberName - call member as if it were in current class
        var targetClass = _context.Program?.GetClass(node.ClassName);
        if (targetClass == null)
            return RuntimeValue.Null;

        var member = targetClass.LookupMember(node.MemberName);
        if (member is CompiledFunction func)
        {
            return CallFunction(_context.CurrentObject as RuntimeObject, func, Array.Empty<RuntimeValue>());
        }
        if (member is CompiledConstant constant)
        {
            return EvaluateExpression(constant.Value);
        }

        return RuntimeValue.Null;
    }

    private RuntimeValue EvaluateBinaryExpression(BinaryExpressionNode node)
    {
        // Short-circuit evaluation for logical operators
        if (node.Operator == BinaryOperator.And)
        {
            var left = EvaluateExpression(node.Left);
            if (!left.IsTruthy)
                return RuntimeValue.False;
            var right = EvaluateExpression(node.Right);
            return RuntimeValue.FromBool(right.IsTruthy);
        }

        if (node.Operator == BinaryOperator.Or)
        {
            var left = EvaluateExpression(node.Left);
            if (left.IsTruthy)
                return RuntimeValue.True;
            var right = EvaluateExpression(node.Right);
            return RuntimeValue.FromBool(right.IsTruthy);
        }

        // Evaluate both operands
        var leftVal = EvaluateExpression(node.Left);
        var rightVal = EvaluateExpression(node.Right);

        return node.Operator switch
        {
            BinaryOperator.Add => leftVal + rightVal,
            BinaryOperator.Subtract => leftVal - rightVal,
            BinaryOperator.Multiply => leftVal * rightVal,
            BinaryOperator.Divide => leftVal / rightVal,
            BinaryOperator.Modulo => leftVal % rightVal,
            BinaryOperator.LessThan => leftVal < rightVal,
            BinaryOperator.LessOrEqual => leftVal <= rightVal,
            BinaryOperator.GreaterThan => leftVal > rightVal,
            BinaryOperator.GreaterOrEqual => leftVal >= rightVal,
            BinaryOperator.Equal => leftVal == rightVal,
            BinaryOperator.NotEqual => leftVal != rightVal,
            BinaryOperator.StrictEqual => RuntimeValue.FromBool(leftVal.StrictEquals(rightVal)),
            BinaryOperator.StrictNotEqual => RuntimeValue.FromBool(!leftVal.StrictEquals(rightVal)),
            BinaryOperator.BitwiseAnd => leftVal & rightVal,
            BinaryOperator.BitwiseOr => leftVal | rightVal,
            BinaryOperator.BitwiseXor => leftVal ^ rightVal,
            BinaryOperator.ShiftLeft => RuntimeValue.ShiftLeft(leftVal, rightVal),
            BinaryOperator.ShiftRight => RuntimeValue.ShiftRight(leftVal, rightVal),
            _ => throw new ExecutionException($"Unknown binary operator: {node.Operator}")
        };
    }

    private RuntimeValue EvaluateUnaryExpression(UnaryExpressionNode node)
    {
        if (node.Operator == UnaryOperator.PreIncrement || node.Operator == UnaryOperator.PreDecrement)
        {
            return EvaluatePreIncrement(node);
        }

        var operand = EvaluateExpression(node.Operand);

        return node.Operator switch
        {
            UnaryOperator.Negate => -operand,
            UnaryOperator.Not => RuntimeValue.LogicalNot(operand),
            UnaryOperator.BitwiseNot => ~operand,
            _ => throw new ExecutionException($"Unknown unary operator: {node.Operator}")
        };
    }

    private RuntimeValue EvaluatePreIncrement(UnaryExpressionNode node)
    {
        var value = EvaluateExpression(node.Operand);
        var newValue = node.Operator == UnaryOperator.PreIncrement
            ? value + RuntimeValue.One
            : value - RuntimeValue.One;

        // Assign back to the operand
        AssignToExpression(node.Operand, newValue);

        return newValue;
    }

    private RuntimeValue EvaluateConditionalExpression(ConditionalExpressionNode node)
    {
        var condition = EvaluateExpression(node.Condition);

        if (condition.IsTruthy)
        {
            return node.ThenValue != null
                ? EvaluateExpression(node.ThenValue)
                : RuntimeValue.Null;
        }
        else
        {
            return node.ElseValue != null
                ? EvaluateExpression(node.ElseValue)
                : RuntimeValue.Null;
        }
    }

    private RuntimeValue EvaluateNullCoalesceExpression(NullCoalesceExpressionNode node)
    {
        var left = EvaluateExpression(node.Left);
        if (left.IsTruthy)
            return left;
        return EvaluateExpression(node.Right);
    }

    private RuntimeValue EvaluateAssignmentExpression(AssignmentExpressionNode node)
    {
        RuntimeValue newValue;

        if (node.Operator == AssignmentOperator.Assign)
        {
            newValue = EvaluateExpression(node.Right);
        }
        else
        {
            var currentValue = EvaluateExpression(node.Left);
            var rightValue = EvaluateExpression(node.Right);

            newValue = node.Operator switch
            {
                AssignmentOperator.AddAssign => currentValue + rightValue,
                AssignmentOperator.SubtractAssign => currentValue - rightValue,
                AssignmentOperator.MultiplyAssign => currentValue * rightValue,
                AssignmentOperator.DivideAssign => currentValue / rightValue,
                AssignmentOperator.ModuloAssign => currentValue % rightValue,
                AssignmentOperator.BitwiseAndAssign => currentValue & rightValue,
                AssignmentOperator.BitwiseOrAssign => currentValue | rightValue,
                AssignmentOperator.BitwiseXorAssign => currentValue ^ rightValue,
                AssignmentOperator.ShiftLeftAssign => RuntimeValue.ShiftLeft(currentValue, rightValue),
                AssignmentOperator.ShiftRightAssign => RuntimeValue.ShiftRight(currentValue, rightValue),
                _ => throw new ExecutionException($"Unknown assignment operator: {node.Operator}")
            };
        }

        AssignToExpression(node.Left, newValue);
        return newValue;
    }

    private void AssignToExpression(ExpressionNode target, RuntimeValue value)
    {
        switch (target)
        {
            case IdentifierNode id:
                AssignToIdentifier(id.Name, value);
                break;

            case MemberAccessNode member:
                AssignToMember(member, value);
                break;

            case IndexAccessNode index:
                AssignToIndex(index, value);
                break;

            default:
                throw new ExecutionException($"Cannot assign to expression type: {target.GetType().Name}",
                    target.SourceFile, target.Line, target.Column);
        }
    }

    private void AssignToIdentifier(string name, RuntimeValue value)
    {
        // Try local variable first
        if (_context.HasLocalVariable(name))
        {
            _context.SetLocalVariable(name, value);
            return;
        }

        // Try object variable
        if (_context.CurrentObject is RuntimeObject obj)
        {
            var variable = obj.Class.LookupVariable(name);
            if (variable != null)
            {
                obj.SetVariable(name, value);
                return;
            }

            // Check for varfunc (setter)
            var func = obj.Class.LookupFunction(name);
            if (func is { IsVarFunc: true })
            {
                CallFunction(obj, func, new[] { value });
                return;
            }
        }

        // Declare as new local
        _context.DeclareLocalVariable(name, value);
    }

    private void AssignToMember(MemberAccessNode node, RuntimeValue value)
    {
        var obj = EvaluateExpression(node.Object);
        if (obj.AsObject<RuntimeObject>() is { } targetObj)
        {
            var variable = targetObj.Class.LookupVariable(node.Member);
            if (variable != null)
            {
                targetObj.SetVariable(node.Member, value);
                return;
            }

            // Check for varfunc
            var func = targetObj.Class.LookupFunction(node.Member);
            if (func is { IsVarFunc: true })
            {
                CallFunction(targetObj, func, new[] { value });
            }
        }
    }

    private void AssignToIndex(IndexAccessNode node, RuntimeValue value)
    {
        // Handle dynamic variable names like x[name]
        var obj = EvaluateExpression(node.Object);
        var index = EvaluateExpression(node.Index);

        if (node.Object is IdentifierNode baseId)
        {
            var name = baseId.Name + index.AsString();
            AssignToIdentifier(name, value);
        }
    }

    private RuntimeValue EvaluateMemberAccess(MemberAccessNode node)
    {
        var obj = EvaluateExpression(node.Object);

        if (obj.AsObject<RuntimeObject>() is { } targetObj)
        {
            // Check for variable
            var variable = targetObj.Class.LookupVariable(node.Member);
            if (variable != null)
            {
                return targetObj.GetVariable(node.Member);
            }

            // Check for function (call without arguments)
            var func = targetObj.Class.LookupFunction(node.Member);
            if (func != null)
            {
                return CallFunction(targetObj, func, Array.Empty<RuntimeValue>());
            }

            // Check for constant
            var constant = targetObj.Class.LookupMember(node.Member) as CompiledConstant;
            if (constant != null)
            {
                var savedObj = _context.CurrentObject;
                _context.CurrentObject = targetObj;
                try
                {
                    return EvaluateExpression(constant.Value);
                }
                finally
                {
                    _context.CurrentObject = savedObj;
                }
            }
        }

        // Handle numeric member for vectors (v.0, v.1, etc.)
        if (int.TryParse(node.Member, out _))
        {
            // This is a vector access
            if (node.Object is IdentifierNode id)
            {
                var name = id.Name + "." + node.Member;
                return EvaluateIdentifier(new IdentifierNode { Name = name });
            }
        }

        return RuntimeValue.Null;
    }

    private RuntimeValue EvaluateIndexAccess(IndexAccessNode node)
    {
        var index = EvaluateExpression(node.Index);

        if (node.Object is IdentifierNode id)
        {
            var name = id.Name + index.AsString();
            return EvaluateIdentifier(new IdentifierNode { Name = name });
        }

        return RuntimeValue.Null;
    }

    private RuntimeValue EvaluateFunctionCall(FunctionCallNode node)
    {
        var args = node.Arguments.Select(EvaluateExpression).ToArray();

        // Direct function call on identifier
        if (node.Function is IdentifierNode id)
        {
            // Check builtin functions first
            if (_builtins != null && _builtins.HasFunction(id.Name))
            {
                return _builtins.Call(id.Name, args, _context);
            }

            // Check object function
            if (_context.CurrentObject is RuntimeObject obj)
            {
                var func = obj.Class.LookupFunction(id.Name);
                if (func != null)
                {
                    return CallFunction(obj, func, args);
                }
            }

            return RuntimeValue.Null;
        }

        // Member function call (obj.func())
        if (node.Function is MemberAccessNode member)
        {
            var target = EvaluateExpression(member.Object);

            if (target.AsObject<RuntimeObject>() is { } targetObj)
            {
                var func = targetObj.Class.LookupFunction(member.Member);
                if (func != null)
                {
                    return CallFunction(targetObj, func, args);
                }
            }

            // Check for builtin type methods
            if (_builtins != null)
            {
                return _builtins.CallMethod(target, member.Member, args, _context);
            }
        }

        return RuntimeValue.Null;
    }

    private RuntimeValue EvaluatePostfixIncrement(PostfixIncrementNode node)
    {
        var value = EvaluateExpression(node.Operand);
        var newValue = node.IsIncrement
            ? value + RuntimeValue.One
            : value - RuntimeValue.One;

        AssignToExpression(node.Operand, newValue);

        return value;  // Return original value
    }

    /// <summary>
    /// Call a function on an object.
    /// </summary>
    public RuntimeValue CallFunction(RuntimeObject? target, CompiledFunction function, RuntimeValue[] args)
    {
        // Save current state
        var savedObject = _context.CurrentObject;
        var savedArgs = new RuntimeValue[10];
        Array.Copy(_context.Arguments, savedArgs, 10);
        var savedArgCount = _context.ArgumentCount;

        try
        {
            // Set up new context
            _context.CurrentObject = target;
            _context.SetArguments(args);
            _context.PushFunction(function.Name, target, args);

            using var scope = _context.PushScope();

            var result = ExecuteStatements(function.Body);

            return result.Value;
        }
        finally
        {
            // Restore state
            _context.PopFunction();
            _context.CurrentObject = savedObject;
            Array.Copy(savedArgs, _context.Arguments, 10);
            _context.ArgumentCount = savedArgCount;
        }
    }
}

/// <summary>
/// Interface for providing builtin functions.
/// </summary>
public interface IBuiltinFunctionProvider
{
    /// <summary>
    /// Check if a builtin function exists.
    /// </summary>
    bool HasFunction(string name);

    /// <summary>
    /// Call a builtin function.
    /// </summary>
    RuntimeValue Call(string name, RuntimeValue[] args, ExecutionContext context);

    /// <summary>
    /// Call a method on a value.
    /// </summary>
    RuntimeValue CallMethod(RuntimeValue target, string methodName, RuntimeValue[] args, ExecutionContext context);
}
