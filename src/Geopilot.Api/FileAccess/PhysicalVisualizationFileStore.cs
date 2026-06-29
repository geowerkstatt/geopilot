namespace Geopilot.Api.FileAccess;

/// <inheritdoc cref="IVisualizationFileStore" />
public class PhysicalVisualizationFileStore : PhysicalJobFileStore, IVisualizationFileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalVisualizationFileStore"/> class.
    /// </summary>
    public PhysicalVisualizationFileStore(IDirectoryProvider directoryProvider)
        : base((directoryProvider ?? throw new ArgumentNullException(nameof(directoryProvider))).GetVisualizationDirectoryPath)
    {
    }
}
