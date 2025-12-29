using IntMud.Compiler.Ast;
using IntMud.Compiler.Parsing;

namespace IntMud.Runtime.Execution;

/// <summary>
/// Loads and compiles IntMUD source files into a program.
/// </summary>
public class ProgramLoader
{
    private readonly IntMudSourceParser _parser = new();

    /// <summary>
    /// Load a program from a single source file.
    /// </summary>
    public CompiledProgram LoadFromFile(string filePath)
    {
        var sourceCode = File.ReadAllText(filePath);
        return LoadFromSource(sourceCode, filePath);
    }

    /// <summary>
    /// Load a program from source code.
    /// </summary>
    public CompiledProgram LoadFromSource(string sourceCode, string? fileName = null)
    {
        var ast = _parser.Parse(sourceCode, fileName);
        return CompileAst(ast);
    }

    /// <summary>
    /// Load a program from multiple source files.
    /// </summary>
    public CompiledProgram LoadFromFiles(IEnumerable<string> filePaths)
    {
        var program = new CompiledProgram();

        foreach (var filePath in filePaths)
        {
            var sourceCode = File.ReadAllText(filePath);
            var ast = _parser.Parse(sourceCode, filePath);
            MergeIntoProgram(program, ast);
            program.SourceFiles.Add(filePath);
        }

        // Resolve inheritance
        ResolveInheritance(program);

        return program;
    }

    /// <summary>
    /// Compile an AST into a program.
    /// </summary>
    public CompiledProgram CompileAst(CompilationUnitNode ast)
    {
        var program = new CompiledProgram();
        MergeIntoProgram(program, ast);
        ResolveInheritance(program);
        return program;
    }

    private void MergeIntoProgram(CompiledProgram program, CompilationUnitNode ast)
    {
        // Process file options
        foreach (var option in ast.Options)
        {
            ProcessOption(program, option);
        }

        // Process classes
        foreach (var classNode in ast.Classes)
        {
            var compiledClass = CompileClass(classNode);
            program.AddClass(compiledClass);
        }
    }

    private void ProcessOption(CompiledProgram program, FileOptionNode option)
    {
        program.Options[option.Name] = option.Value;

        switch (option.Name.ToLowerInvariant())
        {
            case "exec":
                if (int.TryParse(option.Value, out var exec))
                    program.MaxInstructions = exec;
                break;

            case "telatxt":
                program.ShowTelaTxt = option.Value == "1";
                break;
        }
    }

    private CompiledClass CompileClass(ClassDefinitionNode node)
    {
        var compiledClass = new CompiledClass
        {
            Name = node.Name,
            SourceFile = node.SourceFile,
            Line = node.Line
        };

        // Base classes
        compiledClass.BaseClasses.AddRange(node.BaseClasses);

        // Process members
        foreach (var member in node.Members)
        {
            switch (member)
            {
                case VariableDeclarationNode varDecl:
                    var variable = new CompiledVariable
                    {
                        Name = varDecl.Name,
                        TypeName = varDecl.TypeName,
                        IsComum = varDecl.Modifiers.HasFlag(VariableModifiers.Comum),
                        IsSav = varDecl.Modifiers.HasFlag(VariableModifiers.Sav),
                        VectorSize = varDecl.VectorSize,
                        Initializer = varDecl.Initializer,
                        SourceFile = varDecl.SourceFile,
                        Line = varDecl.Line,
                        Column = varDecl.Column
                    };
                    compiledClass.Variables[variable.Name] = variable;
                    break;

                case FunctionDefinitionNode funcDef:
                    var function = new CompiledFunction
                    {
                        Name = funcDef.Name,
                        IsVarFunc = false,
                        SourceFile = funcDef.SourceFile,
                        Line = funcDef.Line,
                        Column = funcDef.Column
                    };
                    function.Body.AddRange(funcDef.Body);
                    compiledClass.Functions[function.Name] = function;
                    break;

                case VarFuncDefinitionNode varFuncDef:
                    var varFunc = new CompiledFunction
                    {
                        Name = varFuncDef.Name,
                        IsVarFunc = true,
                        SourceFile = varFuncDef.SourceFile,
                        Line = varFuncDef.Line,
                        Column = varFuncDef.Column
                    };
                    varFunc.Body.AddRange(varFuncDef.Body);
                    compiledClass.Functions[varFunc.Name] = varFunc;
                    break;

                case ConstantDefinitionNode constDef:
                    var constant = new CompiledConstant
                    {
                        Name = constDef.Name,
                        Value = constDef.Value,
                        IsVarConst = false,
                        SourceFile = constDef.SourceFile,
                        Line = constDef.Line,
                        Column = constDef.Column
                    };
                    compiledClass.Constants[constant.Name] = constant;
                    break;

                case VarConstDefinitionNode varConstDef:
                    var varConst = new CompiledConstant
                    {
                        Name = varConstDef.Name,
                        Value = varConstDef.Value,
                        IsVarConst = true,
                        SourceFile = varConstDef.SourceFile,
                        Line = varConstDef.Line,
                        Column = varConstDef.Column
                    };
                    compiledClass.Constants[varConst.Name] = varConst;
                    break;
            }
        }

        return compiledClass;
    }

