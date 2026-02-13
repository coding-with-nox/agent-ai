using Codex.Core.Models;

namespace Codex.Core.Interfaces;

/// <summary>
/// Provides a named stack preset.
/// </summary>
public interface IStackPreset
{
    /// <summary>
    /// Gets the preset identifier.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the stack configuration instance.
    /// </summary>
    /// <returns>Stack config for this preset.</returns>
    StackConfig Build();
}
