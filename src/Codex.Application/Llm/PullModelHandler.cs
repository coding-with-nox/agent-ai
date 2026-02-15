using Codex.Core.Interfaces;
using Codex.Core.Models;
using MediatR;

namespace Codex.Application.Llm;

/// <summary>
/// Handles model pull commands by delegating to the appropriate provider.
/// </summary>
public sealed class PullModelHandler : IRequestHandler<PullModelCommand, TaskResult>
{
    private readonly ILlmClientManager _clientManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PullModelHandler"/> class.
    /// </summary>
    public PullModelHandler(ILlmClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    /// <inheritdoc/>
    public async Task<TaskResult> Handle(
        PullModelCommand request, CancellationToken cancellationToken)
    {
        ILlmProvider provider = request.ProviderId is not null
            ? _clientManager.GetProvider(request.ProviderId)
            : _clientManager.Primary;

        bool success = await provider.EnsureModelLoadedAsync(
            request.ModelId, cancellationToken);

        return success
            ? TaskResult.Success($"Model '{request.ModelId}' is ready on provider '{provider.ProviderId}'.")
            : TaskResult.Failed(
                $"Failed to load model '{request.ModelId}' on provider '{provider.ProviderId}'. " +
                "The provider may not support model pulling.");
    }
}
