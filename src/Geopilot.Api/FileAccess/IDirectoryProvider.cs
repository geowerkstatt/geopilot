namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides access to the configured root storage directories and their
/// per-job subdirectories.
/// </summary>
public interface IDirectoryProvider
{
    /// <summary>
    /// Gets the root directory for downloadable files produced by pipeline steps.
    /// </summary>
    string DownloadDirectory { get; }

    /// <summary>
    /// Gets the root directory for visualization config files produced by pipeline steps.
    /// </summary>
    string VisualizationDirectory { get; }

    /// <summary>
    /// Gets the root directory for persisted assets of deliveries. Until
    /// the declaration the payload waits in <see cref="AssetStagingDirectory"/>, so a job directory here
    /// belongs to a delivery, or to a declaration that failed after promoting it and retires with the job.
    /// </summary>
    string AssetDirectory { get; }

    /// <summary>
    /// Gets the root directory for staged delivery payloads: the files a finished, deliverable job tagged
    /// for delivery wait here until the delivery is declared and they move into <see cref="AssetDirectory"/>.
    /// Lies below the asset directory on purpose, so that move is a rename on one volume.
    /// </summary>
    string AssetStagingDirectory { get; }

    /// <summary>
    /// Gets the root directory for pipeline working files.
    /// </summary>
    string PipelineDirectory { get; }

    /// <summary>
    /// Gets the root directory for read-only resource files that pipeline definitions reference via
    /// <c>${file(path)}</c>. Provided by the deployment; not created by the application.
    /// </summary>
    string ResourcesDirectory { get; }

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
    /// Gets the per-job staging directory for the specified <paramref name="jobId"/>.
    /// </summary>
    string GetAssetStagingDirectoryPath(Guid jobId);

    /// <summary>
    /// Gets the per-job pipeline working directory for the specified <paramref name="jobId"/>.
    /// </summary>
    string GetPipelineDirectoryPath(Guid jobId);
}
