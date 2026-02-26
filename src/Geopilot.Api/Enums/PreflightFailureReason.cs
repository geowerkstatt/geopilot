namespace Geopilot.Api.Enums;

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
