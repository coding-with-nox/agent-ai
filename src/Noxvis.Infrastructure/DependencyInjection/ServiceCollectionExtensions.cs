using Noxvis.Core.Interfaces;
using Noxvis.Infrastructure.Stack;
using Noxvis.Infrastructure.Stack.Presets;
using Microsoft.Extensions.DependencyInjection;

namespace Noxvis.Infrastructure.DependencyInjection;

/// <summary>
/// Registers infrastructure-layer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds infrastructure services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated collection.</returns>
    public static IServiceCollection AddNoxvisInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IStackPreset, DotnetCleanPreset>();
        services.AddSingleton<IStackPreset, NextjsFullstackPreset>();
        services.AddSingleton<IStackPreset, FastapiHexPreset>();
        services.AddSingleton<IStackPreset, GoMicroPreset>();
        services.AddSingleton<IStackPreset, SpringDddPreset>();
        services.AddSingleton<IStackPreset, RustAxumPreset>();
        services.AddSingleton<IStackPreset, LaravelModularPreset>();
        services.AddSingleton<IStackRegistry, StackRegistry>();
        return services;
    }
}
