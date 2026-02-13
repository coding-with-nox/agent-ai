using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;
using MediatR;

namespace Codex.Application.Llm;

/// <summary>
/// Handles LLM status queries.
/// </summary>
public sealed class LlmStatusHandler : IRequestHandler<LlmStatusQuery, IReadOnlyDictionary<string, LlmHealthStatus>>
{
    private readonly ILlmClientManager _llmClientManager;

    /// <summary>
    /// Initializes the handler.
    /// </summary>
    /// <param name="llmClientManager">LLM manager.</param>
    public LlmStatusHandler(ILlmClientManager llmClientManager)
    {
        _llmClientManager = llmClientManager;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, LlmHealthStatus>> Handle(LlmStatusQuery request, CancellationToken cancellationToken)
    {
        return _llmClientManager.CheckAllHealthAsync(cancellationToken);
    }
}
