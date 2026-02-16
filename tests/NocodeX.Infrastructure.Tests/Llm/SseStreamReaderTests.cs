using System.Net;
using System.Text;
using NocodeX.Core.Models.Llm;
using NocodeX.Infrastructure.Llm;
using FluentAssertions;
using Xunit;

namespace NocodeX.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for the SSE stream reader.
/// </summary>
public sealed class SseStreamReaderTests
{
    private readonly SseStreamReader _sut = new();

    [Fact]
    public async Task ReadAsync_OpenAiFormat_YieldsTokens()
    {
        string sseData = """
            data: {"choices":[{"delta":{"content":"Hello"},"finish_reason":null}]}

            data: {"choices":[{"delta":{"content":" world"},"finish_reason":null}]}

            data: {"choices":[{"delta":{"content":"!"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":3,"total_tokens":8}}

            data: [DONE]

            """;

        HttpResponseMessage response = CreateResponse(sseData);
        List<LlmTokenChunk> chunks = new();

        await foreach (LlmTokenChunk chunk in _sut.ReadAsync(response))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(3);
        chunks[0].Token.Should().Be("Hello");
        chunks[0].IsComplete.Should().BeFalse();
        chunks[1].Token.Should().Be(" world");
        chunks[2].Token.Should().Be("!");
        chunks[2].IsComplete.Should().BeTrue();
        chunks[2].FinalUsage.Should().NotBeNull();
        chunks[2].FinalUsage!.TotalTokens.Should().Be(8);
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_YieldsNothing()
    {
        HttpResponseMessage response = CreateResponse("");
        List<LlmTokenChunk> chunks = new();

        await foreach (LlmTokenChunk chunk in _sut.ReadAsync(response))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_DoneSentinelOnly_YieldsNothing()
    {
        HttpResponseMessage response = CreateResponse("data: [DONE]\n\n");
        List<LlmTokenChunk> chunks = new();

        await foreach (LlmTokenChunk chunk in _sut.ReadAsync(response))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_SkipsCommentLines()
    {
        string sseData = """
            :comment line
            data: {"choices":[{"delta":{"content":"Hi"},"finish_reason":"stop"}]}

            data: [DONE]

            """;

        HttpResponseMessage response = CreateResponse(sseData);
        List<LlmTokenChunk> chunks = new();

        await foreach (LlmTokenChunk chunk in _sut.ReadAsync(response))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(1);
        chunks[0].Token.Should().Be("Hi");
    }

    [Fact]
    public async Task ReadAsync_OllamaFormat_YieldsTokens()
    {
        string sseData = """
            data: {"content":"func","done":false}

            data: {"content":"()","done":true,"tokens_per_second":45.2}

            """;

        HttpResponseMessage response = CreateResponse(sseData);
        List<LlmTokenChunk> chunks = new();

        await foreach (LlmTokenChunk chunk in _sut.ReadAsync(response))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(2);
        chunks[0].Token.Should().Be("func");
        chunks[1].Token.Should().Be("()");
        chunks[1].IsComplete.Should().BeTrue();
    }

    private static HttpResponseMessage CreateResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream")
        };
    }
}
