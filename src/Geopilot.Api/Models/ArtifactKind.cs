namespace Geopilot.Api.Models;

/// <summary>
/// What a recorded step artifact is. Persisted as text.
/// </summary>
public enum ArtifactKind
{
    /// <summary>A user-facing download (log, report), persisted in the download store.</summary>
    Download,

    /// <summary>A visualization config, persisted in the visualization store.</summary>
    Visualization,

    /// <summary>A delivery payload file, persisted in the asset store for a deliverable run.</summary>
    Delivery,
}
