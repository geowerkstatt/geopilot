namespace Geopilot.Api.Validation.Interlis;

/// <summary>
/// Result of a successful upload to interlis-check-service at /api/v1/upload.
/// </summary>
public class InterlisUploadResponse
{
    /// <summary>
    /// Id of the validation job.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Url to retrieve the status of the validation job.
    /// </summary>
    public string? StatusUrl { get; set; }
}
