using NocodeX.Core.Exceptions;
using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using NocodeX.Infrastructure.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace NocodeX.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for the LLM client manager.
/// </summary>
public sealed class LlmClientManagerTests
{
    private readonly ILogger<LlmClientManager> _logger = NullLogger<LlmClientManager>.Instance;

    [Fact]
    public void Primary_NoProvider_ThrowsLlmUnavailable()
    {
        LlmClientManager sut = new(_logger, Enumerable.Empty<ILlmProvider>(), Array.Empty<string>());
        FluentActions.Invoking(() => sut.Primary)
            .Should().Throw<LlmUnavailableException>();
    }

    [Fact]
    public void Register_SetsPrimary()
    {
        LlmClientManager sut = new(_logger, Enumerable.Empty<ILlmProvider>(), Array.Empty<string>());
        ILlmProvider provider = CreateMockProvider("test-provider");

        sut.Register(provider, isPrimary: true);

        sut.Primary.Should().BeSameAs(provider);
    }

    [Fact]
    public void GetProvider_ExistingId_ReturnsProvider()
    {
        ILlmProvider provider = CreateMockProvider("my-provider");
        LlmClientManager sut = new(_logger, new[] { provider }, Array.Empty<string>());

        sut.GetProvider("my-provider").Should().BeSameAs(provider);
    }

    [Fact]
    public void GetProvider_UnknownId_ThrowsKeyNotFound()
    {
        LlmClientManager sut = new(_logger, Enumerable.Empty<ILlmProvider>(), Array.Empty<string>());

        FluentActions.Invoking(() => sut.GetProvider("unknown"))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetProviderIds_ReturnsAllRegistered()
    {
        ILlmProvider p1 = CreateMockProvider("a");
        ILlmProvider p2 = CreateMockProvider("b");

        LlmClientManager sut = new(_logger, new[] { p1, p2 }, Array.Empty<string>());

        sut.GetProviderIds().Should().Contain("a").And.Contain("b");
    }

    [Fact]
    public async Task CheckAllHealthAsync_ReturnsStatusForEach()
    {
        ILlmProvider provider = CreateMockProvider("test");
        provider.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new LlmHealthStatus(true, true, "model-x", null, null, 42.0));

        LlmClientManager sut = new(_logger, new[] { provider }, Array.Empty<string>());

        IReadOnlyDictionary<string, LlmHealthStatus> result =
            await sut.CheckAllHealthAsync(CancellationToken.None);

        result.Should().ContainKey("test");
        result["test"].IsReachable.Should().BeTrue();
        result["test"].ActiveModel.Should().Be("model-x");
    }

    [Fact]
    public async Task CompleteWithFallbackAsync_PrimarySucceeds_ReturnsPrimaryResult()
    {
        ILlmProvider primary = CreateMockProvider("primary");
        LlmResponse expected = new()
        {
            Content = "Hello",
            Usage = new LlmUsage(10, 5, 15, 50.0),
            InferenceTime = TimeSpan.FromMilliseconds(100)
        };
        primary.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        LlmClientManager sut = new(_logger, new[] { primary }, new[] { "primary" });
        sut.Register(primary, isPrimary: true);

        LlmRequest request = new()
        {
            Model = "test",
            Messages = new[] { new LlmMessage("user", "hi") }
        };

        LlmResponse result = await sut.CompleteWithFallbackAsync(request, CancellationToken.None);
        result.Content.Should().Be("Hello");
    }

    [Fact]
    public async Task CompleteWithFallbackAsync_PrimaryFails_FallsBackToNext()
    {
        ILlmProvider primary = CreateMockProvider("primary");
        primary.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        ILlmProvider fallback = CreateMockProvider("fallback");
        LlmResponse fallbackResponse = new()
        {
            Content = "From fallback",
            Usage = new LlmUsage(10, 5, 15, 30.0),
            InferenceTime = TimeSpan.FromMilliseconds(200)
        };
        fallback.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(fallbackResponse);

        LlmClientManager sut = new(_logger,
            new[] { primary, fallback },
            new[] { "primary", "fallback" });
        sut.Register(primary, isPrimary: true);

        LlmRequest request = new()
        {
            Model = "test",
            Messages = new[] { new LlmMessage("user", "hi") }
        };

        LlmResponse result = await sut.CompleteWithFallbackAsync(request, CancellationToken.None);
        result.Content.Should().Be("From fallback");
    }

    [Fact]
    public async Task CompleteWithFallbackAsync_AllFail_ThrowsLlmUnavailable()
    {
        ILlmProvider provider = CreateMockProvider("only");
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("down"));

        LlmClientManager sut = new(_logger, new[] { provider }, new[] { "only" });
        sut.Register(provider, isPrimary: true);

        LlmRequest request = new()
        {
            Model = "test",
            Messages = new[] { new LlmMessage("user", "hi") }
        };

        Func<Task> act = () => sut.CompleteWithFallbackAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<LlmUnavailableException>();
    }

    private static ILlmProvider CreateMockProvider(string id)
    {
        ILlmProvider provider = Substitute.For<ILlmProvider>();
        provider.ProviderId.Returns(id);
        return provider;
    }
}
