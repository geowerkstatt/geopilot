using Geopilot.Api.Validation;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Geopilot.Api
{
    /// <summary>
    /// Health check for the validation service.
    /// </summary>
    public class ValidationServiceHealthCheck : IHealthCheck
    {
        private readonly IValidationService validationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationServiceHealthCheck"/> class.
        /// </summary>
        /// <param name="validationService">The <see cref="IValidationService"/>.</param>
        public ValidationServiceHealthCheck(IValidationService validationService) => this.validationService = validationService;

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var healthCheckResult = HealthCheckResult.Healthy();

            try
            {
                // Check if the validation service is available by simply retrieving the supported file types of all available validators.
                var fileExtensions = await validationService.GetSupportedFileExtensionsAsync().ConfigureAwait(false);

                // There must be at least one supported file extension, because there
                // is at mininum one validator for INTERLIS configured by default.
                if (fileExtensions is null || fileExtensions.Count == 0)
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