    private void ResolveInheritance(CompiledProgram program)
    {
        foreach (var compiledClass in program.Classes.Values)
        {
            foreach (var baseName in compiledClass.BaseClasses)
            {
                var baseClass = program.GetClass(baseName);
                if (baseClass != null)
                {
                    compiledClass.ResolvedBases.Add(baseClass);
                }
            }
        }
    }
}

/// <summary>
/// Runtime class registry implementation.
/// </summary>
public class RuntimeClassRegistry : IClassRegistry
{
    private readonly CompiledProgram _program;
    private long _nextObjectId = 1;
    private readonly List<RuntimeObject> _objectsToDelete = new();
    private bool _commonVariablesInitialized;

    public RuntimeClassRegistry(CompiledProgram program)
    {
        _program = program;
    }

    /// <summary>
    /// The program this registry is managing.
    /// </summary>
    public CompiledProgram Program => _program;

    public CompiledClass? GetClass(string name)
    {
        return _program.GetClass(name);
    }

    public IEnumerable<CompiledClass> GetAllClasses()
    {
        return _program.Classes.Values;
    }

    public object? GetFirstObject(string className)
    {
        var cls = GetClass(className);
        return cls?.FirstObject;
    }

    /// <summary>
    /// Get the last object of a class.
    /// </summary>
    public RuntimeObject? GetLastObject(string className)
    {
        var cls = GetClass(className);
        return cls?.LastObject;
    }

    /// <summary>
    /// Get object count for a class.
    /// </summary>
    public int GetObjectCount(string className)
    {
        var cls = GetClass(className);
        return cls?.ObjectCount ?? 0;
    }

    /// <summary>
    /// Initialize common (static) variables for all classes.
    /// </summary>
    public void InitializeCommonVariables(ExecutionContext context)
    {
        if (_commonVariablesInitialized)
            return;

        foreach (var cls in _program.Classes.Values)
        {
            InitializeClassCommonVariables(cls, context);
        }

        _commonVariablesInitialized = true;
    }

    private void InitializeClassCommonVariables(CompiledClass cls, ExecutionContext context)
    {
        foreach (var variable in cls.Variables.Values)
        {
            if (!variable.IsComum)
                continue;

            // Skip if already initialized
            if (cls.CommonVariables.ContainsKey(variable.Name))
                continue;

            var value = Values.RuntimeValue.Null;

            if (variable.Initializer != null)
            {
                try
                {
                    var interpreter = new AstInterpreter(context);
                    value = interpreter.EvaluateExpression(variable.Initializer);
                }
                catch
                {
                    // Ignore initialization errors for common variables
                }
            }

            cls.CommonVariables[variable.Name] = value;
        }
    }

    /// <summary>
    /// Create a new object of a class.
    /// </summary>
    public RuntimeObject CreateObject(string className, ExecutionContext context)
    {
        var cls = GetClass(className)
            ?? throw new ExecutionException($"Class not found: {className}");

        return CreateObject(cls, context);
    }

    /// <summary>
    /// Create a new object of a class.
    /// </summary>
    public RuntimeObject CreateObject(CompiledClass cls, ExecutionContext context)
    {
        var obj = new RuntimeObject
        {
            Class = cls,
            Id = _nextObjectId++
        };

        // Initialize variables with default values
        InitializeObjectVariables(obj, cls, context);

        // Add to class object list
        obj.IndexInClass = cls.Objects.Count;
        cls.Objects.Add(obj);
        cls.TotalObjectsCreated++;

        return obj;
    }

