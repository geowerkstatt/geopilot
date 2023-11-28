namespace GeoCop.Api;

/// <summary>
/// Deletes files from a persistent storage.
/// </summary>
public interface IPersistedAssetDeleter
{
    /// <summary>
    /// Deletes all log files for a validation job from persistent storage.
    /// </summary>
    /// <param name="jobId"></param>
    void DeleteJobAssets(Guid jobId);
}
