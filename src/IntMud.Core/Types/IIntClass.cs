namespace IntMud.Core.Types;

/// <summary>
/// Represents an IntMUD class definition.
/// Equivalent to TClasse from the original C++ implementation.
/// </summary>
public interface IIntClass
{
    /// <summary>
    /// Class name (max 47 characters in original).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Source file this class was defined in.
    /// </summary>
    ISourceFile? SourceFile { get; }

    /// <summary>
    /// Compiled bytecode for this class.
    /// </summary>
    ReadOnlyMemory<byte> Bytecode { get; }

    /// <summary>
    /// Base classes this class inherits from (up to 50 in original).
    /// </summary>
    IReadOnlyList<IIntClass> BaseClasses { get; }

    /// <summary>
    /// Classes that derive from this class.
    /// </summary>
    IReadOnlyList<IIntClass> DerivedClasses { get; }

    /// <summary>
    /// Variable definitions in this class.
    /// </summary>
    IReadOnlyDictionary<string, VariableDefinition> Variables { get; }

    /// <summary>
    /// Function definitions in this class.
    /// </summary>
    IReadOnlyDictionary<string, FunctionDefinition> Functions { get; }

    /// <summary>
    /// Constant definitions in this class.
    /// </summary>
    IReadOnlyDictionary<string, ConstantDefinition> Constants { get; }

    /// <summary>
    /// Size of class-level variables in bytes.
    /// </summary>
    int ClassVariablesSize { get; }

    /// <summary>
    /// Size of instance variables in bytes.
    /// </summary>
    int InstanceVariablesSize { get; }

    /// <summary>
    /// Number of objects currently created from this class.
    /// </summary>
    int ObjectCount { get; }

    /// <summary>
    /// First object in the linked list.
    /// </summary>
    IIntObject? FirstObject { get; }

    /// <summary>
    /// Last object in the linked list.
    /// </summary>
    IIntObject? LastObject { get; }

    /// <summary>
    /// Find a variable by name, searching through inheritance chain.
    /// </summary>
    VariableDefinition? FindVariable(string name);

    /// <summary>
    /// Find a function by name, searching through inheritance chain.
    /// </summary>
    FunctionDefinition? FindFunction(string name);

    /// <summary>
    /// Find a constant by name, searching through inheritance chain.
    /// </summary>
    ConstantDefinition? FindConstant(string name);

    /// <summary>
    /// Create a new object instance of this class.
    /// </summary>
    IIntObject CreateObject();
}

/// <summary>
/// Source file information.
/// </summary>
public interface ISourceFile
{
    /// <summary>File path</summary>
    string Path { get; }

    /// <summary>File name without path</summary>
    string Name { get; }

    /// <summary>Last modification time</summary>
    DateTime LastModified { get; }
}
