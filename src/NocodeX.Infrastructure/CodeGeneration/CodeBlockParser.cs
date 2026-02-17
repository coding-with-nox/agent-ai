using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.CodeGeneration;

/// <summary>
/// Extracts code blocks from LLM output.
/// </summary>
public sealed partial class CodeBlockParser
{
    private readonly ILogger<CodeBlockParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeBlockParser"/> class.
    /// </summary>
    public CodeBlockParser(ILogger<CodeBlockParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses LLM output to extract code blocks with file paths.
    /// </summary>
    /// <param name="llmOutput">The raw LLM response text.</param>
    /// <returns>Extracted code blocks.</returns>
    public IReadOnlyList<CodeBlock> Parse(string llmOutput)
    {
        if (string.IsNullOrWhiteSpace(llmOutput))
        {
            return Array.Empty<CodeBlock>();
        }

        // Try XML-style first: <code filepath="...">...</code>
        List<CodeBlock> blocks = ParseXmlBlocks(llmOutput);
        if (blocks.Count > 0)
        {
            _logger.LogDebug("Parsed {Count} XML code blocks", blocks.Count);
            return blocks;
        }

        // Fallback: markdown fenced blocks with filepath comments
        blocks = ParseMarkdownBlocks(llmOutput);
        if (blocks.Count > 0)
        {
            _logger.LogDebug("Parsed {Count} markdown code blocks", blocks.Count);
            return blocks;
        }

        _logger.LogWarning("No code blocks found in LLM output ({Length} chars)", llmOutput.Length);
        return Array.Empty<CodeBlock>();
    }

    private static List<CodeBlock> ParseXmlBlocks(string text)
    {
        List<CodeBlock> blocks = [];
        Regex regex = XmlCodeBlockRegex();
        MatchCollection matches = regex.Matches(text);

        foreach (Match match in matches)
        {
            string filepath = match.Groups[1].Value.Trim();
            string content = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(filepath) && !string.IsNullOrEmpty(content))
            {
                blocks.Add(new CodeBlock(filepath, content));
            }
        }

        return blocks;
    }

    private static List<CodeBlock> ParseMarkdownBlocks(string text)
    {
        List<CodeBlock> blocks = [];
        Regex regex = MarkdownCodeBlockRegex();
        MatchCollection matches = regex.Matches(text);

        foreach (Match match in matches)
        {
            string filepath = match.Groups[1].Value.Trim();
            string content = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(filepath) && !string.IsNullOrEmpty(content))
            {
                blocks.Add(new CodeBlock(filepath, content));
            }
        }

        return blocks;
    }

    [GeneratedRegex(
        @"<code\s+filepath=""([^""]+)""\s*>(.*?)</code>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex XmlCodeBlockRegex();

    [GeneratedRegex(
        @"```\w*\s*//\s*filepath:\s*(.+?)\n(.*?)```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownCodeBlockRegex();
}

/// <summary>
/// A parsed code block with its target file path.
/// </summary>
/// <param name="FilePath">Relative file path for the generated code.</param>
/// <param name="Content">The generated source code content.</param>
public sealed record CodeBlock(string FilePath, string Content);
