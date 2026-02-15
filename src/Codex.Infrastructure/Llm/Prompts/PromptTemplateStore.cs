using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Llm.Prompts;

/// <summary>
/// Reads and caches prompt template files from disk.
/// </summary>
public sealed class PromptTemplateStore
{
    private readonly string _templateBasePath;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<PromptTemplateStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptTemplateStore"/> class.
    /// </summary>
    /// <param name="templateBasePath">Root directory for template files.</param>
    /// <param name="logger">Logger instance.</param>
    public PromptTemplateStore(string templateBasePath, ILogger<PromptTemplateStore> logger)
    {
        _templateBasePath = templateBasePath;
        _logger = logger;
    }

    /// <summary>
    /// Gets a template by stack and template name.
    /// </summary>
    /// <param name="stackName">Stack preset name (e.g. dotnet-clean).</param>
    /// <param name="templateName">Template name (e.g. endpoint, system-prompt).</param>
    /// <returns>Template content or null if not found.</returns>
    public string? GetTemplate(string stackName, string templateName)
    {
        string cacheKey = $"{stackName}/{templateName}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        // Try stack-specific template first
        string stackPath = Path.Combine(_templateBasePath, stackName, $"{templateName}.md");
        if (File.Exists(stackPath))
        {
            string content = File.ReadAllText(stackPath);
            _cache[cacheKey] = content;
            _logger.LogDebug("Loaded template {Stack}/{Template} from {Path}", stackName, templateName, stackPath);
            return content;
        }

        // Fall back to shared template
        string sharedPath = Path.Combine(_templateBasePath, "shared", $"{templateName}.md");
        if (File.Exists(sharedPath))
        {
            string content = File.ReadAllText(sharedPath);
            _cache[cacheKey] = content;
            _logger.LogDebug("Loaded shared template {Template} from {Path}", templateName, sharedPath);
            return content;
        }

        _logger.LogDebug("No template found for {Stack}/{Template}", stackName, templateName);
        return null;
    }

    /// <summary>
    /// Lists all available template names for a given stack.
    /// </summary>
    /// <param name="stackName">Stack preset name.</param>
    /// <returns>Available template names.</returns>
    public IReadOnlyList<string> ListTemplates(string stackName)
    {
        List<string> templates = new();
        string stackDir = Path.Combine(_templateBasePath, stackName);

        if (Directory.Exists(stackDir))
        {
            templates.AddRange(
                Directory.GetFiles(stackDir, "*.md")
                    .Select(f => Path.GetFileNameWithoutExtension(f)));
        }

        string sharedDir = Path.Combine(_templateBasePath, "shared");
        if (Directory.Exists(sharedDir))
        {
            templates.AddRange(
                Directory.GetFiles(sharedDir, "*.md")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => !templates.Contains(n, StringComparer.OrdinalIgnoreCase)));
        }

        return templates;
    }

    /// <summary>
    /// Clears the template cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
}
