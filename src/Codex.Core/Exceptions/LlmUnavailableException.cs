namespace Codex.Core.Exceptions;

/// <summary>
/// Thrown when no LLM provider is healthy or reachable.
/// </summary>
public sealed class LlmUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LlmUnavailableException"/> class.
    /// </summary>
    public LlmUnavailableException()
        : base("No LLM provider is available. Check '/llm status' for details.")
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom message.
    /// </summary>
    /// <param name="message">Error message.</param>
    public LlmUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and inner exception.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public LlmUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
