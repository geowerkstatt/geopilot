namespace Geopilot.Api.Models;

/// <summary>
/// The outcome of the malware scan of a run's uploaded files. Persisted as text.
/// </summary>
public enum ScanState
{
    /// <summary>No scan ran: scanning is disabled, or the run never reached the scan.</summary>
    NotScanned,

    /// <summary>The scan ran and found no threat.</summary>
    Clean,

    /// <summary>The scan found a threat; details are recorded with the run.</summary>
    ThreatDetected,
}
