using Noxvis.Core.Interfaces;
using Noxvis.Core.Models;

namespace Noxvis.Infrastructure.Stack.Presets;

/// <summary>
/// Built-in preset for rust-axum projects.
/// </summary>
public sealed class RustAxumPreset : IStackPreset
{
    /// <inheritdoc/>
    public string Name => "rust-axum";

    /// <inheritdoc/>
    public StackConfig Build()
    {
        return new StackConfig
        {
            Name = Name,
            Language = "Rust",
            Framework = "Axum",
            Commands = new Dictionary<string, string>
            {
                ["lint"] = "cargo fmt --check",
                ["build"] = "cargo build",
                ["test"] = "cargo test"
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
