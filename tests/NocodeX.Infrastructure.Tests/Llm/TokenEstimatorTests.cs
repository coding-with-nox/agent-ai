using NocodeX.Core.Models.Llm;
using NocodeX.Infrastructure.Llm;
using FluentAssertions;
using Xunit;

namespace NocodeX.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for the heuristic token estimator.
/// </summary>
public sealed class TokenEstimatorTests
{
    private readonly TokenEstimator _sut = new();

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        _sut.EstimateTokens("").Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_NullString_ReturnsZero()
    {
        _sut.EstimateTokens(null).Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_ShortString_ReturnsAtLeastOne()
    {
        _sut.EstimateTokens("Hi").Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void EstimateTokens_LongString_ScalesWithLength()
    {
        string text = new('a', 1000);
        int estimate = _sut.EstimateTokens(text);
        estimate.Should().BeInRange(200, 300);
    }

    [Fact]
    public void EstimateTokensForMessages_EmptyList_ReturnsZero()
    {
        _sut.EstimateTokensForMessages(Array.Empty<LlmMessage>()).Should().Be(0);
    }

    [Fact]
    public void EstimateTokensForMessages_SingleMessage_IncludesOverhead()
    {
        List<LlmMessage> messages = new()
        {
            new LlmMessage("user", "Hello")
        };

        int estimate = _sut.EstimateTokensForMessages(messages);
        estimate.Should().BeGreaterThan(1);
    }

    [Fact]
    public void EstimateTokensForMessages_MultipleMessages_IncludesReplyPriming()
    {
        List<LlmMessage> messages = new()
        {
            new LlmMessage("system", "You are a helper."),
            new LlmMessage("user", "Hello, world!")
        };

        int estimate = _sut.EstimateTokensForMessages(messages);
        // Should include overhead for each message + 3 for reply priming
        estimate.Should().BeGreaterThan(10);
    }
}
