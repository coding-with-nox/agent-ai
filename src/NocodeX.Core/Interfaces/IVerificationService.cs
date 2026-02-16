using NocodeX.Core.Models;

namespace NocodeX.Core.Interfaces;

/// <summary>
/// Runs lint, build, and test verification against generated code.
/// </summary>
public interface IVerificationService
{
    /// <summary>
    /// Runs the full verification pipeline (lint, build, test).
    /// </summary>
    /// <param name="stack">Active stack configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered verification results.</returns>
    Task<IReadOnlyList<VerificationResult>> VerifyAsync(
        StackConfig stack, CancellationToken ct);

    /// <summary>
    /// Runs a single verification step by name (lint, build, or test).
    /// </summary>
    /// <param name="stepName">The step to run.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    Task<VerificationResult> RunStepAsync(
        string stepName, string command, CancellationToken ct);
}
