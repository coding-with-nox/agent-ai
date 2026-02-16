namespace Noxvis.Core.Enums;

/// <summary>
/// Categorizes failures returned by MCP/ACP integrations.
/// </summary>
public enum ErrorCategory
{
    Transient,
    Validation,
    Logic,
    Dependency,
    Permanent
}
