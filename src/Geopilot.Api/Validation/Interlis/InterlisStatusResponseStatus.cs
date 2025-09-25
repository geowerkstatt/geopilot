namespace Geopilot.Api.Validation.Interlis;

/// <summary>
/// Represents the status of a validation returned by the interlis-check-service.
/// </summary>
public enum InterlisStatusResponseStatus
{
    /// <summary>
    /// The validation is processing.
    /// </summary>
    Processing,

    /// <summary>
    /// The validation completed successfully without errors.
    /// </summary>
    Completed,

    /// <summary>
    /// The validation completed but with errors.
    /// </summary>
    CompletedWithErrors,

    /// <summary>
    /// The validation failed.
    /// </summary>
    Failed,
}
