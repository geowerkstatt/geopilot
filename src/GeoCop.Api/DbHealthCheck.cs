using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GeoCop.Api;

/// <summary>
/// Represents a health check for the database.
/// </summary>
public class DbHealthCheck : IHealthCheck
{
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbHealthCheck"/> class.
    /// </summary>
    /// <param name="context">The given <see cref="Context"/>.</param>
    public DbHealthCheck(Context context)
        => this.context = context;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await this.context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("The geopilot database is unreachable.", ex);
        }
    }
}
