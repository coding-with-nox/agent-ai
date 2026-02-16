using System.CommandLine;
using NocodeX.Application.Llm;
using NocodeX.Cli.Rendering;
using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using MediatR;

namespace NocodeX.Cli.Commands;

/// <summary>
/// Builds /llm command tree for LLM provider management.
/// </summary>
public static class LlmCommands
{
    /// <summary>
    /// Creates the /llm command hierarchy.
    /// </summary>
    /// <param name="mediator">Mediator for command dispatch.</param>
    /// <param name="clientManager">LLM client manager.</param>
    /// <returns>Configured command.</returns>
    public static Command Build(IMediator mediator, ILlmClientManager clientManager)
    {
        Command llm = new("llm", "Manage LLM inference providers.");

        llm.AddCommand(BuildStatusCommand(mediator));
        llm.AddCommand(BuildProvidersCommand(clientManager));
        llm.AddCommand(BuildHealthCommand(mediator));
        llm.AddCommand(BuildBenchmarkCommand(mediator));
        llm.AddCommand(BuildPullCommand(mediator));
        llm.AddCommand(BuildModelsCommand(clientManager));
        llm.AddCommand(BuildSetPrimaryCommand(clientManager));

        return llm;
    }

    private static Command BuildStatusCommand(IMediator mediator)
    {
        Command cmd = new("status", "Show all providers: health, model, GPU usage, tokens/sec.");
        cmd.SetHandler(async () =>
        {
            IReadOnlyDictionary<string, LlmHealthStatus> statuses =
                await mediator.Send(new LlmStatusQuery());
            LlmStatusRenderer.Render(statuses);
        });
        return cmd;
    }

    private static Command BuildProvidersCommand(ILlmClientManager clientManager)
    {
        Command cmd = new("providers", "List registered providers.");
        cmd.SetHandler(() =>
        {
            foreach (string id in clientManager.GetProviderIds())
            {
                Console.WriteLine(id);
            }
        });
        return cmd;
    }

    private static Command BuildHealthCommand(IMediator mediator)
    {
        Command cmd = new("health", "Run health check on all providers.");
        cmd.SetHandler(async () =>
        {
            IReadOnlyDictionary<string, LlmHealthStatus> statuses =
                await mediator.Send(new LlmStatusQuery());

            foreach (KeyValuePair<string, LlmHealthStatus> kvp in statuses)
            {
                string icon = kvp.Value.IsReachable && kvp.Value.IsModelLoaded ? "OK" : "FAIL";
                Console.WriteLine($"[{icon}] {kvp.Key}: {kvp.Value.ActiveModel ?? "no model"}");
            }
        });
        return cmd;
    }

    private static Command BuildBenchmarkCommand(IMediator mediator)
    {
        Command cmd = new("benchmark", "Run a performance benchmark.");
        Argument<string?> providerArg = new("provider", () => null, "Provider ID (optional).");
        cmd.AddArgument(providerArg);

        cmd.SetHandler(async (string? providerId) =>
        {
            LlmBenchmarkResult result = await mediator.Send(new LlmBenchmarkCommand(providerId));
            Console.WriteLine($"Provider: {result.ProviderId}");
            Console.WriteLine($"Model:    {result.ModelId}");
            Console.WriteLine($"Tok/s:    {result.TokensPerSecond:F1}");
            Console.WriteLine($"Time:     {result.Response.InferenceTime.TotalMilliseconds:F0}ms");
            Console.WriteLine($"Tokens:   {result.Response.Usage.TotalTokens}");
        }, providerArg);

        return cmd;
    }

    private static Command BuildPullCommand(IMediator mediator)
    {
        Command cmd = new("pull", "Pull/download a model.");
        Argument<string> modelArg = new("model", "Model identifier to pull.");
        Argument<string?> providerArg = new("provider", () => null, "Target provider ID (optional).");
        cmd.AddArgument(modelArg);
        cmd.AddArgument(providerArg);

        cmd.SetHandler(async (string model, string? provider) =>
        {
            var result = await mediator.Send(new PullModelCommand(model, provider));
            Console.WriteLine(result.Message);
        }, modelArg, providerArg);

        return cmd;
    }

    private static Command BuildModelsCommand(ILlmClientManager clientManager)
    {
        Command cmd = new("models", "List available models on a provider.");
        Argument<string?> providerArg = new("provider", () => null, "Provider ID (optional).");
        cmd.AddArgument(providerArg);

        cmd.SetHandler(async (string? providerId) =>
        {
            ILlmProvider provider = providerId is not null
                ? clientManager.GetProvider(providerId)
                : clientManager.Primary;

            LlmModelInfo info = await provider.GetModelInfoAsync(CancellationToken.None);
            Console.WriteLine($"Model:      {info.ModelId}");
            Console.WriteLine($"Context:    {info.ContextWindowTokens} tokens");
            Console.WriteLine($"Quant:      {info.Quantization}");
            Console.WriteLine($"Parameters: {info.ParameterCount / 1_000_000_000.0:F1}B");
            Console.WriteLine($"VRAM:       {info.VramUsageMb} MB");
            Console.WriteLine($"Loaded:     {info.IsLoaded}");
        }, providerArg);

        return cmd;
    }

    private static Command BuildSetPrimaryCommand(ILlmClientManager clientManager)
    {
        Command cmd = new("set-primary", "Switch primary provider.");
        Argument<string> providerArg = new("provider_id", "Provider ID to set as primary.");
        cmd.AddArgument(providerArg);

        cmd.SetHandler((string providerId) =>
        {
            ILlmProvider provider = clientManager.GetProvider(providerId);
            clientManager.Register(provider, isPrimary: true);
            Console.WriteLine($"Primary provider set to '{providerId}'.");
        }, providerArg);

        return cmd;
    }
}
