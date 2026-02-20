using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents a prog (program introspection) instance.
/// Maps to C++ TVarProg - provides access to program metadata.
/// Allows scripts to enumerate files, classes, functions, variables, etc.
///
/// Uses a state machine (_consulta) to track the current iteration mode,
/// matching the C++ pattern where texto/depois/lin dispatch based on mode.
/// </summary>
public sealed class ProgInstance
{
    // Registry of all compiled units (set by interpreter before method calls)
    private Dictionary<string, CompiledUnit>? _loadedUnits;

    // State machine for iteration (matches C++ consulta enum)
    private enum Consulta
    {
        None,
        Arquivo,    // File iteration
        Classe,     // Class iteration
        Func,       // Function-only iteration
        FuncTudo,   // All items (func + var) iteration
        FuncCl,     // Functions in class source order
        Herda,      // Direct base classes
        HerdaTudo,  // All ancestors
        HerdaInv,   // Derived classes (inverse inheritance)
        LinhaCl,    // Lines of class
        LinhaFunc,  // Lines of function
    }

    private Consulta _consulta = Consulta.None;
    private List<string> _iterItems = new();
    private int _iterIndex;

    // Context preserved across iteration mode changes
    private string _currentClass = "";
    private int _currentLine;

    public object? Owner { get; set; }
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Set the registry of loaded compiled units. Called by the interpreter.
    /// </summary>
    public void SetRegistry(Dictionary<string, CompiledUnit> loadedUnits)
    {
        _loadedUnits = loadedUnits;
    }

    // ---- File iteration ----

