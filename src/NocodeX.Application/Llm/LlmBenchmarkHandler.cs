using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using MediatR;

namespace NocodeX.Application.Llm;

/// <summary>
/// Handles LLM benchmark commands by running a test completion.
/// </summary>
public sealed class LlmBenchmarkHandler : IRequestHandler<LlmBenchmarkCommand, LlmBenchmarkResult>
{
    private readonly ILlmClientManager _clientManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmBenchmarkHandler"/> class.
    /// </summary>
    public LlmBenchmarkHandler(ILlmClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    /// <inheritdoc/>
    public async Task<LlmBenchmarkResult> Handle(
        LlmBenchmarkCommand request, CancellationToken cancellationToken)
    {
        ILlmProvider provider = request.ProviderId is not null
            ? _clientManager.GetProvider(request.ProviderId)
            : _clientManager.Primary;

        LlmModelInfo modelInfo = await provider.GetModelInfoAsync(cancellationToken);

        LlmRequest benchRequest = new()
        {
            Model = modelInfo.ModelId,
            Messages = new[]
            {
                new LlmMessage("system", "You are a coding assistant."),
                new LlmMessage("user",
                    "Write a hello world function in C# with XML doc comments. Include a unit test.")
            },
            Temperature = 0.2f,
            MaxTokens = 512,
            TopP = 0.95f
        };

        LlmResponse response = await provider.CompleteAsync(benchRequest, cancellationToken);

        double tps = response.Usage.TokensPerSecond ??
            (response.InferenceTime.TotalSeconds > 0
                ? response.Usage.CompletionTokens / response.InferenceTime.TotalSeconds
                : 0);

        return new LlmBenchmarkResult
        {
            ProviderId = provider.ProviderId,
            ModelId = modelInfo.ModelId,
            Response = response,
            TokensPerSecond = tps
        };
    }
}
