using Noxvis.Core.Models;
using MediatR;

namespace Noxvis.Application.Stack;

/// <summary>
/// Returns the active stack configuration.
/// </summary>
public sealed record ShowStackQuery : IRequest<StackConfig?>;
