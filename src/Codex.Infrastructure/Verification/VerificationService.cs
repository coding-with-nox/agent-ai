using System.Diagnostics;
using Codex.Core.Interfaces;
using Codex.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Verification;

/// <summary>
/// Runs lint, build, and test verification commands against generated code.
/// </summary>
public sealed class VerificationService : IVerificationService
{
    private readonly ILogger<VerificationService> _logger;
    private readonly TimeSpan _stepTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerificationService"/> class.
    /// </summary>
    public VerificationService(ILogger<VerificationService> logger, TimeSpan? stepTimeout = null)
    {
        _logger = logger;
        _stepTimeout = stepTimeout ?? TimeSpan.FromMinutes(5);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VerificationResult>> VerifyAsync(
        StackConfig stack, CancellationToken ct)
    {
        List<VerificationResult> results = new();
        string[] steps = { "lint", "build", "test" };

        foreach (string step in steps)
        {
            if (!stack.Commands.TryGetValue(step, out string? command))
            {
                _logger.LogDebug("Stack {Stack} has no '{Step}' command, skipping", stack.Name, step);
                continue;
            }

            VerificationResult result = await RunStepAsync(step, command, ct);
            results.Add(result);

            if (!result.Passed)
            {
                _logger.LogWarning("Verification step '{Step}' failed: {Error}",
                    step, result.StandardError ?? "no details");
                break;
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<VerificationResult> RunStepAsync(
        string stepName, string command, CancellationToken ct)
    {
        _logger.LogInformation("Running verification step '{Step}': {Cmd}", stepName, command);
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd" : "sh",
                    Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);

            using CancellationTokenSource timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_stepTimeout);

            await process.WaitForExitAsync(timeoutCts.Token);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            sw.Stop();

            bool passed = process.ExitCode == 0;

            _logger.LogInformation(
                "Step '{Step}' {Status} in {Time}ms (exit {Code})",
                stepName, passed ? "passed" : "failed", sw.ElapsedMilliseconds, process.ExitCode);

            return new VerificationResult
            {
                Passed = passed,
                StepName = stepName,
                StandardOutput = stdout,
                StandardError = stderr,
                ExitCode = process.ExitCode,
                Elapsed = sw.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning("Step '{Step}' timed out after {Time}ms", stepName, sw.ElapsedMilliseconds);
            return new VerificationResult
            {
                Passed = false,
                StepName = stepName,
                StandardError = $"Step timed out after {_stepTimeout.TotalSeconds}s",
                ExitCode = -1,
                Elapsed = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Step '{Step}' threw an exception", stepName);
            return new VerificationResult
            {
                Passed = false,
                StepName = stepName,
                StandardError = ex.Message,
                ExitCode = -1,
                Elapsed = sw.Elapsed
            };
        }
    }
}
