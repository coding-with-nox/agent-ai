using NocodeX.Core.Enums;
using NocodeX.Core.Models.Llm;
using NocodeX.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace NocodeX.Infrastructure.Tests.Llm;

/// <summary>
/// Tests around LLM provider endpoint configuration and URL composition.
/// </summary>
public sealed class LlmProviderConfigurationTests
{
    [Fact]
    public void BaseUrl_RemoteHostAndBasePath_ComposesValidHttpUrl()
    {
        LlmProviderConfig config = new()
        {
            ProviderId = "vm-vllm",
            Type = LlmProviderType.Vllm,
            Host = "192.168.1.100",
            Port = 8000,
            BasePath = "v1"
        };

        config.BaseUrl.Should().Be("http://192.168.1.100:8000/v1");
    }

    [Fact]
    public void BaseUrl_BaseUrlOverride_TakesPrecedence()
    {
        LlmProviderConfig config = new()
        {
            ProviderId = "vm-vllm",
            Type = LlmProviderType.Vllm,
            Host = "localhost",
            Port = 11434,
            BasePath = "/ignored",
            BaseUrlOverride = "https://vm-llm.local:8443/v1/"
        };

        config.BaseUrl.Should().Be("https://vm-llm.local:8443/v1");
    }

    [Fact]
    public void ToProviderConfig_BaseUrl_IsMappedToOverride()
    {
        LlmProviderJsonConfig json = new()
        {
            ProviderId = "remote-openai",
            Type = "openai-compatible",
            Host = "ignored",
            Port = 1234,
            BasePath = "/v1",
            BaseUrl = "https://llm-vm.lan:9443/v1"
        };

        LlmProviderConfig result = json.ToProviderConfig();

        result.Type.Should().Be(LlmProviderType.OpenAiCompatible);
        result.BaseUrl.Should().Be("https://llm-vm.lan:9443/v1");
    }
}
