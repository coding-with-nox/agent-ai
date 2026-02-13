using Codex.Core.Models.Llm;
using MediatR;

namespace Codex.Application.Llm;

/// <summary>
/// Query for all provider health states.
/// </summary>
public sealed record LlmStatusQuery : IRequest<IReadOnlyDictionary<string, LlmHealthStatus>>;
