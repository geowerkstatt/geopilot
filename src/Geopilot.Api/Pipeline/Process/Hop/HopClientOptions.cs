namespace Geopilot.Api.Pipeline.Process.Hop;

/// <summary>
/// Options for <see cref="HopClient"/>. <see cref="JobsDirectory"/> must point at the host-side path
/// that the Hop worker has mounted as its jobs directory (see the worker README for the shared-folder protocol).
/// </summary>
internal sealed class HopClientOptions
{
    /// <summary>Gets or sets the local path to the folder where the Hop worker processes jobs. A GUID subfolder is created per job.</summary>
    public string JobsDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets how often to poll for the worker's <c>output.ready</c> sentinel. Defaults to one second.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum time to wait for a single job to complete. Defaults to ten minutes.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
}
