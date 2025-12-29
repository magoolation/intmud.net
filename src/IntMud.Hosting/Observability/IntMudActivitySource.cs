using System.Diagnostics;

namespace IntMud.Hosting.Observability;

/// <summary>
/// Activity source for distributed tracing.
/// </summary>
public static class IntMudActivitySource
{
    /// <summary>
    /// The activity source for IntMUD operations.
    /// </summary>
    public static readonly ActivitySource Source = new("IntMud.Engine", "1.0.0");

    /// <summary>
    /// Start a new activity for function execution.
    /// </summary>
    public static Activity? StartFunctionExecution(string functionName, string? className = null)
    {
        var activity = Source.StartActivity("function.execute", ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("intmud.function.name", functionName);
            if (className != null)
            {
                activity.SetTag("intmud.class.name", className);
            }
        }
        return activity;
    }

    /// <summary>
    /// Start a new activity for network operation.
    /// </summary>
    public static Activity? StartNetworkOperation(string operation, string? remoteAddress = null, int? remotePort = null)
    {
        var activity = Source.StartActivity($"network.{operation}", ActivityKind.Client);
        if (activity != null)
        {
            activity.SetTag("intmud.network.operation", operation);
            if (remoteAddress != null)
            {
                activity.SetTag("net.peer.name", remoteAddress);
            }
            if (remotePort != null)
            {
                activity.SetTag("net.peer.port", remotePort);
            }
        }
        return activity;
    }

    /// <summary>
    /// Start a new activity for compilation.
    /// </summary>
    public static Activity? StartCompilation(string fileName)
    {
        var activity = Source.StartActivity("compile", ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("intmud.compile.file", fileName);
        }
        return activity;
    }

    /// <summary>
    /// Start a new activity for object creation.
    /// </summary>
    public static Activity? StartObjectCreation(string className)
    {
        var activity = Source.StartActivity("object.create", ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("intmud.class.name", className);
        }
        return activity;
    }

    /// <summary>
    /// Record an error on the current activity.
    /// </summary>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity == null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddTag("exception.type", exception.GetType().FullName);
        activity.AddTag("exception.message", exception.Message);
        activity.AddTag("exception.stacktrace", exception.StackTrace);
    }
}
