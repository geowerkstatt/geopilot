namespace Geopilot.Api.Models;

/// <summary>
/// One artifact a pipeline step produced, by name: a user-facing download, a visualization config, or a
/// delivery payload file. Kept in the protocol so "which artifacts existed for this run" stays answerable
/// after the files themselves are cleaned up.
/// </summary>
public class PipelineRunArtifact
{
    /// <summary>
    /// The unique identifier for the artifact record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The step record this artifact belongs to.
    /// </summary>
    public int PipelineRunStepId { get; set; }

    /// <summary>
    /// Navigation to the step record.
    /// </summary>
    public PipelineRunStep Step { get; set; } = null!;

    /// <summary>
    /// What the artifact is: a download, a visualization config, or a delivery payload file.
    /// </summary>
    public ArtifactKind Kind { get; set; }

    /// <summary>
    /// The human-readable original file name.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// The file name the artifact was persisted under in its job file store.
    /// </summary>
    public string PersistedFileName { get; set; } = string.Empty;

    /// <summary>
    /// For a delivery file: whether it entered the pipeline as an upload (primary data) or was produced
    /// by a step (processed data). <see langword="null"/> for downloads and visualizations.
    /// </summary>
    public bool? FromUpload { get; set; }
}
