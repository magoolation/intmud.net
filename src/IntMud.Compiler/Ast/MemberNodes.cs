namespace IntMud.Compiler.Ast;

/// <summary>
/// Modifiers for variable declarations.
/// </summary>
[Flags]
public enum VariableModifiers
{
    None = 0,
    Comum = 1,  // Static/shared across all objects
    Sav = 2     // Should be saved when object is persisted
}

/// <summary>
/// Variable declaration.
/// </summary>
public class VariableDeclarationNode : AstNode
{
    public VariableModifiers Modifiers { get; set; }
    public required string TypeName { get; init; }
    public required string Name { get; init; }
    public int VectorSize { get; set; }  // 0 = not a vector
    public ExpressionNode? Initializer { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitVariableDeclaration(this);
}

/// <summary>
/// Function definition.
/// </summary>
public class FunctionDefinitionNode : AstNode
{
    public required string Name { get; init; }
    public List<StatementNode> Body { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionDefinition(this);
}

/// <summary>
/// Constant definition (const name = value, additional...).
/// </summary>
public class ConstantDefinitionNode : AstNode
{
    public required string Name { get; init; }
    public required ExpressionNode Value { get; init; }
    /// <summary>Additional expressions after the value (side effects).</summary>
    public List<ExpressionNode> AdditionalExpressions { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitConstantDefinition(this);
}

/// <summary>
/// VarFunc definition (function that acts like a variable).
/// </summary>
public class VarFuncDefinitionNode : AstNode
{
    public required string Name { get; init; }
    public List<StatementNode> Body { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitVarFuncDefinition(this);
}

/// <summary>
/// VarConst definition (varfunc as a single expression).
/// </summary>
public class VarConstDefinitionNode : AstNode
{
    public required string Name { get; init; }
    public required ExpressionNode Value { get; init; }
    /// <summary>Additional expressions after the value (side effects).</summary>
    public List<ExpressionNode> AdditionalExpressions { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitVarConstDefinition(this);
}

/// <summary>
/// RefVar declaration (reference to a variable).
/// </summary>
public class RefVarDeclarationNode : StatementNode
{
    public required string Name { get; init; }
    public required ExpressionNode Value { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitRefVarDeclaration(this);
}
