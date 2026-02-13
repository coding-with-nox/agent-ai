using Codex.Core.Interfaces;
using MediatR;

namespace Codex.Application.Stack;

/// <summary>
/// Handles stack activation requests.
/// </summary>
public sealed class SetStackHandler : IRequestHandler<SetStackCommand>
{
    private readonly IStackRegistry _stackRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetStackHandler"/> class.
    /// </summary>
    /// <param name="stackRegistry">Stack registry instance.</param>
    public SetStackHandler(IStackRegistry stackRegistry)
    {
        _stackRegistry = stackRegistry;
    }

    /// <inheritdoc/>
    public Task Handle(SetStackCommand request, CancellationToken cancellationToken)
    {
        _stackRegistry.Set(request.Preset);
        return Task.CompletedTask;
    }
}
