namespace Geopilot.Pipeline;

/// <summary>
/// Tracking record for a visualization config produced by a pipeline step. The config object is
/// serialized to JSON and stored in the dedicated visualization store; the frontend fetches it from
/// the visualization endpoint and renders the component selected by the config's own type discriminator.
/// </summary>
/// <param name="OriginalFileName">The human-readable file name reported for the config.</param>
/// <param name="PersistedFileName">The file name on disk under the job's visualization directory.</param>
public record StepVisualization(string OriginalFileName, string PersistedFileName);
