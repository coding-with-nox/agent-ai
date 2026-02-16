using NocodeX.Application.Common.Validators;
using NocodeX.Core.Interfaces;
using NocodeX.Core.Models;

namespace NocodeX.Infrastructure.Stack;

/// <summary>
/// In-memory stack registry with built-in presets.
/// </summary>
public sealed class StackRegistry : IStackRegistry
{
    private readonly Dictionary<string, IStackPreset> _presets;
    private readonly StackConfigValidator _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="StackRegistry"/> class.
    /// </summary>
    /// <param name="presets">Available stack presets.</param>
    public StackRegistry(IEnumerable<IStackPreset> presets)
    {
        _validator = new StackConfigValidator();
        _presets = presets.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public StackConfig? Current { get; private set; }

    /// <inheritdoc/>
    public void Set(string presetName)
    {
        if (!_presets.TryGetValue(presetName, out IStackPreset? preset))
        {
            string supported = string.Join(", ", _presets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException($"Unknown preset '{presetName}'. Available: {supported}");
        }

        StackConfig config = preset.Build();
        IReadOnlyList<string> errors = Validate(config);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Preset '{presetName}' is invalid: {string.Join("; ", errors)}");
        }

        Current = config;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Presets()
    {
        return _presets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Validate(StackConfig config)
    {
        FluentValidation.Results.ValidationResult validationResult = _validator.Validate(config);
        return validationResult.Errors.Select(x => x.ErrorMessage).ToArray();
    }
}
