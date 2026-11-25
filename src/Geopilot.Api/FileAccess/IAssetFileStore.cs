namespace Geopilot.Api.FileAccess;

/// <summary>
/// Per-job file store rooted at the configured asset directory. Holds the payload of a delivery for
/// the long term. The pipeline runner stages the files a step tagged with
/// <c>OutputAction.Delivery</c> in <see cref="IAssetStagingFileStore"/>; once the delivery is saved,
/// its files are promoted into this store, so a job directory here belongs to a saved delivery, which
/// is what survives the periodic cleanup sweep.
/// </summary>
public interface IAssetFileStore : IJobFileStore
{
    /// <summary>
    /// Moves the job's staged delivery files into the asset directory. Does nothing when nothing is
    /// staged, so the creation and the cleanup can both attempt it; throws when the job directory
    /// already exists in the asset directory.
    /// </summary>
    void PromoteStagedFiles(Guid jobId);
}
