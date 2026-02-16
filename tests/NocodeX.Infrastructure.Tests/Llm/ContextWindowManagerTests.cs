using NocodeX.Core.Models.Llm;
using NocodeX.Infrastructure.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace NocodeX.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for the context window manager.
/// </summary>
public sealed class ContextWindowManagerTests
{
    private readonly ContextWindowManager _sut;

    public ContextWindowManagerTests()
    {
        _sut = new ContextWindowManager(
            new TokenEstimator(),
            NullLogger<ContextWindowManager>.Instance);
    }

    [Fact]
    public void EstimateTokens_DelegatesToTokenEstimator()
    {
        _sut.EstimateTokens("Hello world").Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetAvailableOutputTokens_SubtractsPromptAndMargin()
    {
        LlmRequest request = new()
        {
            Model = "test",
            Messages = new[] { new LlmMessage("user", "Hello") }
        };

        LlmModelInfo modelInfo = new("test", 4096, "fp16", 7_000_000_000, 4000, true);
        int available = _sut.GetAvailableOutputTokens(request, modelInfo);

        available.Should().BePositive();
        available.Should().BeLessThan(4096);
    }

    [Fact]
    public async Task FitToContextWindowAsync_SmallWindow_DropsOldMessages()
    {
        List<LlmMessage> messages = new()
        {
            new LlmMessage("system", "You are a coding assistant."),
            new LlmMessage("user", new string('a', 2000)),  // ~500 tokens
            new LlmMessage("assistant", new string('b', 2000)),  // ~500 tokens
            new LlmMessage("user", "Short question")  // ~4 tokens
        };

        LlmModelInfo modelInfo = new("test", 600, "Q4", 7_000_000_000, 4000, true);

        IReadOnlyList<LlmMessage> result = await _sut.FitToContextWindowAsync(
            messages, modelInfo, 100, CancellationToken.None);

        // Should keep system + at least the most recent message
        result.Count.Should().BeLessThan(messages.Count);
        result[0].Role.Should().Be("system");
        result[^1].Content.Should().Be("Short question");
    }

    [Fact]
    public async Task FitToContextWindowAsync_LargeWindow_KeepsAllMessages()
    {
        List<LlmMessage> messages = new()
        {
            new LlmMessage("system", "You are a helper."),
            new LlmMessage("user", "Hello"),
            new LlmMessage("assistant", "Hi there!")
        };

        LlmModelInfo modelInfo = new("test", 128_000, "fp16", 70_000_000_000, 48000, true);

        IReadOnlyList<LlmMessage> result = await _sut.FitToContextWindowAsync(
            messages, modelInfo, 4096, CancellationToken.None);

        result.Count.Should().Be(messages.Count);
    }

    [Fact]
    public async Task FitToContextWindowAsync_ZeroBudget_ReturnsEmpty()
    {
        List<LlmMessage> messages = new()
        {
            new LlmMessage("user", "Hello")
        };

        LlmModelInfo modelInfo = new("test", 100, "Q4", 7_000_000_000, 4000, true);

        IReadOnlyList<LlmMessage> result = await _sut.FitToContextWindowAsync(
            messages, modelInfo, 200, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
