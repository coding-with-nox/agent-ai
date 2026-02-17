using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using NocodeX.Core.Models.Prompts;
using NocodeX.Infrastructure.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace NocodeX.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for the LLM request router.
/// </summary>
public sealed class LlmRequestRouterTests
{
    [Fact]
    public void ResolveProvider_MatchingComplexityRule_ReturnsRuleProvider()
    {
        ILlmClientManager manager = CreateManager("local", "gpu-server");
        List<LlmRoutingRule> rules =
        [
            new LlmRoutingRule("task_complexity >= 'high'", "gpu-server", "Complex tasks go to GPU")
        ];

        LlmRequestRouter sut = new(manager, rules, NullLogger<LlmRequestRouter>.Instance);

        string result = sut.ResolveProvider(PromptType.GenerateEndpoint, "high");
        result.Should().Be("gpu-server");
    }

    [Fact]
    public void ResolveProvider_MatchingPromptTypeRule_ReturnsRuleProvider()
    {
        ILlmClientManager manager = CreateManager("local", "gpu-server");
        List<LlmRoutingRule> rules =
        [
            new LlmRoutingRule("prompt_type == 'Explain'", "local", "Explain stays local")
        ];

        LlmRequestRouter sut = new(manager, rules, NullLogger<LlmRequestRouter>.Instance);

        string result = sut.ResolveProvider(PromptType.Explain, "low");
        result.Should().Be("local");
    }

    [Fact]
    public void ResolveProvider_NoMatchingRule_ReturnsPrimary()
    {
        ILlmClientManager manager = CreateManager("primary", "other");
        List<LlmRoutingRule> rules =
        [
            new LlmRoutingRule("task_complexity >= 'critical'", "other", "Only critical")
        ];

        LlmRequestRouter sut = new(manager, rules, NullLogger<LlmRequestRouter>.Instance);

        string result = sut.ResolveProvider(PromptType.GenerateModel, "low");
        result.Should().Be("primary");
    }

    [Fact]
    public void ResolveProvider_RuleProviderNotRegistered_SkipsRule()
    {
        ILlmClientManager manager = CreateManager("local");
        List<LlmRoutingRule> rules =
        [
            new LlmRoutingRule("task_complexity >= 'high'", "missing-server", "Not registered")
        ];

        LlmRequestRouter sut = new(manager, rules, NullLogger<LlmRequestRouter>.Instance);

        string result = sut.ResolveProvider(PromptType.GenerateEndpoint, "high");
        result.Should().Be("local");
    }

    [Fact]
    public void GetRoutedProvider_ReturnsCorrectProviderInstance()
    {
        ILlmClientManager manager = CreateManager("local", "gpu");
        List<LlmRoutingRule> rules =
        [
            new LlmRoutingRule("prompt_type == 'Review'", "local", "Reviews stay local")
        ];

        LlmRequestRouter sut = new(manager, rules, NullLogger<LlmRequestRouter>.Instance);

        ILlmProvider result = sut.GetRoutedProvider(PromptType.Review, "low");
        result.ProviderId.Should().Be("local");
    }

    private static ILlmClientManager CreateManager(params string[] providerIds)
    {
        ILlmClientManager manager = Substitute.For<ILlmClientManager>();
        manager.GetProviderIds().Returns(providerIds.ToList().AsReadOnly());

        foreach (string id in providerIds)
        {
            ILlmProvider provider = Substitute.For<ILlmProvider>();
            provider.ProviderId.Returns(id);
            manager.GetProvider(id).Returns(provider);
        }

        // First provider is primary
        if (providerIds.Length > 0)
        {
            ILlmProvider primary = Substitute.For<ILlmProvider>();
            primary.ProviderId.Returns(providerIds[0]);
            manager.Primary.Returns(primary);
        }

        return manager;
    }
}
