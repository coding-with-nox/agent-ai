using Noxvis.Core.Interfaces;
using Noxvis.Core.Models;

namespace Noxvis.Infrastructure.Stack.Presets;

/// <summary>
/// Built-in preset for laravel-modular projects.
/// </summary>
public sealed class LaravelModularPreset : IStackPreset
{
    /// <inheritdoc/>
    public string Name => "laravel-modular";

    /// <inheritdoc/>
    public StackConfig Build()
    {
        return new StackConfig
        {
            Name = Name,
            Language = "PHP",
            Framework = "Laravel",
            Commands = new Dictionary<string, string>
            {
                ["lint"] = "./vendor/bin/pint --test",
                ["build"] = "php artisan test",
                ["test"] = "php artisan test"
            },
            Conventions = new[]
            {
                "clean-architecture",
                "cqrs",
                "structured-logging"
            },
            CustomRules = new[]
            {
                "no-placeholder-code",
                "max-300-lines-per-file",
                "xml-docs-for-public-members"
            }
        };
    }
}
