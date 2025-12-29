using IntMud.Core.Instructions;
using IntMud.Core.Registry;
using IntMud.Types.Handlers;

namespace IntMud.Types.Registry;

/// <summary>
/// Registry for variable type handlers.
/// Provides lookup and management of all variable type handlers.
/// </summary>
public sealed class VariableTypeRegistry : IVariableTypeRegistry
{
    private readonly Dictionary<OpCode, IVariableTypeHandler> _handlers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Create a new registry with all basic type handlers pre-registered.
    /// </summary>
    public static VariableTypeRegistry CreateWithBasicTypes()
    {
        var registry = new VariableTypeRegistry();
        registry.RegisterBasicTypes();
        return registry;
    }

    /// <summary>
    /// Create a new registry with all type handlers pre-registered (basic + complex).
    /// </summary>
    public static VariableTypeRegistry CreateWithAllTypes()
    {
        var registry = new VariableTypeRegistry();
        registry.RegisterAllTypes();
        return registry;
    }

    /// <inheritdoc />
    public void Register(IVariableTypeHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            _handlers[handler.OpCode] = handler;
        }
    }

    /// <inheritdoc />
    public IVariableTypeHandler? GetHandler(OpCode opCode)
    {
        lock (_lock)
        {
            return _handlers.GetValueOrDefault(opCode);
        }
    }

    /// <inheritdoc />
    public IEnumerable<IVariableTypeHandler> GetAllHandlers()
    {
        lock (_lock)
        {
            return _handlers.Values.ToList();
        }
    }

    /// <inheritdoc />
    public bool HasHandler(OpCode opCode)
    {
        lock (_lock)
        {
            return _handlers.ContainsKey(opCode);
        }
    }

    /// <summary>
    /// Register all basic type handlers.
    /// </summary>
    public void RegisterBasicTypes()
    {
        // Integer types
        Register(new Int1Handler());
        Register(new Int8Handler());
        Register(new UInt8Handler());
        Register(new Int16Handler());
        Register(new UInt16Handler());
        Register(new Int32Handler());
        Register(new UInt32Handler());

        // Float types
        Register(new RealHandler());
        Register(new Real2Handler());

        // Text types (default sizes)
        Register(new Txt1Handler(256));
        Register(new Txt2Handler(512));

        // Reference types
        Register(new RefHandler());
    }

    /// <summary>
    /// Register all complex type handlers.
    /// </summary>
    public void RegisterComplexTypes()
    {
        // List types
        Register(new ListaObjHandler());
        Register(new ListaItemHandler());

        // Complex text types
        Register(new TextoTxtHandler());
        Register(new TextoPosHandler());
        Register(new NomeObjHandler());

        // File types
        Register(new ArqTxtHandler());
        Register(new ArqMemHandler());
        Register(new ArqDirHandler());
        Register(new ArqLogHandler());

        // Time and counter types
        Register(new IntTempoHandler());
        Register(new IntExecHandler());
        Register(new DataHoraHandler());
        Register(new IntIncHandler());
        Register(new IntDecHandler());

        // Miscellaneous types
        Register(new IndiceObjHandler());
        Register(new IndiceItemHandler());
        Register(new DebugHandler());
        Register(new ProgHandler());
        Register(new TelaTxtHandler());

        // Networking types
        Register(new SocketHandler());
        Register(new ServerHandler());
    }

    /// <summary>
    /// Register all type handlers (basic + complex).
    /// </summary>
    public void RegisterAllTypes()
    {
        RegisterBasicTypes();
        RegisterComplexTypes();
    }

    /// <summary>
    /// Get handler for a text type with specific size.
    /// </summary>
    public IVariableTypeHandler GetTextHandler(int maxLength)
    {
        return TextHandlerFactory.Create(maxLength);
    }

    /// <summary>
    /// Number of registered handlers.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _handlers.Count;
            }
        }
    }
}
