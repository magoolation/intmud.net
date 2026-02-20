using System.Text;
using IntMud.Runtime.Values;

namespace IntMud.Runtime.Types;

/// <summary>
/// Represents an arqsav (save file) instance.
/// For saving and loading object data to/from files.
/// </summary>
public sealed class ArqSavInstance
{
    private string _senha = "";

    /// <summary>
    /// The owner object that contains this arqsav variable.
    /// </summary>
    public object? Owner { get; set; }

    /// <summary>
    /// The variable name.
    /// </summary>
    public string VariableName { get; set; } = "";

    /// <summary>
    /// Check if file exists.
    /// </summary>
    public bool Existe(string filename) => File.Exists(filename);

    /// <summary>
    /// Check if file is valid.
    /// </summary>
    public bool Valido(string filename) => File.Exists(filename);

    /// <summary>
    /// Set password for encrypted files.
    /// </summary>
    public void Senha(string senha)
    {
        _senha = senha ?? "";
    }

    /// <summary>
    /// Get file age in days.
    /// </summary>
    public int Dias(string filename)
    {
        try
        {
            if (!File.Exists(filename))
                return -1;
            var lastWrite = File.GetLastWriteTime(filename);
            return (int)(DateTime.Now - lastWrite).TotalDays;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Read objects from a save file.
    /// </summary>
    /// <param name="filename">File to read from.</param>
    /// <param name="lista">ListaObj containing objects to populate.</param>
    /// <param name="mode">Read mode (0=normal, 1=merge).</param>
    /// <returns>1 if successful, 0 if failed.</returns>
    public int Ler(string filename, ListaObjInstance? lista, int mode)
    {
        try
        {
            if (!File.Exists(filename) || lista == null)
                return 0;

            var lines = File.ReadAllLines(filename, Encoding.Latin1);

            // Simple format: each line is "objectName.varName=value"
            // For now, look for textotxt variables in the objects
            BytecodeRuntimeObject? currentObj = null;
            TextoTxtInstance? currentTextTxt = null;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Check for object header [ClassName]
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var className = line.Trim('[', ']');
                    // Find matching object in lista
                    foreach (var obj in lista.Objects)
                    {
                        if (obj is BytecodeRuntimeObject runtimeObj &&
                            runtimeObj.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
                        {
                            currentObj = runtimeObj;
                            break;
                        }
                    }
                    currentTextTxt = null;
                    continue;
                }

                // Check for textotxt variable header {varName}
                if (line.StartsWith("{") && line.EndsWith("}"))
                {
                    var varName = line.Trim('{', '}');
                    if (currentObj != null)
                    {
                        var field = currentObj.GetField(varName);
                        if (field.Type == RuntimeValueType.Object && field.AsObject() is TextoTxtInstance txtInstance)
                        {
                            currentTextTxt = txtInstance;
                            currentTextTxt.Limpar();
                        }
                    }
                    continue;
                }

                // Regular line - add to current textotxt
                if (currentTextTxt != null)
                {
                    currentTextTxt.AddFim(line);
                }
                // Or try to set as field value
                else if (currentObj != null && line.Contains('='))
                {
                    var eqPos = line.IndexOf('=');
                    var varName = line.Substring(0, eqPos).Trim();
                    var value = line.Substring(eqPos + 1);
                    currentObj.SetField(varName, RuntimeValue.FromString(value));
                }
            }

            return 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Save objects to a file.
    /// </summary>
    /// <param name="filename">File to save to.</param>
    /// <param name="lista">ListaObj containing objects to save.</param>
    /// <param name="mode">Save mode.</param>
    /// <param name="param">Additional parameters.</param>
    /// <returns>1 if successful, 0 if failed.</returns>
    public int Salvar(string filename, ListaObjInstance? lista, int mode, string param)
    {
        try
        {
            if (lista == null)
                return 0;

            using var writer = new StreamWriter(filename, false, Encoding.Latin1);

            foreach (var obj in lista.Objects)
            {
                if (obj is BytecodeRuntimeObject runtimeObj)
                {
                    // Write object header
                    writer.WriteLine($"[{runtimeObj.ClassName}]");

                    // Write textotxt variables
                    foreach (var unit in runtimeObj.ClassHierarchy)
                    {
                        foreach (var variable in unit.Variables)
                        {
                            // Check for sav modifier (simplified - check if type is saveable)
                            var field = runtimeObj.GetField(variable.Name);

                            if (field.Type == RuntimeValueType.Object && field.AsObject() is TextoTxtInstance txtInstance)
                            {
                                writer.WriteLine($"{{{variable.Name}}}");
                                for (int i = 0; i < txtInstance.Linhas; i++)
                                {
                                    writer.WriteLine(txtInstance.GetLine(i));
                                }
                            }
                            else if (field.Type != RuntimeValueType.Null && field.Type != RuntimeValueType.Object)
                            {
                                writer.WriteLine($"{variable.Name}={field.AsString()}");
                            }
                        }
                    }

                    writer.WriteLine();
                }
            }

            return 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Delete a save file.
    /// </summary>
    public bool Apagar(string filename)
    {
        try
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clear (truncate) a save file.
    /// </summary>
    public bool Limpar(string filename)
    {
        try
        {
            File.WriteAllText(filename, "", Encoding.Latin1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override string ToString() => "[ArqSav]";
}
