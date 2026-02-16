using NocodeX.Core.Interfaces;
using NocodeX.Core.Models;

namespace NocodeX.Infrastructure.Stack.Presets;

/// <summary>
/// Built-in preset for dotnet-clean projects.
/// </summary>
public sealed class DotnetCleanPreset : IStackPreset
{
    /// <inheritdoc/>
    public string Name => "dotnet-clean";

    /// <inheritdoc/>
    public StackConfig Build()
    {
        return new StackConfig
        {
            Name = Name,
            Language = "C#",
            Framework = ".NET 8 Web API",
            Commands = new Dictionary<string, string>
            {
                ["lint"] = "dotnet format",
                ["build"] = "dotnet build",
                ["test"] = "dotnet test"
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
