using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Services;

/// <summary>
/// Health check for the upload directory of the direct backend, which holds the only copy of uploaded files.
/// Registered only where that backend is active, because <see cref="UploadDirectOptions"/> is never bound otherwise.
/// </summary>
public class UploadDirectoryHealthCheck : IHealthCheck
{
    private readonly IOptions<UploadDirectOptions> options;
    private readonly ILogger<UploadDirectoryHealthCheck> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadDirectoryHealthCheck"/> class.
    /// </summary>
    public UploadDirectoryHealthCheck(IOptions<UploadDirectOptions> options, ILogger<UploadDirectoryHealthCheck> logger)
    {
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(StorageHealthCheck.Check([options.Value.ToDataDirectory()], logger));
}
