using Codex.Core.Interfaces;
using Codex.Core.Models;

namespace Codex.Infrastructure.Stack.Presets;

/// <summary>
/// Built-in preset for spring-ddd projects.
/// </summary>
public sealed class SpringDddPreset : IStackPreset
{
    /// <inheritdoc/>
    public string Name => "spring-ddd";

    /// <inheritdoc/>
    public StackConfig Build()
    {
        return new StackConfig
        {
            Name = Name,
            Language = "Java",
            Framework = "Spring Boot",
            Commands = new Dictionary<string, string>
            {
                ["lint"] = "./gradlew spotlessCheck",
                ["build"] = "./gradlew build",
                ["test"] = "./gradlew test"
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
