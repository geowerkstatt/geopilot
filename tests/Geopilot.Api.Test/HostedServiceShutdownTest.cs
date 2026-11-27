using Geopilot.Api.Authorization;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Test;

[TestClass]
public sealed class HostedServiceShutdownTest
{
    [TestMethod]
    public void PreflightStopsBeforeTheRunner()
    {
        using var app = new JwtTestApp();
        var hostedServices = app.Services.GetServices<IHostedService>().Select(s => s.GetType()).ToList();

        // Hosted services stop in reverse registration order, so the runner has to be registered first.
        Assert.IsLessThan(
            hostedServices.IndexOf(typeof(PreflightBackgroundService)),
            hostedServices.IndexOf(typeof(ProcessingRunner)),
            "The preflight must stop before the runner, or it can still queue work nobody reads any more.");
    }

    [TestMethod]
    public void HeartbeatStopsAfterTheRunner()
    {
        using var app = new JwtTestApp();
        var hostedServices = app.Services.GetServices<IHostedService>().Select(s => s.GetType()).ToList();

        // Otherwise a run that still computes in the drain window would count as dead after Liveness.DeadAfter.
        Assert.Contains(typeof(RecordingHeartbeatService), hostedServices);
        Assert.IsLessThan(
            hostedServices.IndexOf(typeof(ProcessingRunner)),
            hostedServices.IndexOf(typeof(RecordingHeartbeatService)),
            "The heartbeat must stop after the runner, so it keeps the runs of a draining runner alive.");
    }

    [TestMethod]
    public void ShutdownTimeoutCoversTheDrainWindow()
    {
        using var app = new JwtTestApp();
        using var drainingApp = app.WithWebHostBuilder(builder => builder.UseSetting("Processing:ShutdownDrainTimeout", "00:01:00"));

        var shutdownTimeout = drainingApp.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout;

        Assert.IsGreaterThan(TimeSpan.FromMinutes(1), shutdownTimeout, "The host must wait longer than the drain window.");
    }
}
