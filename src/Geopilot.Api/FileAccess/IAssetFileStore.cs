namespace Geopilot.Api.FileAccess;

/// <summary>
/// Per-job file store rooted at the configured asset directory. Holds the long-term
/// delivery payload — pipeline outputs flagged with <c>OutputAction.Delivery</c> are
/// written here directly, and originals from the upload directory are copied here on
/// submission. Survives the periodic cleanup sweep.
/// </summary>
public interface IAssetFileStore : IJobFileStore
{
}
