namespace IntMud.Compiler.Ast;

/// <summary>
/// Base class for all expression nodes.
/// </summary>
public abstract class ExpressionNode : AstNode
{
}

/// <summary>
/// Binary operator types.
/// </summary>
public enum BinaryOperator
{
    // Arithmetic
    Add,        // +
    Subtract,   // -
    Multiply,   // *
    Divide,     // /
    Modulo,     // %

    // Comparison
    LessThan,       // <
    LessOrEqual,    // <=
    GreaterThan,    // >
    GreaterOrEqual, // >=
    Equal,          // ==
    StrictEqual,    // ===
    NotEqual,       // !=
    StrictNotEqual, // !==

    // Logical
    And,            // &&
    Or,             // ||

    // Bitwise
    BitwiseAnd,     // &
    BitwiseOr,      // |
    BitwiseXor,     // ^
    ShiftLeft,      // <<
    ShiftRight,     // >>
}

/// <summary>
/// Unary operator types.
/// </summary>
public enum UnaryOperator
{
    Negate,         // -
    Not,            // !
    BitwiseNot,     // ~
    PreIncrement,   // ++x
    PreDecrement,   // --x
}

/// <summary>
/// Assignment operator types.
/// </summary>
public enum AssignmentOperator
{
    Assign,         // =
    AddAssign,      // +=
    SubtractAssign, // -=
    MultiplyAssign, // *=
    DivideAssign,   // /=
    ModuloAssign,   // %=
    ShiftLeftAssign,    // <<=
    ShiftRightAssign,   // >>=
    BitwiseAndAssign,   // &=
    BitwiseOrAssign,    // |=
    BitwiseXorAssign,   // ^=
}

/// <summary>
/// Binary expression (a op b).
/// </summary>
public class BinaryExpressionNode : ExpressionNode
{
    public required ExpressionNode Left { get; init; }
    public required BinaryOperator Operator { get; init; }
    public required ExpressionNode Right { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBinaryExpression(this);
}

/// <summary>
/// Unary expression (op a).
/// </summary>
public class UnaryExpressionNode : ExpressionNode
{
    public required UnaryOperator Operator { get; init; }
    public required ExpressionNode Operand { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitUnaryExpression(this);
}

/// <summary>
/// Conditional expression (a ? b : c).
/// </summary>
public class ConditionalExpressionNode : ExpressionNode
{
    public required ExpressionNode Condition { get; init; }
    public ExpressionNode? ThenValue { get; set; }
    public ExpressionNode? ElseValue { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitConditionalExpression(this);
}

/// <summary>
/// Null coalesce expression (a ?? b).
/// </summary>
public class NullCoalesceExpressionNode : ExpressionNode
{
    public required ExpressionNode Left { get; init; }
    public required ExpressionNode Right { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitNullCoalesceExpression(this);
}

/// <summary>
/// Assignment expression (a = b, a += b, etc.).
/// </summary>
public class AssignmentExpressionNode : ExpressionNode
{
    public required ExpressionNode Left { get; init; }
    public required AssignmentOperator Operator { get; init; }
    public required ExpressionNode Right { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitAssignmentExpression(this);
}

/// <summary>
/// Member access expression (a.b).
/// </summary>
public class MemberAccessNode : ExpressionNode
{
    public required ExpressionNode Object { get; init; }
    public required string Member { get; init; }

    /// <summary>
    /// Whether this is a countdown variable (has @ suffix).
    /// </summary>
    public bool IsCountdown { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitMemberAccess(this);
}

/// <summary>
/// Dynamic member access expression (a.prefix_[expr]_suffix).
/// The member name is constructed by concatenating static and dynamic parts.
/// </summary>
public class DynamicMemberAccessNode : ExpressionNode
{
    public required ExpressionNode Object { get; init; }

    /// <summary>
    /// Parts of the dynamic member name. Each part is either:
    /// - A StringLiteralNode for static parts (e.g., "cmd_", "_suffix")
    /// - An expression for dynamic parts (e.g., arg1)
    /// </summary>
    public List<ExpressionNode> MemberParts { get; } = new();

