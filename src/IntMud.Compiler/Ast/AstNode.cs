namespace IntMud.Compiler.Ast;

/// <summary>
/// Base class for all AST nodes.
/// </summary>
public abstract class AstNode
{
    /// <summary>
    /// Source file path where this node was defined.
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// Line number in the source file (1-based).
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number in the source file (1-based).
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Accept a visitor for processing this node.
    /// </summary>
    public abstract T Accept<T>(IAstVisitor<T> visitor);
}

/// <summary>
/// Visitor interface for AST nodes.
/// </summary>
public interface IAstVisitor<T>
{
    // Compilation Unit
    T VisitCompilationUnit(CompilationUnitNode node);
    T VisitFileOption(FileOptionNode node);

    // Class
    T VisitClassDefinition(ClassDefinitionNode node);

    // Members
    T VisitVariableDeclaration(VariableDeclarationNode node);
    T VisitFunctionDefinition(FunctionDefinitionNode node);
    T VisitConstantDefinition(ConstantDefinitionNode node);
    T VisitVarFuncDefinition(VarFuncDefinitionNode node);
    T VisitVarConstDefinition(VarConstDefinitionNode node);

    // Statements
    T VisitIfStatement(IfStatementNode node);
    T VisitWhileStatement(WhileStatementNode node);
    T VisitForStatement(ForStatementNode node);
    T VisitForEachStatement(ForEachStatementNode node);
    T VisitSwitchStatement(SwitchStatementNode node);
    T VisitCaseClause(CaseClauseNode node);
    T VisitReturnStatement(ReturnStatementNode node);
    T VisitExitStatement(ExitStatementNode node);
    T VisitContinueStatement(ContinueStatementNode node);
    T VisitTerminateStatement(TerminateStatementNode node);
    T VisitExpressionStatement(ExpressionStatementNode node);
    T VisitRefVarDeclaration(RefVarDeclarationNode node);
    T VisitLocalVariableDeclaration(LocalVariableDeclarationNode node);

    // Expressions
    T VisitBinaryExpression(BinaryExpressionNode node);
    T VisitUnaryExpression(UnaryExpressionNode node);
    T VisitConditionalExpression(ConditionalExpressionNode node);
    T VisitNullCoalesceExpression(NullCoalesceExpressionNode node);
    T VisitAssignmentExpression(AssignmentExpressionNode node);
    T VisitMemberAccess(MemberAccessNode node);
    T VisitDynamicMemberAccess(DynamicMemberAccessNode node);
    T VisitIndexAccess(IndexAccessNode node);
    T VisitFunctionCall(FunctionCallNode node);
    T VisitPostfixIncrement(PostfixIncrementNode node);
    T VisitIdentifier(IdentifierNode node);
    T VisitNumericLiteral(NumericLiteralNode node);
    T VisitStringLiteral(StringLiteralNode node);
    T VisitNullLiteral(NullLiteralNode node);
    T VisitThisReference(ThisReferenceNode node);
    T VisitArgReference(ArgReferenceNode node);
    T VisitArgsReference(ArgsReferenceNode node);
    T VisitDynamicIdentifier(DynamicIdentifierNode node);
    T VisitDollarReference(DollarReferenceNode node);
    T VisitClassReference(ClassReferenceNode node);
    T VisitNewExpression(NewExpressionNode node);
    T VisitDeleteExpression(DeleteExpressionNode node);
}
