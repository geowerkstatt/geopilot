namespace Geopilot.Api.FileAccess;

/// <inheritdoc cref="IAssetFileStore" />
public class PhysicalAssetFileStore : PhysicalJobFileStore, IAssetFileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalAssetFileStore"/> class.
    /// </summary>
    public PhysicalAssetFileStore(IDirectoryProvider directoryProvider)
        : base((directoryProvider ?? throw new ArgumentNullException(nameof(directoryProvider))).GetAssetDirectoryPath)
    {
    }
}
