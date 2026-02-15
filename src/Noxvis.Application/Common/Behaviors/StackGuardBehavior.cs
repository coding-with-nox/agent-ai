using Noxvis.Core.Exceptions;
using Noxvis.Core.Interfaces;
using MediatR;

namespace Noxvis.Application.Common.Behaviors;

/// <summary>
/// Prevents stack-dependent requests from running when no stack is active.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class StackGuardBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IStackRegistry _stackRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="StackGuardBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="stackRegistry">Stack registry instance.</param>
    public StackGuardBehavior(IStackRegistry stackRegistry)
    {
        _stackRegistry = stackRegistry;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IRequireStack && _stackRegistry.Current is null)
        {
            throw new NoStackConfiguredException();
        }

        return await next();
    }
}
