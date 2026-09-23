namespace Geopilot.Api.FileAccess;

/// <summary>
/// Per-job file store for the delivery payload of a finished, deliverable job that has not been delivered
/// yet. The pipeline runner writes the files a step tagged with <c>OutputAction.Delivery</c> here before
/// the pipeline's working directory is removed; declaring the delivery promotes them into
/// <see cref="IAssetFileStore"/>, and a job that is never delivered loses them with its retirement.
/// </summary>
public interface IAssetStagingFileStore : IJobFileStore
{
}
