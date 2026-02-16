using System.Runtime.CompilerServices;
using System.Text.Json;
using NocodeX.Core.Models.Llm;

namespace NocodeX.Infrastructure.Llm;

/// <summary>
/// Reads a Server-Sent Events (SSE) stream from an HTTP response and yields
/// <see cref="LlmTokenChunk"/> instances as they arrive. Compatible with
/// OpenAI-style streaming endpoints that emit <c>data: {json}\n\n</c> frames
/// and terminate with <c>data: [DONE]</c>.
/// </summary>
public sealed class SseStreamReader
{
    /// <summary>
    /// The SSE data-line prefix that precedes every payload frame.
    /// </summary>
    private const string DataPrefix = "data: ";

    /// <summary>
    /// Sentinel value that signals the end of the stream.
    /// </summary>
    private const string DoneSentinel = "[DONE]";

    /// <summary>
    /// Sentinel value including the data prefix, as emitted by some providers.
    /// </summary>
    private const string DoneSentinelWithPrefix = "data: [DONE]";

    /// <summary>
    /// Reusable JSON deserialization options configured for camelCase property names.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reads SSE frames from the given <see cref="HttpResponseMessage"/> and yields
    /// <see cref="LlmTokenChunk"/> instances as they are parsed from the stream.
    /// </summary>
    /// <param name="response">
    /// The HTTP response whose content stream contains SSE-formatted data.
    /// The caller is responsible for disposing the response after enumeration completes.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous enumeration.
    /// </param>
    /// <returns>
    /// An asynchronous sequence of <see cref="LlmTokenChunk"/> parsed from the SSE stream.
    /// The final chunk will have <see cref="LlmTokenChunk.IsComplete"/> set to <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="response"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="JsonException">
    /// Thrown when a data frame contains JSON that cannot be deserialized into the expected format.
    /// </exception>
    public async IAsyncEnumerable<LlmTokenChunk> ReadAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await reader.ReadLineAsync(cancellationToken)
                .ConfigureAwait(false);

            // SSE frames are separated by blank lines; skip them.
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            // Skip SSE comment lines (lines starting with ':').
            if (line.StartsWith(':'))
            {
                continue;
            }

            // Check for the DONE sentinel in both forms.
            if (IsDoneSentinel(line))
            {
                yield break;
            }

            // Only process data frames.
            if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string json = line[DataPrefix.Length..];

            // The payload itself might be the done sentinel without the prefix.
            if (string.Equals(json, DoneSentinel, StringComparison.Ordinal))
            {
                yield break;
            }

            LlmTokenChunk? chunk = ParseChunk(json);

