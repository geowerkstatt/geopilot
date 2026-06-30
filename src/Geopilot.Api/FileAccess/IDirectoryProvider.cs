namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides access to the configured root storage directories and their
/// per-job subdirectories.
/// </summary>
public interface IDirectoryProvider
{
    /// <summary>
    /// Gets the root directory for files uploaded by the user (the originals fed into the pipeline).
    /// </summary>
    string UploadDirectory { get; }

    /// <summary>
    /// Gets the root directory for downloadable files produced by pipeline steps.
    /// </summary>
    string DownloadDirectory { get; }

    /// <summary>
    /// Gets the root directory for visualization config files produced by pipeline steps.
    /// </summary>
    string VisualizationDirectory { get; }

    /// <summary>
    /// Gets the root directory for persisted assets — pipeline outputs marked as part
    /// of the delivery payload land here directly, and originals from the upload
    /// directory are copied here on submission.
    /// </summary>
    string AssetDirectory { get; }

    /// <summary>
    /// Gets the root directory for pipeline working files.
    /// </summary>
    string PipelineDirectory { get; }

    /// <summary>
    /// Gets the root directory for shared files.
    /// </summary>
    string SharedDirectory { get; }

    /// <summary>
    /// Gets the per-job upload directory for the specified <paramref name="jobId"/>.
    /// </summary>
    string GetUploadDirectoryPath(Guid jobId);

    /// <summary>
    /// Gets the per-job download directory for the specified <paramref name="jobId"/>.
    /// </summary>
    string GetDownloadDirectoryPath(Guid jobId);

    /// <summary>
    /// Gets the per-job visualization directory for the specified <paramref name="jobId"/>.
    /// </summary>
    string GetVisualizationDirectoryPath(Guid jobId);

    /// <summary>
    /// Gets the per-job asset directory for the specified <paramref name="jobId"/>.
    /// </summary>
    string GetAssetDirectoryPath(Guid jobId);

    /// <summary>
    /// Gets the per-job pipeline working directory for the specified <paramref name="jobId"/>.
    /// </summary>
    string GetPipelineDirectoryPath(Guid jobId);
}
