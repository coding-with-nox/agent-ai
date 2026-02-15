using Codex.Core.Models;
using MediatR;

namespace Codex.Application.Llm;

/// <summary>
/// Pulls/downloads a model on a specified provider.
/// </summary>
/// <param name="ModelId">The model identifier to pull.</param>
/// <param name="ProviderId">Optional target provider; defaults to primary.</param>
public sealed record PullModelCommand(string ModelId, string? ProviderId = null)
    : IRequest<TaskResult>;
