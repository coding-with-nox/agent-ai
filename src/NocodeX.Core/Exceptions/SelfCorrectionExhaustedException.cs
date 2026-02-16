namespace NocodeX.Core.Exceptions;

/// <summary>
/// Thrown when the self-correction loop exceeds the maximum retry attempts.
/// </summary>
public sealed class SelfCorrectionExhaustedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelfCorrectionExhaustedException"/> class.
    /// </summary>
    /// <param name="attempts">Number of attempts made.</param>
    /// <param name="lastError">The error from the final attempt.</param>
    public SelfCorrectionExhaustedException(int attempts, string lastError)
        : base($"Self-correction exhausted after {attempts} attempts. Last error: {lastError}")
    {
        Attempts = attempts;
        LastError = lastError;
    }

    /// <summary>Gets the number of correction attempts made.</summary>
    public int Attempts { get; }

    /// <summary>Gets the error from the final attempt.</summary>
    public string LastError { get; }
}