    /// <summary>
    /// Start file iteration. C++ FuncIniArq.
    /// Args: optional file/class name pattern to filter.
    /// </summary>
    public string IniArq(string pattern = "")
    {
        var files = GetSourceFiles();
        if (!string.IsNullOrEmpty(pattern))
        {
            // If pattern matches a class name, get its file
            var unit = GetUnit(pattern);
            if (unit != null && !string.IsNullOrEmpty(unit.SourceFile))
            {
                _iterItems = new List<string> { unit.SourceFile };
            }
            else
            {
                // Filter files matching pattern prefix
                _iterItems = files
                    .Where(f => f.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
        else
        {
            _iterItems = files;
        }

        _consulta = Consulta.Arquivo;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
            return _iterItems[0];
        _consulta = Consulta.None;
        return "";
    }

    /// <summary>Get file path for a class. C++ FuncArquivo.</summary>
    public string Arquivo(string className = "")
    {
        if (!string.IsNullOrEmpty(className))
        {
            var unit = GetUnit(className);
            return unit?.SourceFile ?? "";
        }
        // No arg: return current file from iteration
        if (_consulta == Consulta.Arquivo && _iterIndex < _iterItems.Count)
            return _iterItems[_iterIndex];
        return "";
    }

    /// <summary>Get file name (without path) for a class. C++ FuncArqNome.</summary>
    public string ArqNome(string className = "")
    {
        var file = Arquivo(className);
        return string.IsNullOrEmpty(file) ? "" : Path.GetFileName(file);
    }

    // ---- Class iteration ----

    /// <summary>
    /// Start class iteration. C++ FuncIniClasse.
    /// Args: optional class name prefix pattern.
    /// </summary>
    public string IniClasse(string pattern = "")
    {
        _iterItems = GetAllClasses(pattern);
        _consulta = Consulta.Classe;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
        {
            _currentClass = _iterItems[0];
            return _currentClass;
        }
        _consulta = Consulta.None;
        _currentClass = "";
        return "";
    }

    /// <summary>Get current class name.</summary>
    public string Classe() => _currentClass;

    /// <summary>Move to first class in class iteration.</summary>
    public string ClIni()
    {
        if (_consulta == Consulta.Classe && _iterItems.Count > 0)
        {
            _iterIndex = 0;
            _currentClass = _iterItems[0];
            return _currentClass;
        }
        return "";
    }

    /// <summary>Move to last class in class iteration.</summary>
    public string ClFim()
    {
        if (_consulta == Consulta.Classe && _iterItems.Count > 0)
        {
            _iterIndex = _iterItems.Count - 1;
            _currentClass = _iterItems[_iterIndex];
            return _currentClass;
        }
        return "";
    }

    /// <summary>Move to previous class in class iteration.</summary>
    public string ClAntes()
    {
        if (_consulta == Consulta.Classe && _iterIndex > 0)
        {
            _iterIndex--;
            _currentClass = _iterItems[_iterIndex];
            return _currentClass;
        }
        return "";
    }

    /// <summary>Move to next class in class iteration.</summary>
    public string ClDepois()
    {
        if (_consulta == Consulta.Classe && _iterIndex < _iterItems.Count - 1)
        {
            _iterIndex++;
            _currentClass = _iterItems[_iterIndex];
            return _currentClass;
        }
        return "";
    }

    // ---- Function iteration ----

    /// <summary>
    /// Start function iteration for a class. C++ FuncIniFunc.
    /// Args: className (required), funcPattern (optional prefix filter).
    /// Only iterates functions (not variables).
    /// </summary>
    public string IniFunc(string className, string pattern = "")
    {
        _currentClass = className;
        var unit = GetUnit(className);
        if (unit != null)
        {
            _iterItems = unit.Functions.Keys
                .Where(k => string.IsNullOrEmpty(pattern) ||
                       k.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            _iterItems = new();
        }

        _consulta = Consulta.Func;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
            return _iterItems[0];
        _consulta = Consulta.None;
        return "";
    }

    /// <summary>
    /// Start iteration over ALL items (functions + variables) in a class. C++ FuncIniFuncTudo.
    /// Args: className (required), pattern (optional prefix filter).
    /// </summary>
    public string IniFuncTudo(string className, string pattern = "")
    {
        _currentClass = className;
        var unit = GetUnit(className);
        if (unit != null)
        {
            var items = new List<string>();
            // Add functions
            foreach (var funcName in unit.Functions.Keys)
            {
                if (string.IsNullOrEmpty(pattern) ||
                    funcName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    items.Add(funcName);
            }
            // Add variables (avoid duplicates with functions)
            var funcSet = new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
            foreach (var v in unit.Variables)
            {
                if (!funcSet.Contains(v.Name) &&
                    (string.IsNullOrEmpty(pattern) ||
                     v.Name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    items.Add(v.Name);
                }
            }
            items.Sort(StringComparer.OrdinalIgnoreCase);
            _iterItems = items;
        }
        else
        {
            _iterItems = new();
        }

        _consulta = Consulta.FuncTudo;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
            return _iterItems[0];
        _consulta = Consulta.None;
        return "";
    }

    /// <summary>
    /// Start function iteration in class source order. C++ FuncIniFuncCl.
    /// Args: className (required), pattern (optional prefix filter).
    /// </summary>
    public string IniFuncCl(string className, string pattern = "")
    {
        _currentClass = className;
        var unit = GetUnit(className);
        if (unit != null)
        {
            // Functions in declaration order (dictionary preserves insertion order in modern .NET)
            _iterItems = unit.Functions.Keys
                .Where(k => string.IsNullOrEmpty(pattern) ||
                       k.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            _iterItems = new();
        }

        _consulta = Consulta.FuncCl;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
            return _iterItems[0];
        _consulta = Consulta.None;
        return "";
    }

    // ---- Inheritance iteration ----

    /// <summary>
    /// Start direct base class iteration. C++ FuncIniHerda.
    /// Args: className (required).
    /// </summary>
    public string IniHerda(string className)
    {
        _currentClass = className;
        var unit = GetUnit(className);
        _iterItems = unit != null ? new List<string>(unit.BaseClasses) : new();
        _consulta = Consulta.Herda;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
            return _iterItems[0];
        _consulta = Consulta.None;
        return "";
    }

    /// <summary>
    /// Start all ancestors iteration (recursive). C++ FuncIniHerdaTudo.
    /// Args: className (required).
    /// </summary>
    public string IniHerdaTudo(string className)
    {
        _currentClass = className;
        _iterItems = new List<string>();
        CollectAllBases(className, _iterItems, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _consulta = Consulta.HerdaTudo;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
            return _iterItems[0];
        _consulta = Consulta.None;
        return "";
    }

    /// <summary>
    /// Start inverse inheritance iteration (derived classes). C++ FuncIniHerdaInv.
    /// Args: className (required).
    /// </summary>
    public string IniHerdaInv(string className)
    {
        _currentClass = className;
        _iterItems = new List<string>();
        if (_loadedUnits != null)
        {
            foreach (var unit in _loadedUnits.Values)
            {
                if (unit.BaseClasses.Any(b =>
                    string.Equals(b, className, StringComparison.OrdinalIgnoreCase)))
                {
                    _iterItems.Add(unit.ClassName);
                }
            }
            _iterItems.Sort(StringComparer.OrdinalIgnoreCase);
        }
        _consulta = Consulta.HerdaInv;
        _iterIndex = 0;
        if (_iterItems.Count > 0)
            return _iterItems[0];
        _consulta = Consulta.None;
        return "";
    }

    // ---- Line iteration ----

    /// <summary>
    /// Start line iteration for a class or function. C++ FuncIniLinha.
    /// Args: className (required), funcName (optional - if given, iterate only that function's lines).
    /// </summary>
    public string IniLinha(string className, string funcName = "")
    {
        _currentClass = className;
        _currentLine = 0;

        var unit = GetUnit(className);
        if (unit == null)
        {
            _consulta = Consulta.None;
            _iterItems = new();
            return "";
        }

        if (!string.IsNullOrEmpty(funcName))
        {
            // Lines of a specific function
            if (unit.Functions.TryGetValue(funcName, out var func) && func.LineInfo.Count > 0)
            {
                _iterItems = func.LineInfo
                    .Select(li => li.Line.ToString())
                    .Distinct()
                    .ToList();
            }
            else
            {
                _iterItems = new();
            }
            _consulta = Consulta.LinhaFunc;
        }
        else
        {
            // Lines of the whole class
            var lines = new SortedSet<int>();
            foreach (var func in unit.Functions.Values)
            {
                foreach (var li in func.LineInfo)
                    lines.Add(li.Line);
            }
            _iterItems = lines.Select(l => l.ToString()).ToList();
            _consulta = Consulta.LinhaCl;
        }

        _iterIndex = 0;
        if (_iterItems.Count > 0)
        {
            _currentLine = int.TryParse(_iterItems[0], out var ln) ? ln : 0;
            return _iterItems[0];
        }
        _consulta = Consulta.None;
        return "";
    }

    // ---- Universal iteration methods ----

    /// <summary>
    /// Check if iteration is active. C++ FuncLin.
    /// Returns 0 if no active iteration, current line number for line iteration,
    /// or 1 for other active iterations. Used as loop condition in epara(; p.lin; p.depois).
    /// </summary>
    public int Lin()
    {
        if (_consulta == Consulta.None || _iterIndex >= _iterItems.Count)
            return 0;
        if (_consulta is Consulta.LinhaCl or Consulta.LinhaFunc)
            return _currentLine > 0 ? _currentLine : 1;
        return 1; // Active non-line iteration
    }

    /// <summary>
    /// Get current item text. C++ FuncTexto.
    /// Returns the current item based on the active iteration mode.
    /// </summary>
    public string Texto()
    {
        if (_consulta == Consulta.None || _iterIndex >= _iterItems.Count)
            return "";
        return _iterItems[_iterIndex];
    }

    /// <summary>
    /// Advance to next item. C++ FuncDepois.
    /// Args: optional count (default 1).
    /// Returns text of new current item, or empty string when done.
    /// </summary>
    public string Depois(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            _iterIndex++;
            if (_iterIndex >= _iterItems.Count)
            {
                _consulta = Consulta.None;
                _currentLine = 0;
                return "";
            }
        }

        // Update context based on mode
        if (_consulta is Consulta.LinhaCl or Consulta.LinhaFunc)
        {
            _currentLine = int.TryParse(_iterItems[_iterIndex], out var ln) ? ln : 0;
        }
        else if (_consulta == Consulta.Classe)
        {
            _currentClass = _iterItems[_iterIndex];
        }

        return _iterItems[_iterIndex];
    }

    /// <summary>
    /// Get inheritance depth of current class. C++ FuncNivel.
    /// Returns 0 if no inheritance.
    /// </summary>
    public int Nivel()
    {
        if (string.IsNullOrEmpty(_currentClass)) return 0;
        var unit = GetUnit(_currentClass);
        return unit?.BaseClasses.Count ?? 0;
    }

    // ---- Metadata ----

    /// <summary>
    /// Check if a class or name exists. C++ FuncExiste.
    /// With 1 arg (className): returns 1 if class exists, 0 if not.
    /// With 2 args (className, name): returns non-zero if name exists in class.
    ///   0 = not found, 1 = function, 2 = variable, 3 = constant.
    /// </summary>
    public int Existe(string className, string name = "")
    {
        if (_loadedUnits == null) return 0;

        if (string.IsNullOrEmpty(name))
        {
            // Check if class exists
            return _loadedUnits.ContainsKey(className) ? 1 : 0;
        }

        // Check if name exists in class
        var unit = GetUnit(className);
        if (unit == null) return 0;

        // Check functions
        if (unit.Functions.ContainsKey(name)) return 1;

        // Check variables
        var variable = unit.Variables.FirstOrDefault(
            v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        if (variable != null) return 2;

        // Check constants
        if (unit.Constants.ContainsKey(name)) return 3;

        return 0;
    }

    // ---- Variable info (takes className + varName, matching C++ API) ----

    /// <summary>Check if variable is numeric type. C++ FuncVarNum.</summary>
    public int VarNum(string className, string name)
    {
        var variable = FindVariable(className, name);
        if (variable == null) return 0;
        return IsNumericType(variable.TypeName) ? 1 : 0;
    }

    /// <summary>Check if variable is text type. C++ FuncVarTexto.</summary>
    public int VarTexto(string className, string name)
    {
        var variable = FindVariable(className, name);
        if (variable == null) return 0;
        return IsTextType(variable.TypeName) ? 1 : 0;
    }

    /// <summary>Check if a variable is common (static). C++ FuncVarComum.</summary>
    public int VarComum(string className, string name)
    {
        var variable = FindVariable(className, name);
        return variable?.IsCommon == true ? 1 : 0;
    }

    /// <summary>Check if a variable is saved. C++ FuncVarSav.</summary>
    public int VarSav(string className, string name)
    {
        var variable = FindVariable(className, name);
        return variable?.IsSaved == true ? 1 : 0;
    }

    /// <summary>Get variable type name. C++ FuncVarTipo.</summary>
    public string VarTipo(string className, string name)
    {
        var variable = FindVariable(className, name);
        return variable?.TypeName ?? "";
    }

    /// <summary>Get variable location (class name where defined). C++ FuncVarLugar.</summary>
    public string VarLugar(string className, string name)
    {
        var unit = GetUnit(className);
        if (unit == null) return "";
        var v = unit.Variables.FirstOrDefault(
            variable => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase));
        return v != null ? className : "";
    }

    /// <summary>Get array size of variable (0 if not array). C++ FuncVarVetor.</summary>
    public int VarVetor(string className, string name)
    {
        var variable = FindVariable(className, name);
        if (variable == null) return 0;
        return variable.ArraySize;
    }

    /// <summary>Get constant value as string. C++ FuncConst.</summary>
    public string Const(string className, string constName)
    {
        var unit = GetUnit(className);
        if (unit == null) return "";

        if (unit.Constants.TryGetValue(constName, out var constant))
        {
            return constant.Type switch
            {
                ConstantType.Int => constant.IntValue.ToString(),
                ConstantType.Double => constant.DoubleValue.ToString(),
                ConstantType.String => constant.StringValue ?? "",
                _ => ""
            };
        }
        return "";
    }

    // ---- Modification methods (read-only stubs for now - runtime modification is complex) ----

    public bool Criar(string text1, string text2 = "") => false;
    public bool Apagar(string className, string name = "") => false;
    public bool ApagarLin(string className, int line) => false;
    public bool ApagarLin(string className, string funcName, int line) => false;
    public bool CriarLin(string className, int line, string text) => false;
    public bool CriarLin(string className, string funcName, int line, string text) => false;
    public bool FAntes(string className, string funcName, string refFunc = "") => false;
    public bool FDepois(string className, string funcName, string refFunc = "") => false;
    public bool Renomear(string oldName, string newName) => false;
    public bool Salvar(string filename) => false;
    public bool SalvarTudo() => false;

    // ---- Helpers ----

    private CompiledUnit? GetUnit(string className)
    {
        if (_loadedUnits == null || string.IsNullOrEmpty(className))
            return null;
        _loadedUnits.TryGetValue(className, out var unit);
        return unit;
    }

    private CompiledVariable? FindVariable(string className, string name)
    {
        var unit = GetUnit(className);
        if (unit == null) return null;
        return unit.Variables.FirstOrDefault(
            v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private List<string> GetSourceFiles()
    {
        if (_loadedUnits == null) return new();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in _loadedUnits.Values)
        {
            if (!string.IsNullOrEmpty(unit.SourceFile))
                files.Add(unit.SourceFile);
        }
        return files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<string> GetAllClasses(string pattern = "")
    {
        if (_loadedUnits == null) return new();
        return _loadedUnits.Keys
            .Where(k => string.IsNullOrEmpty(pattern) ||
                   k.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void CollectAllBases(string className, List<string> result, HashSet<string> visited)
    {
        var unit = GetUnit(className);
        if (unit == null) return;

        foreach (var baseName in unit.BaseClasses)
        {
            if (visited.Add(baseName))
            {
                result.Add(baseName);
                CollectAllBases(baseName, result, visited);
            }
        }
    }

    private static bool IsNumericType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        var lower = typeName.ToLowerInvariant();
        return lower.StartsWith("int") || lower.StartsWith("uint") ||
               lower == "real" || lower == "ref";
    }

    private static bool IsTextType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        var lower = typeName.ToLowerInvariant();
        return lower.StartsWith("txt");
    }

    public override string ToString() => "[Prog]";
}
