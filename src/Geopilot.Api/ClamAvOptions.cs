namespace Geopilot.Api;

/// <summary>
/// Configuration options for the ClamAV antivirus scanner.
/// </summary>
public class ClamAvOptions
{
    /// <summary>
    /// Whether ClamAV virus scanning is enabled. Requires cloud storage to be enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The hostname of the ClamAV daemon.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// The TCP port of the ClamAV daemon.
    /// </summary>
    public int Port { get; set; }
}
