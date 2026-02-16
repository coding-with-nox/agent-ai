using Noxvis.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Noxvis.Application.DependencyInjection;

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
    public static IServiceCollection AddNoxvisApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(StackGuardBehavior<,>));
        return services;
    }
}
