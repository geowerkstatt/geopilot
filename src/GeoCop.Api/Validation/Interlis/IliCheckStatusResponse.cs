using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api.Validation.Interlis;

/// <summary>
/// Result of a status query of interlis-check-service at /api/v1/status/{jobId}.
/// </summary>
public class IliCheckStatusResponse
{
    /// <summary>
    /// The job identification.
    /// </summary>
    [Required]
    public Guid JobId { get; set; }

    /// <summary>
    /// The job status.
    /// </summary>
    [Required]
    public Status Status { get; set; }

    /// <summary>
    /// The job status message.
    /// </summary>
    [Required]
    public string? StatusMessage { get; set; }

    /// <summary>
    /// The log url if available; otherwise, <c>null</c>. Please be aware that the log file might not be complete while validation is still processing.
    /// </summary>
    public string? LogUrl { get; set; }

    /// <summary>
    /// The XTF log url if available; otherwise, <c>null</c>. Please be aware that the log file might not be complete while validation is still processing.
    /// </summary>
    public string? XtfLogUrl { get; set; }
}
