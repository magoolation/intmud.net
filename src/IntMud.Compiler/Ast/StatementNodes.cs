namespace IntMud.Compiler.Ast;

/// <summary>
/// Base class for all statement nodes.
/// </summary>
public abstract class StatementNode : AstNode
{
}

/// <summary>
/// If statement with optional else-if and else clauses.
/// </summary>
public class IfStatementNode : StatementNode
{
    public required ExpressionNode Condition { get; init; }
    public List<StatementNode> ThenBody { get; } = new();
    public List<ElseIfClause> ElseIfClauses { get; } = new();
    public List<StatementNode> ElseBody { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIfStatement(this);
}

/// <summary>
/// Else-if clause within an if statement.
/// </summary>
public class ElseIfClause
{
    public required ExpressionNode Condition { get; init; }
    public List<StatementNode> Body { get; } = new();
}

/// <summary>
/// While loop (enquanto ... efim).
/// </summary>
public class WhileStatementNode : StatementNode
{
    public required ExpressionNode Condition { get; init; }
    public List<StatementNode> Body { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitWhileStatement(this);
}

/// <summary>
/// For loop (epara init, condition, increment ... efim).
/// </summary>
public class ForStatementNode : StatementNode
{
    public required ExpressionNode Initializer { get; init; }
    public required ExpressionNode Condition { get; init; }
    public required ExpressionNode Increment { get; init; }
    public List<StatementNode> Body { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitForStatement(this);
}

/// <summary>
/// Foreach loop (para cada variavel em colecao ... efim).
/// </summary>
public class ForEachStatementNode : StatementNode
{
    public required string VariableName { get; init; }
    public required ExpressionNode Collection { get; init; }
    public List<StatementNode> Body { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitForEachStatement(this);
}

/// <summary>
/// Switch statement (casovar ... casofim).
/// </summary>
public class SwitchStatementNode : StatementNode
{
    public required ExpressionNode Expression { get; init; }
    public List<CaseClauseNode> Cases { get; } = new();
    public CaseClauseNode? DefaultCase { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSwitchStatement(this);
}

/// <summary>
/// Case clause within a switch statement.
/// </summary>
public class CaseClauseNode : AstNode
{
    public string? Label { get; set; }  // null = default case
    public List<StatementNode> Body { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCaseClause(this);
}

/// <summary>
/// Return statement (ret [value] or ret condition, value).
/// When Condition is set, the return only executes if condition is true.
/// </summary>
public class ReturnStatementNode : StatementNode
{
    /// <summary>
    /// Optional condition - if set, ret only executes when condition is true.
    /// </summary>
    public ExpressionNode? Condition { get; set; }

    /// <summary>
    /// The value to return.
    /// </summary>
    public ExpressionNode? Value { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitReturnStatement(this);
}

/// <summary>
/// Exit statement (sair [condition]).
/// </summary>
public class ExitStatementNode : StatementNode
{
    public ExpressionNode? Condition { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitExitStatement(this);
}

/// <summary>
/// Continue statement (continuar [condition]).
/// </summary>
public class ContinueStatementNode : StatementNode
{
    public ExpressionNode? Condition { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitContinueStatement(this);
}

/// <summary>
/// Terminate statement (terminar).
/// </summary>
public class TerminateStatementNode : StatementNode
{
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitTerminateStatement(this);
}

/// <summary>
/// Expression statement (expression used as a statement).
/// </summary>
public class ExpressionStatementNode : StatementNode
{
    public List<ExpressionNode> Expressions { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitExpressionStatement(this);
}

/// <summary>
/// Local variable declaration statement inside a function.
/// </summary>
public class LocalVariableDeclarationNode : StatementNode
{
    public VariableModifiers Modifiers { get; set; }
    public required string TypeName { get; init; }
    public required string Name { get; init; }
    public int VectorSize { get; set; }  // 0 = not a vector
    public ExpressionNode? Initializer { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLocalVariableDeclaration(this);
}
