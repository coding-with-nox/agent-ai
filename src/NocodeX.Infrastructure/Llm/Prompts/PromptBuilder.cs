using NocodeX.Core.Interfaces;
using NocodeX.Core.Models;
using NocodeX.Core.Models.Llm;
using NocodeX.Core.Models.Prompts;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Llm.Prompts;

/// <summary>
/// Builds LLM prompts adapted to the active model and stack.
/// </summary>
public sealed class PromptBuilder : IPromptBuilder
{
    private readonly PromptTemplateStore _templateStore;
    private readonly ILogger<PromptBuilder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptBuilder"/> class.
    /// </summary>
    public PromptBuilder(
        PromptTemplateStore templateStore,
        ILogger<PromptBuilder> logger)
    {
        _templateStore = templateStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public LlmRequest BuildPrompt(
        PromptContext context,
        LlmModelInfo modelInfo,
        StackConfig stack)
    {
        string systemPrompt = BuildSystemPrompt(stack, modelInfo);
        string userPrompt = BuildUserPrompt(context, stack);

        List<LlmMessage> messages = new()
        {
            new LlmMessage("system", systemPrompt),
            new LlmMessage("user", userPrompt)
        };

        bool isSmallModel = modelInfo.ParameterCount < 14_000_000_000;
        float temperature = isSmallModel ? 0.1f : 0.2f;

        _logger.LogDebug(
            "Built prompt for {Type} with {MsgCount} messages, model {Model} ({Params}B)",
            context.Type, messages.Count, modelInfo.ModelId,
            modelInfo.ParameterCount / 1_000_000_000);

        return new LlmRequest
        {
            Model = modelInfo.ModelId,
            Messages = messages,
            Temperature = temperature,
            MaxTokens = Math.Min(8192, modelInfo.ContextWindowTokens / 2),
            TopP = 0.95f,
            StopSequences = new[] { "</code>", "---END---" }
        };
    }

    private string BuildSystemPrompt(StackConfig stack, LlmModelInfo modelInfo)
    {
        string? template = _templateStore.GetTemplate(stack.Name, "system-prompt");
        if (template is not null)
        {
            return InterpolateTemplate(template, stack, null);
        }

        return $"""
            You are NOcodeX, an expert code generation agent.
            Stack: {stack.Language} / {stack.Framework}
            Architecture: {string.Join(", ", stack.Conventions)}
            Rules: {string.Join(", ", stack.CustomRules)}

            Output code inside XML tags: <code filepath="relative/path">...code...</code>
            Generate production-ready code with no placeholders.
            Max 300 lines per file. Include XML doc comments on public members.
            """;
    }

    private string BuildUserPrompt(PromptContext context, StackConfig stack)
    {
        string? template = _templateStore.GetTemplate(stack.Name, MapPromptTypeToTemplate(context.Type));
        string basePrompt;

        if (template is not null)
        {
            basePrompt = InterpolateTemplate(template, stack, context);
        }
        else
        {
            basePrompt = $"Task: {context.TaskDescription}";
        }

        if (context.ExistingFiles is { Count: > 0 })
        {
            basePrompt += "\n\nExisting files for context:\n";
            foreach (KeyValuePair<string, string> file in context.ExistingFiles)
            {
                basePrompt += $"\n--- {file.Key} ---\n{file.Value}\n";
            }
        }

        if (context.ErrorContext is not null)
        {
            basePrompt += $"\n\nPrevious error:\n{context.ErrorContext}";
        }

        if (context.PreviousAttempt is not null)
        {
            basePrompt += $"\n\nPrevious attempt (fix the issues):\n{context.PreviousAttempt}";
        }

        return basePrompt;
    }

    private static string InterpolateTemplate(
        string template,
        StackConfig stack,
        PromptContext? context)
    {
        string result = template
            .Replace("{{stack.language}}", stack.Language)
            .Replace("{{stack.framework}}", stack.Framework)
            .Replace("{{stack.name}}", stack.Name)
            .Replace("{{stack.conventions}}", string.Join(", ", stack.Conventions))
            .Replace("{{stack.custom_rules}}", string.Join(", ", stack.CustomRules));

        if (context is not null)
        {
            result = result
                .Replace("{{task.description}}", context.TaskDescription)
                .Replace("{{task.type}}", context.Type.ToString());
        }

        return result;
    }

    private static string MapPromptTypeToTemplate(PromptType type)
    {
        return type switch
        {
            PromptType.GenerateEndpoint => "endpoint",
            PromptType.GenerateModel => "model",
            PromptType.GenerateService => "service",
            PromptType.GenerateTest => "test",
            PromptType.GenerateComponent => "component",
            PromptType.GenerateMigration => "migration",
            PromptType.FixCompilationError => "fix-error",
            PromptType.Refactor => "refactor",
            PromptType.Explain => "explain",
            PromptType.Review => "review",
            _ => "generic"
        };
    }
}
