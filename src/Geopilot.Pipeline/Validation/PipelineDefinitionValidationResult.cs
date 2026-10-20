namespace Geopilot.Pipeline;

/// <summary>
/// Outcome of validating a pipeline definition. Carries one ready to display message listing every problem
/// found, so a caller can surface all of them at once without knowing how validation is structured.
/// </summary>
/// <param name="ErrorMessage">All problems found, or <see langword="null"/> when the definition is valid.</param>
public sealed record PipelineDefinitionValidationResult(string? ErrorMessage)
{
    /// <summary>
    /// Gets a value indicating whether the definition passed validation.
    /// </summary>
    public bool IsValid => ErrorMessage is null;

    /// <summary>
    /// Gets the result for a definition without problems.
    /// </summary>
    public static PipelineDefinitionValidationResult Valid { get; } = new(ErrorMessage: null);
}
