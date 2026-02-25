namespace Geopilot.Api;

/// <summary>
/// Configuration options for the ClamAV antivirus scanner.
/// </summary>
public class ClamAvOptions
{
    /// <summary>
    /// The hostname of the ClamAV daemon.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// The TCP port of the ClamAV daemon.
    /// </summary>
    public int Port { get; set; } = 3310;
}
