namespace IntMud.Compiler.Ast;

/// <summary>
/// Root node representing an entire compilation unit (file).
/// </summary>
public class CompilationUnitNode : AstNode
{
    public List<FileOptionNode> Options { get; } = new();
    public List<ClassDefinitionNode> Classes { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCompilationUnit(this);
}

/// <summary>
/// File-level option (incluir, exec, telatxt, log, err, completo, arqexec).
/// </summary>
public class FileOptionNode : AstNode
{
    public required string Name { get; init; }
    public required string Value { get; init; }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFileOption(this);
}

/// <summary>
/// Class definition.
/// </summary>
public class ClassDefinitionNode : AstNode
{
    public required string Name { get; init; }
    public List<string> BaseClasses { get; } = new();
    public List<AstNode> Members { get; } = new();

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitClassDefinition(this);
}
