using Noxvis.Core.Models;
using FluentValidation;

namespace Noxvis.Application.Common.Validators;

/// <summary>
/// Validates stack configuration payloads.
/// </summary>
public sealed class StackConfigValidator : AbstractValidator<StackConfig>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public StackConfigValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Language).NotEmpty();
        RuleFor(x => x.Framework).NotEmpty();
        RuleFor(x => x.Commands).NotEmpty();
        RuleFor(x => x.Conventions).NotNull();
        RuleFor(x => x.CustomRules).NotNull();
    }
}
