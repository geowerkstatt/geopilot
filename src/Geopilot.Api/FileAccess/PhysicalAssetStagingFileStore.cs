namespace Geopilot.Api.FileAccess;

/// <inheritdoc cref="IAssetStagingFileStore" />
public class PhysicalAssetStagingFileStore : PhysicalJobFileStore, IAssetStagingFileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalAssetStagingFileStore"/> class.
    /// </summary>
    public PhysicalAssetStagingFileStore(IDirectoryProvider directoryProvider)
        : base((directoryProvider ?? throw new ArgumentNullException(nameof(directoryProvider))).GetAssetStagingDirectoryPath)
    {
    }
}
