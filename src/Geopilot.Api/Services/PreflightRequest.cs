namespace Geopilot.Api.Services;

/// <summary>
/// Carries the context needed by <see cref="PreflightBackgroundService"/> to process a cloud upload job.
/// </summary>
public record PreflightRequest(Guid JobId, int MandateId, string? UserAuthId);
