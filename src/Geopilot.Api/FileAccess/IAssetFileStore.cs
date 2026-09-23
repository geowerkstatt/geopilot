namespace Geopilot.Api.FileAccess;

/// <summary>
/// Per-job file store rooted at the configured asset directory. Holds the payload of a delivery for
/// the long term. The pipeline runner stages the files a step tagged with
/// <c>OutputAction.Delivery</c> in <see cref="IAssetStagingFileStore"/>; declaring the delivery
/// promotes them into this store, so a job directory here belongs to a delivery, which is what
/// survives the periodic cleanup sweep.
/// </summary>
public interface IAssetFileStore : IJobFileStore
{
    /// <summary>
    /// Moves the job's staged delivery files into the asset directory. Does nothing when nothing is
    /// staged, so a declaration retried after an earlier attempt already promoted the files succeeds too.
    /// </summary>
    void PromoteStagedFiles(Guid jobId);
}
