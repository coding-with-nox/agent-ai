using NocodeX.Core.Interfaces;
using NocodeX.Core.Models;
using NocodeX.Core.Models.Llm;
using NocodeX.Core.Models.Prompts;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Acp;

/// <summary>
/// ACP agent client that uses a local LLM for inference-based agent tasks.
/// </summary>
public sealed class AcpAgentClient : IAcpClient
{
    private readonly AcpAgentConfig _config;
    private readonly ILlmClientManager _llmClientManager;
    private readonly ILogger<AcpAgentClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcpAgentClient"/> class.
    /// </summary>
    public AcpAgentClient(
        AcpAgentConfig config,
        ILlmClientManager llmClientManager,
        ILogger<AcpAgentClient> logger)
    {
        _config = config;
        _llmClientManager = llmClientManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string AgentId => _config.Id;

    /// <inheritdoc/>
    public async Task<AcpTaskResult> DispatchAsync(
        AcpTaskRequest task,
        CancellationToken ct)
    {
        _logger.LogInformation("ACP [{Agent}] Dispatching task: {Desc}",
            AgentId, task.Description);

        try
        {
            ILlmProvider provider = task.LlmProviderId is not null
                ? _llmClientManager.GetProvider(task.LlmProviderId)
                : _llmClientManager.Primary;

            List<LlmMessage> messages = new()
            {
                new LlmMessage("system", BuildAgentSystemPrompt()),
                new LlmMessage("user", BuildTaskPrompt(task))
            };

            LlmRequest request = new()
            {
                Model = "",
                Messages = messages,
                Temperature = 0.1f,
                MaxTokens = 4096,
                TopP = 0.95f
            };

            LlmResponse response = await provider.CompleteAsync(request, ct);

            _logger.LogInformation("ACP [{Agent}] Task completed in {Time}ms",
                AgentId, response.InferenceTime.TotalMilliseconds);

            return new AcpTaskResult
            {
                Success = true,
                Content = response.Content,
                SuggestedChanges = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACP [{Agent}] Task failed", AgentId);
            return new AcpTaskResult
            {
                Success = false,
                Content = $"Agent task failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public Task<bool> CheckHealthAsync(CancellationToken ct)
    {
        if (!_config.Enabled) return Task.FromResult(false);

        try
        {
            _ = _llmClientManager.Primary;
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private string BuildAgentSystemPrompt()
    {
        return AgentId switch
        {
            "acp-reviewer" => "You are a code review agent. Analyze code for bugs, security issues, and best practice violations. Be concise and actionable.",
            "acp-tester" => "You are a test generation agent. Given source code, generate comprehensive unit tests. Use the appropriate test framework for the language.",
            "acp-security" => "You are a security analysis agent. Scan code for OWASP Top 10 vulnerabilities and security anti-patterns.",
            _ => $"You are an AI agent ({AgentId}). Complete the assigned task accurately."
        };
    }

    private static string BuildTaskPrompt(AcpTaskRequest task)
    {
        string prompt = task.Description;

        if (task.ContextFiles is { Count: > 0 })
        {
            prompt += "\n\nContext files:\n";
            foreach (KeyValuePair<string, string> file in task.ContextFiles)
            {
                prompt += $"\n--- {file.Key} ---\n{file.Value}\n";
            }
        }

        return prompt;
    }
}
