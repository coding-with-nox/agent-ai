using Noxvis.Core.Interfaces;
using Noxvis.Core.Models;

namespace Noxvis.Infrastructure.Stack.Presets;

/// <summary>
/// Built-in preset for go-micro projects.
/// </summary>
public sealed class GoMicroPreset : IStackPreset
{
    /// <inheritdoc/>
    public string Name => "go-micro";

    /// <inheritdoc/>
    public StackConfig Build()
    {
        return new StackConfig
        {
            Name = Name,
            Language = "Go",
            Framework = "Go kit",
            Commands = new Dictionary<string, string>
            {
                ["lint"] = "golangci-lint run",
                ["build"] = "go test ./...",
                ["test"] = "go test ./..."
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
