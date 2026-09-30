namespace Geopilot.Api.FileAccess;

/// <inheritdoc cref="IAssetFileStore" />
public class PhysicalAssetFileStore : PhysicalJobFileStore, IAssetFileStore
{
    private readonly IDirectoryProvider directoryProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalAssetFileStore"/> class.
    /// </summary>
    public PhysicalAssetFileStore(IDirectoryProvider directoryProvider)
        : base((directoryProvider ?? throw new ArgumentNullException(nameof(directoryProvider))).GetAssetDirectoryPath)
    {
        this.directoryProvider = directoryProvider;
    }

    /// <inheritdoc/>
    public void PromoteStagedFiles(Guid jobId)
    {
        var stagedDirectory = directoryProvider.GetAssetStagingDirectoryPath(jobId);
        if (!Directory.Exists(stagedDirectory))
            return;

        // A rename, because the staging root lies below the asset root: nothing is copied, and the job
        // directory appears in the asset directory as a whole or not at all.
        Directory.Move(stagedDirectory, directoryProvider.GetAssetDirectoryPath(jobId));
    }
}
