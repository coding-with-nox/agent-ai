namespace NocodeX.Core.Exceptions;

/// <summary>
/// Thrown when a prompt exceeds the model's context window after truncation.
/// </summary>
public sealed class ContextWindowExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContextWindowExceededException"/> class.
    /// </summary>
    /// <param name="requiredTokens">Tokens required by the prompt.</param>
    /// <param name="availableTokens">Tokens available in the context window.</param>
    public ContextWindowExceededException(int requiredTokens, int availableTokens)
        : base($"Prompt requires {requiredTokens} tokens but only {availableTokens} are available. " +
               "Try a model with a larger context window or reduce the input.")
    {
        RequiredTokens = requiredTokens;
        AvailableTokens = availableTokens;
    }

    /// <summary>Gets the number of tokens required.</summary>
    public int RequiredTokens { get; }

    /// <summary>Gets the number of tokens available.</summary>
    public int AvailableTokens { get; }
}
