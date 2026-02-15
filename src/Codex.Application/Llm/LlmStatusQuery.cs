using Codex.Core.Models.Llm;
using MediatR;

namespace Codex.Application.Llm;

/// <summary>
/// Queries the health status of all LLM providers.
/// </summary>
public sealed record LlmStatusQuery : IRequest<IReadOnlyDictionary<string, LlmHealthStatus>>;
