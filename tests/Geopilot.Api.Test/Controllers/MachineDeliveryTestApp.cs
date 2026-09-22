using Geopilot.Api.Authorization;
using Geopilot.Api.Enums;
using Microsoft.AspNetCore.Hosting;

namespace Geopilot.Api.Controllers;

/// <summary>
/// A host that offers machine delivery and writes its uploads itself, so the resource can be driven over HTTP
/// instead of by calling the controller. What sits between a request and the action body is exactly where the
/// interesting failures live: value providers that read the form before the action runs, the content type an
/// action is selected by, the request size middleware, and a body that arrives in pieces.
/// </summary>
internal class MachineDeliveryTestApp : JwtTestApp
{
    private readonly bool machineDeliveryEnabled;
    private readonly UploadBackend backend;
    private readonly int maxFileSizeMB;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineDeliveryTestApp"/> class.
    /// </summary>
    /// <param name="machineDeliveryEnabled">Whether the installation offers machine delivery.</param>
    /// <param name="backend">Where the installation keeps its uploads. Pinned either way rather than left to the
    /// default, so a developer overlay that switches the backend cannot quietly turn a test into one about the
    /// other kind of installation.</param>
    /// <param name="maxFileSizeMB">The largest file the installation accepts. Small by default, so a test can
    /// exceed it without moving gigabytes.</param>
    public MachineDeliveryTestApp(bool machineDeliveryEnabled = true, UploadBackend backend = UploadBackend.Direct, int maxFileSizeMB = 1)
    {
        this.machineDeliveryEnabled = machineDeliveryEnabled;
        this.backend = backend;
        this.maxFileSizeMB = maxFileSizeMB;
    }

    /// <summary>
    /// Where this host keeps its uploads. Removed with the host.
    /// </summary>
    public string RootDirectory { get; } = Path.Combine(Path.GetTempPath(), $"geopilot-machine-delivery-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // All read while the host is built, so none of them can come through ConfigureAppConfiguration.
        builder.UseSetting("MachineDelivery:Enabled", machineDeliveryEnabled ? "true" : "false");
        builder.UseSetting("Upload:Backend", backend.ToString());

        // Only read where the direct backend is active, and harmless otherwise.
        builder.UseSetting("Upload:Direct:Directory", RootDirectory);
        builder.UseSetting("Upload:MaxFileSizeMB", maxFileSizeMB.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Upload initiation is rate limited per installation, and a handful of tests exhaust ten requests a
        // minute in seconds.
        builder.UseSetting("Upload:RateLimitRequests", "1000");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(RootDirectory))
            Directory.Delete(RootDirectory, true);
    }
}
