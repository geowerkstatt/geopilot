namespace Geopilot.Api.FileAccess;

/// <inheritdoc cref="IDownloadFileStore" />
public class PhysicalDownloadFileStore : PhysicalJobFileStore, IDownloadFileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalDownloadFileStore"/> class.
    /// </summary>
    public PhysicalDownloadFileStore(IDirectoryProvider directoryProvider)
        : base((directoryProvider ?? throw new ArgumentNullException(nameof(directoryProvider))).GetDownloadDirectoryPath)
    {
    }
}
