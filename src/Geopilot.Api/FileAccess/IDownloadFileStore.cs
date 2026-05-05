namespace Geopilot.Api.FileAccess;

/// <summary>
/// Per-job file store rooted at the configured download directory. Holds the
/// pipeline outputs flagged as user-downloadable (e.g. validation logs).
/// </summary>
public interface IDownloadFileStore : IJobFileStore
{
}
