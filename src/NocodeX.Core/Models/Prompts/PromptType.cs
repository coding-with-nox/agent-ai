namespace NocodeX.Core.Models.Prompts;

/// <summary>
/// Identifies the kind of code generation or analysis prompt.
/// </summary>
public enum PromptType
{
    /// <summary>Generate an API endpoint.</summary>
    GenerateEndpoint,

    /// <summary>Generate a domain model or entity.</summary>
    GenerateModel,

    /// <summary>Generate a service with interface.</summary>
    GenerateService,

    /// <summary>Generate unit or integration tests.</summary>
    GenerateTest,

    /// <summary>Generate a UI component.</summary>
    GenerateComponent,

    /// <summary>Generate a database migration.</summary>
    GenerateMigration,

    /// <summary>Refactor existing code.</summary>
    Refactor,

    /// <summary>Explain a code segment.</summary>
    Explain,

    /// <summary>Review code for issues.</summary>
    Review,

    /// <summary>Fix a compilation or runtime error.</summary>
    FixCompilationError
}
