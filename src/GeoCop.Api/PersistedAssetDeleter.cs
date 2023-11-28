namespace GeoCop.Api;

/// <summary>
/// Migrates files delivered for validation into a persistent storage.
/// </summary>
public class PersistedAssetDeleter : IPersistedAssetDeleter
{
    private readonly ILogger<PersistedAssetDeleter> logger;
    private readonly string assetDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistedAssetDeleter"/> class.
    /// </summary>
    /// <param name="logger">The logger used for the instance.</param>
    /// <param name="configuration">The service configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown if required configuration values are not defined.</exception>
    public PersistedAssetDeleter(ILogger<PersistedAssetDeleter> logger, IConfiguration configuration)
    {
        this.logger = logger;
        assetDirectory = configuration.GetValue<string>("Storage:AssetsDirectory")
            ?? throw new InvalidOperationException("Missing root directory for persisted assets, the value can be configured as \"Storage:AssetsDirectory\"");
    }

    /// <inheritdoc/>
    public void DeleteJobAssets(Guid jobId)
    {
        try
        {
            Directory.Delete(Path.Combine(assetDirectory, jobId.ToString()), true);
        }
        catch (Exception e)
        {
            var message = $"Failed to delete assets for job <{jobId}>.";
            logger.LogError(e, message);
            throw new InvalidOperationException(message, e);
        }
    }
}
