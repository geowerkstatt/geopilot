namespace Geopilot.Api.FileAccess;

/// <inheritdoc cref="IUploadFileStore" />
public class PhysicalUploadFileStore : PhysicalJobFileStore, IUploadFileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalUploadFileStore"/> class.
    /// </summary>
    public PhysicalUploadFileStore(IDirectoryProvider directoryProvider)
        : base((directoryProvider ?? throw new ArgumentNullException(nameof(directoryProvider))).GetUploadDirectoryPath)
    {
    }
}