            if (chunk is not null)
            {
                yield return chunk;

                if (chunk.IsComplete)
                {
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Determines whether a raw SSE line represents the end-of-stream sentinel.
    /// </summary>
    /// <param name="line">The raw line read from the stream.</param>
    /// <returns><c>true</c> if the line signals stream completion; otherwise <c>false</c>.</returns>
    private static bool IsDoneSentinel(string line)
    {
        return string.Equals(line, DoneSentinelWithPrefix, StringComparison.Ordinal)
            || string.Equals(line, DoneSentinel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses a JSON payload from an SSE data frame into an <see cref="LlmTokenChunk"/>.
    /// </summary>
    /// <remarks>
    /// Supports the OpenAI chat-completion streaming format where token content resides at
    /// <c>choices[0].delta.content</c> and usage at the top-level <c>usage</c> object.
    /// Returns <c>null</c> when no meaningful content can be extracted from the frame
    /// (for example, a frame that only carries a role delta with no content).
    /// </remarks>
    /// <param name="json">The raw JSON string from the SSE data frame.</param>
    /// <returns>
    /// A parsed <see cref="LlmTokenChunk"/>, or <c>null</c> if the frame contains no
    /// actionable content.
    /// </returns>
    private static LlmTokenChunk? ParseChunk(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string token = ExtractToken(root);
        string? finishReason = ExtractFinishReason(root);
        bool isComplete = finishReason is not null;
        LlmUsage? usage = isComplete ? ExtractUsage(root) : null;

        // When there is no token text and the stream has not finished, skip the frame.
        if (string.IsNullOrEmpty(token) && !isComplete)
        {
            return null;
        }

        return new LlmTokenChunk(token, isComplete, usage);
    }

    /// <summary>
    /// Extracts the generated token text from a streaming chunk.
    /// </summary>
    /// <param name="root">The root JSON element of the SSE data frame.</param>
    /// <returns>The token string, or <see cref="string.Empty"/> if not present.</returns>
    private static string ExtractToken(JsonElement root)
    {
        // OpenAI format: choices[0].delta.content
        if (root.TryGetProperty("choices", out JsonElement choices)
            && choices.GetArrayLength() > 0)
        {
            JsonElement firstChoice = choices[0];

            if (firstChoice.TryGetProperty("delta", out JsonElement delta)
                && delta.TryGetProperty("content", out JsonElement content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString() ?? string.Empty;
            }
        }

        // Fallback: top-level "token" or "content" property (used by some local runtimes).
        if (root.TryGetProperty("token", out JsonElement tokenProp))
        {
            // HuggingFace TGI format: { "token": { "text": "..." } }
            if (tokenProp.ValueKind == JsonValueKind.Object
                && tokenProp.TryGetProperty("text", out JsonElement textProp)
                && textProp.ValueKind == JsonValueKind.String)
            {
                return textProp.GetString() ?? string.Empty;
            }

            if (tokenProp.ValueKind == JsonValueKind.String)
            {
                return tokenProp.GetString() ?? string.Empty;
            }
        }

        if (root.TryGetProperty("content", out JsonElement contentFallback)
            && contentFallback.ValueKind == JsonValueKind.String)
        {
            return contentFallback.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Extracts the finish reason from a streaming chunk, if present.
    /// </summary>
    /// <param name="root">The root JSON element of the SSE data frame.</param>
    /// <returns>
    /// The finish reason string (e.g. <c>"stop"</c>, <c>"length"</c>), or <c>null</c>
    /// if the stream has not yet completed.
    /// </returns>
    private static string? ExtractFinishReason(JsonElement root)
    {
        // OpenAI format: choices[0].finish_reason
        if (root.TryGetProperty("choices", out JsonElement choices)
            && choices.GetArrayLength() > 0)
        {
            JsonElement firstChoice = choices[0];

            if (firstChoice.TryGetProperty("finish_reason", out JsonElement reason)
                && reason.ValueKind == JsonValueKind.String)
            {
                return reason.GetString();
            }
        }

        // Fallback: top-level "done" boolean (used by Ollama and similar).
        if (root.TryGetProperty("done", out JsonElement done)
            && done.ValueKind == JsonValueKind.True)
        {
            return "stop";
        }

        return null;
    }

    /// <summary>
    /// Extracts token usage statistics from the final streaming chunk, if available.
    /// </summary>
    /// <param name="root">The root JSON element of the SSE data frame.</param>
    /// <returns>
    /// An <see cref="LlmUsage"/> instance populated from the response, or <c>null</c>
    /// if usage information is not present in this frame.
    /// </returns>
    private static LlmUsage? ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage))
        {
            return null;
        }

        int promptTokens = GetInt32OrDefault(usage, "prompt_tokens");
        int completionTokens = GetInt32OrDefault(usage, "completion_tokens");
        int totalTokens = GetInt32OrDefault(usage, "total_tokens");

        // total_tokens may be absent; compute it if necessary.
        if (totalTokens == 0 && (promptTokens > 0 || completionTokens > 0))
        {
            totalTokens = promptTokens + completionTokens;
        }

        double? tokensPerSecond = null;
        if (root.TryGetProperty("tokens_per_second", out JsonElement tpsElement)
            && tpsElement.TryGetDouble(out double tps))
        {
            tokensPerSecond = tps;
        }

        return new LlmUsage(promptTokens, completionTokens, totalTokens, tokensPerSecond);
    }

    /// <summary>
    /// Safely reads an integer property from a JSON element, returning zero if absent or invalid.
    /// </summary>
    /// <param name="element">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <returns>The integer value, or <c>0</c> if the property does not exist or is not numeric.</returns>
    private static int GetInt32OrDefault(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement prop)
            && prop.TryGetInt32(out int value))
        {
            return value;
        }

        return 0;
    }
}