    private void InitializeObjectVariables(RuntimeObject obj, CompiledClass cls, ExecutionContext context)
    {
        // Initialize base class variables first
        foreach (var baseClass in cls.ResolvedBases)
        {
            InitializeObjectVariables(obj, baseClass, context);
        }

        // Initialize this class's variables
        foreach (var variable in cls.Variables.Values)
        {
            if (variable.IsComum)
                continue;  // Comum variables are shared

            var value = Values.RuntimeValue.Null;

            if (variable.Initializer != null)
            {
                var savedObj = context.CurrentObject;
                context.CurrentObject = obj;
                try
                {
                    var interpreter = new AstInterpreter(context);
                    value = interpreter.EvaluateExpression(variable.Initializer);
                }
                finally
                {
                    context.CurrentObject = savedObj;
                }
            }

            obj.SetVariable(variable.Name, value);
        }
    }

    /// <summary>
    /// Mark an object for deletion.
    /// </summary>
    public void MarkForDeletion(RuntimeObject obj)
    {
        if (!obj.IsMarkedForDeletion)
        {
            obj.IsMarkedForDeletion = true;
            _objectsToDelete.Add(obj);
        }
    }

    /// <summary>
    /// Delete an object immediately.
    /// </summary>
    public void DeleteObject(RuntimeObject obj)
    {
        obj.IsMarkedForDeletion = true;
        var idx = obj.Class.Objects.IndexOf(obj);
        if (idx >= 0)
        {
            obj.Class.Objects.RemoveAt(idx);

            // Update indices for remaining objects
            for (int i = idx; i < obj.Class.Objects.Count; i++)
            {
                obj.Class.Objects[i].IndexInClass = i;
            }
        }
    }

    /// <summary>
    /// Process pending object deletions.
    /// </summary>
    public void ProcessDeletions()
    {
        foreach (var obj in _objectsToDelete)
        {
            if (obj.ReferenceCount <= 0)
            {
                DeleteObject(obj);
            }
        }
        _objectsToDelete.Clear();
    }

    /// <summary>
    /// Get object by class and index.
    /// </summary>
    public RuntimeObject? GetObjectByIndex(string className, int index)
    {
        var cls = GetClass(className);
        if (cls == null || index < 0 || index >= cls.Objects.Count)
            return null;
        return cls.Objects[index];
    }

    /// <summary>
    /// Get object by ID across all classes.
    /// </summary>
    public RuntimeObject? GetObjectById(long id)
    {
        foreach (var cls in _program.Classes.Values)
        {
            foreach (var obj in cls.Objects)
            {
                if (obj.Id == id)
                    return obj;
            }
        }
        return null;
    }

    /// <summary>
    /// Find objects matching a predicate.
    /// </summary>
    public IEnumerable<RuntimeObject> FindObjects(string className, Func<RuntimeObject, bool> predicate)
    {
        var cls = GetClass(className);
        if (cls == null)
            yield break;

        foreach (var obj in cls.Objects)
        {
            if (!obj.IsMarkedForDeletion && predicate(obj))
                yield return obj;
        }
    }

    /// <summary>
    /// Iterate all objects of a class.
    /// </summary>
    public IEnumerable<RuntimeObject> GetObjects(string className)
    {
        var cls = GetClass(className);
        if (cls == null)
            yield break;

        foreach (var obj in cls.Objects)
        {
            if (!obj.IsMarkedForDeletion)
                yield return obj;
        }
    }

    /// <summary>
    /// Reset object iteration for a class.
    /// </summary>
    public void ResetObjectIteration(string className)
    {
        var cls = GetClass(className);
        if (cls != null)
            cls.ObjectIndex = 0;
    }

    /// <summary>
    /// Get next object in iteration.
    /// </summary>
    public RuntimeObject? GetNextObject(string className)
    {
        var cls = GetClass(className);
        if (cls == null)
            return null;

        while (cls.ObjectIndex < cls.Objects.Count)
        {
            var obj = cls.Objects[cls.ObjectIndex++];
            if (!obj.IsMarkedForDeletion)
                return obj;
        }

        return null;
    }

    /// <summary>
    /// Check if iteration has more objects.
    /// </summary>
    public bool HasMoreObjects(string className)
    {
        var cls = GetClass(className);
        return cls != null && cls.ObjectIndex < cls.Objects.Count;
    }
}
