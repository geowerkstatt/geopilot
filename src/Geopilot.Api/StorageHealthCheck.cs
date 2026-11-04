using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Geopilot.Api;

/// <summary>
/// Health check for the local data directories listed by <see cref="DataDirectories.Local"/>. Only checks
/// that they exist; whether they are writable is proven once at startup, not on every probe.
/// </summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly IOptions<FileAccessOptions> fileAccessOptions;
    private readonly ILogger<StorageHealthCheck> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHealthCheck"/> class.
    /// </summary>
    public StorageHealthCheck(IOptions<FileAccessOptions> fileAccessOptions, ILogger<StorageHealthCheck> logger)
    {
        this.fileAccessOptions = fileAccessOptions;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(Check(DataDirectories.Local(fileAccessOptions.Value), logger));

    /// <summary>
    /// Reports the directories that do not exist. The description names their configuration keys only, because
    /// the health endpoints answer anonymously; the paths go to the log.
    /// </summary>
    internal static HealthCheckResult Check(IReadOnlyList<DataDirectory> directories, ILogger logger)
    {
        var missing = directories.Where(directory => !Directory.Exists(directory.Path)).ToList();
        if (missing.Count == 0)
            return HealthCheckResult.Healthy();

        logger.LogWarning(
            "Storage directories missing: {MissingDirectories}",
            string.Join(", ", missing.Select(directory => $"{directory.Key} <{Path.GetFullPath(directory.Path)}>")));

        return HealthCheckResult.Unhealthy($"Missing: {string.Join(", ", missing.Select(directory => directory.Key))}");
    }
}
