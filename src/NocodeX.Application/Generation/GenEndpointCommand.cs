using NocodeX.Application.Common.Behaviors;
using NocodeX.Core.Models;
using MediatR;

namespace NocodeX.Application.Generation;

/// <summary>
/// Generates an endpoint artifact.
/// </summary>
/// <param name="Method">HTTP method.</param>
/// <param name="Route">Route path.</param>
public sealed record GenEndpointCommand(string Method, string Route) : IRequest<TaskResult>, IRequireStack;
