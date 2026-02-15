using MediatR;

namespace Noxvis.Application.Stack;

/// <summary>
/// Activates a preset as the current stack.
/// </summary>
/// <param name="Preset">Preset identifier.</param>
public sealed record SetStackCommand(string Preset) : IRequest;
