using Codex.Core.Models;

namespace Codex.Core.Interfaces;

/// <summary>
/// Manages stack presets and active stack selection.
/// </summary>
public interface IStackRegistry
{
    /// <summary>
    /// Gets the active stack configuration.
    /// </summary>
    StackConfig? Current { get; }

    /// <summary>
    /// Sets the active stack by preset name.
    /// </summary>
    /// <param name="presetName">Preset identifier.</param>
    void Set(string presetName);

    /// <summary>
    /// Gets all available presets.
    /// </summary>
    /// <returns>Preset names.</returns>
    IReadOnlyList<string> Presets();

    /// <summary>
    /// Validates a stack configuration.
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    /// <returns>Validation errors, or empty if valid.</returns>
    IReadOnlyList<string> Validate(StackConfig config);
}
