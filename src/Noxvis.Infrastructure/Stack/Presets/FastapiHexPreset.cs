using Noxvis.Core.Interfaces;
using Noxvis.Core.Models;

namespace Noxvis.Infrastructure.Stack.Presets;

/// <summary>
/// Built-in preset for fastapi-hex projects.
/// </summary>
public sealed class FastapiHexPreset : IStackPreset
{
    /// <inheritdoc/>
    public string Name => "fastapi-hex";

    /// <inheritdoc/>
    public StackConfig Build()
    {
        return new StackConfig
        {
            Name = Name,
            Language = "Python",
            Framework = "FastAPI",
            Commands = new Dictionary<string, string>
            {
                ["lint"] = "ruff check .",
                ["build"] = "mypy .",
                ["test"] = "pytest"
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
