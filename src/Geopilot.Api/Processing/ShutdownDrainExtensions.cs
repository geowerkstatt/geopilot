namespace Geopilot.Api.Processing;

/// <summary>
/// Keeps the host's shutdown timeout in line with <see cref="ProcessingOptions.ShutdownDrainTimeout"/>.
/// </summary>
public static class ShutdownDrainExtensions
{
    // Covers the protocol writes and the services that stop after the runner.
    private static readonly TimeSpan ShutdownReserve = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Raises <see cref="HostOptions.ShutdownTimeout"/> (30 s by default) to at least the drain window plus a
    /// reserve, because the host stops waiting for its services after that timeout and would otherwise cut the
    /// window short. A larger configured timeout stays.
    /// </summary>
    public static void EnsureShutdownTimeoutCoversDrainWindow(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var drainTimeout = builder.Configuration.GetValue<TimeSpan>($"Processing:{nameof(ProcessingOptions.ShutdownDrainTimeout)}");
        var requiredShutdownTimeout = drainTimeout + ShutdownReserve;
        builder.Services.PostConfigure<HostOptions>(options =>
        {
            if (options.ShutdownTimeout < requiredShutdownTimeout)
                options.ShutdownTimeout = requiredShutdownTimeout;
        });
    }
}
