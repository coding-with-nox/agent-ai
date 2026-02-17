using NocodeX.Core.Interfaces;
using NocodeX.Core.Models;
using NocodeX.Core.Models.Llm;
using NocodeX.Core.Models.Prompts;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.CodeGeneration;

/// <summary>
/// Orchestrates the prompt-to-code pipeline using LLM inference.
/// </summary>
public sealed class LlmCodeGenerator
{
    private readonly ILlmClientManager _clientManager;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IContextWindowManager _contextManager;
    private readonly CodeBlockParser _codeParser;
    private readonly FileOutputMapper _fileMapper;
    private readonly ILogger<LlmCodeGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmCodeGenerator"/> class.
    /// </summary>
    public LlmCodeGenerator(
        ILlmClientManager clientManager,
        IPromptBuilder promptBuilder,
        IContextWindowManager contextManager,
        CodeBlockParser codeParser,
        FileOutputMapper fileMapper,
        ILogger<LlmCodeGenerator> logger)
    {
        _clientManager = clientManager;
        _promptBuilder = promptBuilder;
        _contextManager = contextManager;
        _codeParser = codeParser;
        _fileMapper = fileMapper;
        _logger = logger;
    }

    /// <summary>
    /// Generates code from a prompt context using the active LLM provider.
    /// </summary>
    /// <param name="context">The prompt context describing what to generate.</param>
    /// <param name="stack">The active stack configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated code files keyed by relative path.</returns>
    public async Task<GenerationResult> GenerateAsync(
        PromptContext context,
        StackConfig stack,
        CancellationToken ct)
    {
        ILlmProvider provider = _clientManager.Primary;
        LlmModelInfo modelInfo = await provider.GetModelInfoAsync(ct);

        _logger.LogInformation("Generating code for {Type}: {Task}",
            context.Type, context.TaskDescription);

        LlmRequest request = _promptBuilder.BuildPrompt(context, modelInfo, stack);

        IReadOnlyList<LlmMessage> fitted = await _contextManager.FitToContextWindowAsync(
            request.Messages, modelInfo, request.MaxTokens, ct);

        LlmRequest fittedRequest = request with { Messages = fitted };

        LlmResponse response = await _clientManager.CompleteWithFallbackAsync(fittedRequest, ct);

        _logger.LogInformation(
            "LLM responded in {Time}ms ({Tokens} tokens, {Tps} tok/s)",
            response.InferenceTime.TotalMilliseconds,
            response.Usage.TotalTokens,
            response.Usage.TokensPerSecond?.ToString("F1") ?? "N/A");

        IReadOnlyList<CodeBlock> blocks = _codeParser.Parse(response.Content);

        if (blocks.Count == 0)
        {
            _logger.LogWarning("No code blocks found in LLM response");
            return new GenerationResult
            {
                Files = new Dictionary<string, string>(),
                RawResponse = response.Content,
                Usage = response.Usage,
                InferenceTime = response.InferenceTime
            };
        }

        Dictionary<string, string> files = [];
        foreach (CodeBlock block in blocks)
        {
            string filePath = _fileMapper.ResolveFilePath(block.FilePath, stack);
            files[filePath] = block.Content;
        }

        _logger.LogInformation("Generated {Count} file(s)", files.Count);

        return new GenerationResult
        {
            Files = files,
            RawResponse = response.Content,
            Usage = response.Usage,
            InferenceTime = response.InferenceTime
        };
    }

    /// <summary>
    /// Streams code generation tokens for live display.
    /// </summary>
    /// <param name="context">The prompt context.</param>
    /// <param name="stack">The active stack configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of token chunks.</returns>
    public async IAsyncEnumerable<LlmTokenChunk> StreamGenerateAsync(
        PromptContext context,
        StackConfig stack,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ILlmProvider provider = _clientManager.Primary;
        LlmModelInfo modelInfo = await provider.GetModelInfoAsync(ct);

        LlmRequest request = _promptBuilder.BuildPrompt(context, modelInfo, stack);

        IReadOnlyList<LlmMessage> fitted = await _contextManager.FitToContextWindowAsync(
            request.Messages, modelInfo, request.MaxTokens, ct);

        LlmRequest fittedRequest = request with { Messages = fitted };

        await foreach (LlmTokenChunk chunk in provider.StreamAsync(fittedRequest, ct))
        {
            yield return chunk;
        }
    }
}

/// <summary>
/// Result of a code generation operation.
/// </summary>
public sealed class GenerationResult
{
    /// <summary>Gets the generated files keyed by relative path.</summary>
    public required IReadOnlyDictionary<string, string> Files { get; init; }

    /// <summary>Gets the raw LLM response text.</summary>
    public required string RawResponse { get; init; }

    /// <summary>Gets the token usage statistics.</summary>
    public required LlmUsage Usage { get; init; }

    /// <summary>Gets the inference time.</summary>
    public required TimeSpan InferenceTime { get; init; }
}
