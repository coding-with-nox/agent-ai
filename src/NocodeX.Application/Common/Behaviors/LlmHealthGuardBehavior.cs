using NocodeX.Core.Exceptions;
using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using MediatR;

namespace NocodeX.Application.Common.Behaviors;

/// <summary>
/// Marker interface for requests that require a healthy LLM provider.
/// </summary>
public interface IRequireLlm
{
}

/// <summary>
/// Pipeline behavior that checks LLM provider health before generation commands.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class LlmHealthGuardBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILlmClientManager _clientManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmHealthGuardBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    public LlmHealthGuardBehavior(ILlmClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IRequireLlm)
        {
            IReadOnlyDictionary<string, LlmHealthStatus> statuses =
                await _clientManager.CheckAllHealthAsync(cancellationToken);

            bool anyHealthy = statuses.Values.Any(s => s.IsReachable && s.IsModelLoaded);
            if (!anyHealthy)
            {
                throw new LlmUnavailableException(
                    "No LLM provider is healthy. Cannot proceed with generation. " +
                    "Run '/llm status' to diagnose.");
            }
        }

        return await next();
    }
}
