namespace Geopilot.Api.Services;

/// <summary>
/// Carries the context needed by <see cref="PreflightBackgroundService"/> to process an upload job.
/// </summary>
public record PreflightRequest(Guid JobId, Guid UploadId);
