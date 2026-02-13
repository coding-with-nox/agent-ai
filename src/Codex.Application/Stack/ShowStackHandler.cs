using Codex.Core.Interfaces;
using Codex.Core.Models;
using MediatR;

namespace Codex.Application.Stack;

/// <summary>
/// Handles active stack read requests.
/// </summary>
public sealed class ShowStackHandler : IRequestHandler<ShowStackQuery, StackConfig?>
{
    private readonly IStackRegistry _stackRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowStackHandler"/> class.
    /// </summary>
    /// <param name="stackRegistry">Stack registry instance.</param>
    public ShowStackHandler(IStackRegistry stackRegistry)
    {
        _stackRegistry = stackRegistry;
    }

    /// <inheritdoc/>
    public Task<StackConfig?> Handle(ShowStackQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_stackRegistry.Current);
    }
}
