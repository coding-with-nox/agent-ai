using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using MediatR;

namespace NocodeX.Application.Llm;

/// <summary>
/// Handles LLM status queries by checking all provider health.
/// </summary>
public sealed class LlmStatusHandler
    : IRequestHandler<LlmStatusQuery, IReadOnlyDictionary<string, LlmHealthStatus>>
{
    private readonly ILlmClientManager _clientManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmStatusHandler"/> class.
    /// </summary>
    public LlmStatusHandler(ILlmClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, LlmHealthStatus>> Handle(
        LlmStatusQuery request, CancellationToken cancellationToken)
    {
        return await _clientManager.CheckAllHealthAsync(cancellationToken);
    }
}
