using Codex.Core.Models.Llm;
using MediatR;

namespace Codex.Application.Llm;

/// <summary>
/// Runs a performance benchmark on an LLM provider.
/// </summary>
/// <param name="ProviderId">Optional provider ID; defaults to primary.</param>
public sealed record LlmBenchmarkCommand(string? ProviderId = null) : IRequest<LlmBenchmarkResult>;

/// <summary>
/// Result of an LLM benchmark run.
/// </summary>
public sealed record LlmBenchmarkResult
{
    /// <summary>Gets the provider identifier that was benchmarked.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Gets the model identifier used.</summary>
    public required string ModelId { get; init; }

    /// <summary>Gets the inference response.</summary>
    public required LlmResponse Response { get; init; }

    /// <summary>Gets the tokens per second achieved.</summary>
    public double TokensPerSecond { get; init; }
}
