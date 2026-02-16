using NocodeX.Infrastructure.CodeGeneration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace NocodeX.Infrastructure.Tests.Llm;

/// <summary>
/// Tests for the code block parser.
/// </summary>
public sealed class CodeBlockParserTests
{
    private readonly CodeBlockParser _sut = new(NullLogger<CodeBlockParser>.Instance);

    [Fact]
    public void Parse_XmlBlocks_ExtractsCorrectly()
    {
        string input = """
            Here is your code:
            <code filepath="src/Models/Order.cs">
            public sealed record Order(int Id, string Name);
            </code>
            """;

        IReadOnlyList<CodeBlock> blocks = _sut.Parse(input);
        blocks.Should().HaveCount(1);
        blocks[0].FilePath.Should().Be("src/Models/Order.cs");
        blocks[0].Content.Should().Contain("public sealed record Order");
    }

    [Fact]
    public void Parse_MultipleXmlBlocks_ExtractsAll()
    {
        string input = """
            <code filepath="src/IService.cs">
            public interface IService { }
            </code>
            <code filepath="src/Service.cs">
            public class Service : IService { }
            </code>
            """;

        IReadOnlyList<CodeBlock> blocks = _sut.Parse(input);
        blocks.Should().HaveCount(2);
        blocks[0].FilePath.Should().Be("src/IService.cs");
        blocks[1].FilePath.Should().Be("src/Service.cs");
    }

    [Fact]
    public void Parse_MarkdownBlocks_FallsBackCorrectly()
    {
        string input = """
            Here is the code:
            ```csharp // filepath: src/Hello.cs
            public class Hello { }
            ```
            """;

        IReadOnlyList<CodeBlock> blocks = _sut.Parse(input);
        blocks.Should().HaveCount(1);
        blocks[0].FilePath.Should().Be("src/Hello.cs");
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        _sut.Parse("").Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullInput_ReturnsEmpty()
    {
        _sut.Parse(null!).Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoCodeBlocks_ReturnsEmpty()
    {
        _sut.Parse("Just some plain text without any code blocks.").Should().BeEmpty();
    }
}
