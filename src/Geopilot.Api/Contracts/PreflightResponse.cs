namespace Geopilot.Api.Contracts;

/// <summary>
/// Result of preflight checks on uploaded cloud files.
/// </summary>
public record PreflightResponse(bool Success, PreflightFailureReason? FailureReason = null, string? ErrorMessage = null);

/// <summary>
/// Reasons why preflight checks can fail.
/// </summary>
public enum PreflightFailureReason
{
    /// <summary>
    /// One or more expected files are missing or incomplete.
    /// </summary>
    IncompleteUpload,

    /// <summary>
    /// A security threat was detected in the uploaded files.
    /// </summary>
    ThreatDetected,
}
