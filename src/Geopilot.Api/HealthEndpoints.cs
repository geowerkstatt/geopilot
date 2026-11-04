using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Geopilot.Api;

/// <summary>
/// The health endpoints: <c>/health/live</c> for liveness and startup probes, <c>/health/ready</c> for readiness, and
/// <c>/health</c> with every check, kept as it was for the Docker <c>HEALTHCHECK</c> and existing monitoring.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// The tag of the checks <c>/health/ready</c> runs. Only dependencies without which the instance must not take
    /// traffic belong there; the identity provider, the ilitools-wrapper and ClamAV do not, their failure should
    /// fail only the feature that uses them.
    /// </summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Maps the three health endpoints, all anonymous.
    /// </summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Runs no check: it answers as soon as Kestrel listens, which is after migrations, plugin loading and
        // the pipeline validation. Never put the database here, a short outage would restart the pod and
        // lose every running job.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteJsonAsync,
        }).AllowAnonymous();

        app.MapHealthChecks("/health")
            .AllowAnonymous();
    }

    /// <summary>
    /// Writes the status of each check with its description. The endpoint is anonymous, so descriptions name
    /// configuration keys at most. When a check threw, the framework uses the exception message as description,
    /// which can carry a host or a path, so that description is left out; the health check service logs it.
    /// </summary>
    internal static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Exception is null ? entry.Value.Description : null,
                }),
        };

        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
