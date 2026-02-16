using NocodeX.Core.Interfaces;
using NocodeX.Core.Models;

namespace NocodeX.Infrastructure.Stack.Presets;

/// <summary>
/// Built-in preset for nextjs-fullstack projects.
/// </summary>
public sealed class NextjsFullstackPreset : IStackPreset
{
    /// <inheritdoc/>
    public string Name => "nextjs-fullstack";

    /// <inheritdoc/>
    public StackConfig Build()
    {
        return new StackConfig
        {
            Name = Name,
            Language = "TypeScript",
            Framework = "Next.js",
            Commands = new Dictionary<string, string>
            {
                ["lint"] = "pnpm lint",
                ["build"] = "pnpm typecheck",
                ["test"] = "pnpm test"
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
