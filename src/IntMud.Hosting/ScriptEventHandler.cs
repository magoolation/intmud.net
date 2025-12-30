using IntMud.Compiler.Bytecode;
using IntMud.Runtime.Execution;
using IntMud.Runtime.Values;
using Microsoft.Extensions.Logging;

namespace IntMud.Hosting;

/// <summary>
/// Handles script events and allows IntMUD scripts to control game logic.
/// </summary>
public sealed class ScriptEventHandler
{
    private readonly ILogger<ScriptEventHandler> _logger;
    private readonly Dictionary<string, CompiledUnit> _compiledUnits;
    private CompiledUnit? _mainUnit;
    private string _mainClassName = "main";
    private BytecodeInterpreter? _persistentInterpreter;
    private BytecodeRuntimeObject? _mainInstance;

    public ScriptEventHandler(
        ILogger<ScriptEventHandler> logger,
        Dictionary<string, CompiledUnit> compiledUnits)
    {
        _logger = logger;
        _compiledUnits = compiledUnits;
    }

    /// <summary>
    /// Set the main script class that handles events.
    /// </summary>
    public void SetMainClass(string className)
    {
        _mainClassName = className;
        if (_compiledUnits.TryGetValue(className, out var unit))
        {
            _mainUnit = unit;
            _logger.LogInformation("Main event handler class set to '{ClassName}'", className);

            // Create persistent interpreter and call initialization
            InitializePersistentInterpreter();
        }
        else
        {
            _logger.LogWarning("Main class '{ClassName}' not found", className);
        }
    }

    private void InitializePersistentInterpreter()
    {
        if (_mainUnit == null) return;

        _persistentInterpreter = new BytecodeInterpreter(_mainUnit, _compiledUnits.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase));

        // Create an instance of the main class to hold instance fields
        _mainInstance = new BytecodeRuntimeObject(_mainUnit);

        // Call initialization function if it exists
        if (_mainUnit.Functions.TryGetValue("inicializar", out var initFunc))
        {
            try
            {
                _persistentInterpreter.ExecuteFunctionWithThis(initFunc, _mainInstance, []);
                _logger.LogInformation("Script initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in script initialization");
            }
        }
    }

    /// <summary>
    /// Refresh the main unit reference (after hot-reload).
    /// </summary>
    public void RefreshMainUnit()
    {
        if (_compiledUnits.TryGetValue(_mainClassName, out var unit))
        {
            _mainUnit = unit;
            InitializePersistentInterpreter();
        }
    }

    /// <summary>
    /// Called when a player connects.
    /// </summary>
    public async Task<bool> OnConnectAsync(PlayerSession session)
    {
        return await InvokeEventAsync("aoconectar", session, null, null);
    }

    /// <summary>
    /// Called when a player disconnects.
    /// </summary>
    public async Task<bool> OnDisconnectAsync(PlayerSession session)
    {
        return await InvokeEventAsync("aodesconectar", session, null, null);
    }

    /// <summary>
    /// Called when a player sends a command.
    /// Returns true if the command was handled by the script.
    /// </summary>
    public async Task<bool> OnCommandAsync(PlayerSession session, string command, string args)
    {
        return await InvokeEventAsync("aocomando", session, command, args);
    }

    /// <summary>
    /// Called on each game tick.
    /// </summary>
    public async Task OnTickAsync()
    {
        if (_mainUnit == null || _persistentInterpreter == null || _mainInstance == null) return;

        if (_mainUnit.Functions.TryGetValue("aotick", out var tickFunc))
        {
            try
            {
                _persistentInterpreter.ExecuteFunctionWithThis(tickFunc, _mainInstance, []);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in aotick event");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Call a custom event handler.
    /// </summary>
    public RuntimeValue CallEvent(string eventName, params RuntimeValue[] args)
    {
        if (_mainUnit == null || _persistentInterpreter == null || _mainInstance == null) return RuntimeValue.Null;

        if (!_mainUnit.Functions.TryGetValue(eventName, out var eventFunc))
            return RuntimeValue.Null;

        try
        {
            return _persistentInterpreter.ExecuteFunctionWithThis(eventFunc, _mainInstance, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling event '{EventName}'", eventName);
            return RuntimeValue.Null;
        }
    }

    private async Task<bool> InvokeEventAsync(string eventName, PlayerSession session, string? command, string? args)
    {
        if (_mainUnit == null || _persistentInterpreter == null || _mainInstance == null) return false;

        if (!_mainUnit.Functions.TryGetValue(eventName, out var eventFunc))
            return false;

        try
        {
            // Set up output to go to the session with color parsing
            _persistentInterpreter.WriteOutput = text =>
            {
                var coloredText = AnsiColors.ParseColorCodes(text);
                session.QueueOutput(coloredText);
            };

            // Build arguments based on event type
            var funcArgs = new List<RuntimeValue>
            {
                RuntimeValue.FromInt(session.Id)
            };

            if (command != null)
            {
                funcArgs.Add(RuntimeValue.FromString(command));
                funcArgs.Add(RuntimeValue.FromString(args ?? ""));
            }

            var result = _persistentInterpreter.ExecuteFunctionWithThis(eventFunc, _mainInstance, funcArgs.ToArray());

            // Clear the output buffer
            _persistentInterpreter.ClearOutputBuffer();

            // Flush queued output to the session
            await session.FlushOutputAsync();

            // Return true if the script handled the event (returned non-zero/non-null)
            return result.IsTruthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking event '{EventName}'", eventName);
            return false;
        }
    }
}
