namespace Geopilot.Api.FileAccess;

/// <summary>
/// Per-job file store rooted at the configured visualization directory. Holds the self-describing
/// JSON visualization configs produced by pipeline steps, served from the visualization endpoint
/// and cleaned up on a short, dedicated retention (separate from the download store).
/// </summary>
public interface IVisualizationFileStore : IJobFileStore
{
}
