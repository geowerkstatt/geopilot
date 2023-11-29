namespace GeoCop.Api.FileAccess;

/// <summary>
/// Migrates files delivered for validation into a persistent storage.
/// </summary>
public class PersistedAssetDeleter : IPersistedAssetDeleter
{
    private readonly ILogger<PersistedAssetDeleter> logger;
    private readonly IDirectoryProvider directoryProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistedAssetDeleter"/> class.
    /// </summary>
    /// <param name="logger">The logger used for the instance.</param>
    /// <param name="directoryProvider">The service configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown if required configuration values are not defined.</exception>
    public PersistedAssetDeleter(ILogger<PersistedAssetDeleter> logger, IDirectoryProvider directoryProvider)
    {
        this.logger = logger;
        this.directoryProvider = directoryProvider;
    }

    /// <inheritdoc/>
    public void DeleteJobAssets(Guid jobId)
    {
        try
        {
            Directory.Delete(directoryProvider.GetAssetDirectoryPath(jobId), true);
        }
        catch (Exception e)
        {
            var message = $"Failed to delete assets for job <{jobId}>.";
            logger.LogError(e, message);
            throw new InvalidOperationException(message, e);
        }
    }
}
