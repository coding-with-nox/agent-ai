using NocodeX.Core.Models;
using MediatR;

namespace NocodeX.Application.Generation;

/// <summary>
/// Handles endpoint generation requests.
/// </summary>
public sealed class GenEndpointHandler : IRequestHandler<GenEndpointCommand, TaskResult>
{
    /// <inheritdoc/>
    public Task<TaskResult> Handle(GenEndpointCommand request, CancellationToken cancellationToken)
    {
        string message = $"Planned endpoint generation for {request.Method.ToUpperInvariant()} {request.Route}.";
        TaskResult result = TaskResult.Success(message, Array.Empty<string>());
        return Task.FromResult(result);
    }
}
