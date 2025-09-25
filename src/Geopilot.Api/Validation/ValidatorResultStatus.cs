namespace Geopilot.Api.Validation;

/// <summary>
/// Represents the status of an <see cref="ValidatorResult"/>.
/// </summary>
public enum ValidatorResultStatus
{
    /// <summary>
    /// The validator completed without errors.
    /// </summary>
    Completed,

    /// <summary>
    /// The validator completed but with validation errors.
    /// </summary>
    CompletedWithErrors,

    /// <summary>
    /// The validator failed to complete.
    /// </summary>
    Failed,
}
