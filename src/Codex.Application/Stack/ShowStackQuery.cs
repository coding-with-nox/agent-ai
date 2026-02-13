using Codex.Core.Models;
using MediatR;

namespace Codex.Application.Stack;

/// <summary>
/// Returns the active stack configuration.
/// </summary>
public sealed record ShowStackQuery : IRequest<StackConfig?>;
