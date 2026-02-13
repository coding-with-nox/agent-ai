namespace Codex.Core.Exceptions;

/// <summary>
/// Raised when no LLM provider can serve a request.
/// </summary>
public sealed class LlmUnavailableException : Exception
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    /// <param name="message">Error message.</param>
    public LlmUnavailableException(string message)
        : base(message)
    {
    }
}
