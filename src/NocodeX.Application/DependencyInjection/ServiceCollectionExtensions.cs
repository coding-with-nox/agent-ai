using NocodeX.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace NocodeX.Application.DependencyInjection;

/// <summary>
/// Registers application-layer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds application services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated collection.</returns>
    public static IServiceCollection AddNocodeXApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(StackGuardBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LlmHealthGuardBehavior<,>));

        return services;
    }
}
