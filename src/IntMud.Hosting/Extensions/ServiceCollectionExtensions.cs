using IntMud.Hosting.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntMud.Hosting.Extensions;

/// <summary>
/// Extension methods for service collection configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add IntMUD core services.
    /// </summary>
    public static IServiceCollection AddIntMud(this IServiceCollection services, Action<IntMudHostOptions>? configure = null)
    {
        var options = new IntMudHostOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IntMudEngine>();
        services.AddSingleton<IntMudMetrics>();

        return services;
    }

    /// <summary>
    /// Add IntMUD hosted service.
    /// </summary>
    public static IServiceCollection AddIntMudHostedService(this IServiceCollection services)
    {
        services.AddHostedService<IntMudHostedService>();
        return services;
    }

    /// <summary>
    /// Add hot-reload support.
    /// </summary>
    public static IServiceCollection AddIntMudHotReload(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IntMudHostOptions>();
            var logger = sp.GetRequiredService<ILogger<HotReloadWatcher>>();
            return new HotReloadWatcher(options.SourcePath, logger);
        });

        return services;
    }
}