    /// <summary>
    /// Whether this is a countdown variable (has @ suffix).
    /// </summary>
    public bool IsCountdown { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitDynamicMemberAccess(this);
}

/// <summary>
/// Index access expression (a[b]).
/// </summary>
public class IndexAccessNode : ExpressionNode
{
    public required ExpressionNode Object { get; init; }
    public required ExpressionNode Index { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIndexAccess(this);
}

/// <summary>
/// Function call expression (a(b, c, ...)).
/// </summary>
public class FunctionCallNode : ExpressionNode
{
    public required ExpressionNode Function { get; init; }
    public List<ExpressionNode> Arguments { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionCall(this);
}

/// <summary>
/// Postfix increment/decrement expression (a++ or a--).
/// </summary>
public class PostfixIncrementNode : ExpressionNode
{
    public required ExpressionNode Operand { get; init; }
    public required bool IsIncrement { get; init; }  // true = ++, false = --

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitPostfixIncrement(this);
}

/// <summary>
/// Simple identifier reference.
/// </summary>
public class IdentifierNode : ExpressionNode
{
    public required string Name { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIdentifier(this);
}

/// <summary>
/// Numeric literal (integer or floating point).
/// </summary>
public class NumericLiteralNode : ExpressionNode
{
    public required double Value { get; init; }
    public bool IsHexadecimal { get; set; }
    public bool IsInteger { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitNumericLiteral(this);
}

/// <summary>
/// String literal.
/// </summary>
public class StringLiteralNode : ExpressionNode
{
    public required string Value { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitStringLiteral(this);
}

/// <summary>
/// Null literal (nulo).
/// </summary>
public class NullLiteralNode : ExpressionNode
{
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitNullLiteral(this);
}

/// <summary>
/// This reference (este).
/// </summary>
public class ThisReferenceNode : ExpressionNode
{
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitThisReference(this);
}

/// <summary>
/// Argument reference (arg0-arg9).
/// </summary>
public class ArgReferenceNode : ExpressionNode
{
    public required int Index { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitArgReference(this);
}

/// <summary>
/// Argument count reference (args).
/// </summary>
public class ArgsReferenceNode : ExpressionNode
{
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitArgsReference(this);
}

/// <summary>
/// Dynamic identifier reference for patterns like x[y], x[y]_suffix, [expr], [expr]_suffix.
/// In IntMud, [expr] creates a dynamic variable/function name by concatenating
/// the static parts with the evaluated expression values.
/// Example: x["1"] = 10 is the same as x1 = 10
/// Example: x[y] = 20 where y="_teste" is the same as x_teste = 20
/// </summary>
public class DynamicIdentifierNode : ExpressionNode
{
    /// <summary>
    /// Parts that make up the dynamic identifier name.
    /// StringLiteralNode for static text parts, other ExpressionNode for dynamic parts.
    /// </summary>
    public List<ExpressionNode> Parts { get; } = new();

    /// <summary>Whether this identifier has the countdown @ suffix.</summary>
    public bool IsCountdown { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitDynamicIdentifier(this);
}

/// <summary>
/// Dollar reference ($classname or $[expr] or $name[expr]).
/// </summary>
public class DollarReferenceNode : ExpressionNode
{
    /// <summary>Static class name for $classname, or base name for $name[expr].</summary>
    public string? ClassName { get; init; }

    /// <summary>Dynamic expression for $[expr] or the index in $name[expr].</summary>
    public ExpressionNode? DynamicExpression { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitDollarReference(this);
}

/// <summary>
/// Class member reference (classname:member or name[expr]:member).
/// Supports both static and dynamic member names.
/// </summary>
public class ClassReferenceNode : ExpressionNode
{
    /// <summary>Static class name, or base name for dynamic reference.</summary>
    public string? ClassName { get; init; }

    /// <summary>Dynamic index expression for name[expr]:member.</summary>
    public ExpressionNode? ClassNameIndex { get; init; }

    /// <summary>
    /// Static member name (when DynamicMemberParts is empty).
    /// </summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// Dynamic member parts for patterns like class:prefix_[expr]_suffix.
    /// If non-empty, overrides MemberName.
    /// </summary>
    public List<ExpressionNode> DynamicMemberParts { get; } = new();

    /// <summary>
    /// Whether this is a countdown variable (has @ suffix).
    /// </summary>
    public bool IsCountdown { get; set; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitClassReference(this);
}

/// <summary>
/// New object expression (novo classname(args)).
/// </summary>
public class NewExpressionNode : ExpressionNode
{
    public required string ClassName { get; init; }
    public List<ExpressionNode> Arguments { get; init; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitNewExpression(this);
}

/// <summary>
/// Delete object expression (apagar obj).
/// </summary>
public class DeleteExpressionNode : ExpressionNode
{
    public required ExpressionNode Operand { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitDeleteExpression(this);
}
