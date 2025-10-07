using Geopilot.Api.FileAccess;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Geopilot.Api
{
    /// <summary>
    /// Health check for the upload and assets storage.
    /// </summary>
    public class StorageHealthCheck : IHealthCheck
    {
        private readonly IDirectoryProvider directoryProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageHealthCheck"/> class.
        /// </summary>
        /// <param name="directoryProvider">The <see cref="IDirectoryProvider"/>.</param>
        public StorageHealthCheck(IDirectoryProvider directoryProvider) => this.directoryProvider = directoryProvider;

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var healthCheckResult = HealthCheckResult.Healthy();

            try
            {
                // Simply check if the upload and asset directories exist.
                if (!Directory.Exists(directoryProvider.UploadDirectory) || !Directory.Exists(directoryProvider.AssetDirectory))
                {
                    healthCheckResult = HealthCheckResult.Unhealthy();
                }
            }
            catch (Exception)
            {
                healthCheckResult = HealthCheckResult.Unhealthy();
            }

            return await Task.FromResult(healthCheckResult).ConfigureAwait(false);
        }
    }
}
