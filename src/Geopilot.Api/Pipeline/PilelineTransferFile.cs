using Geopilot.Api.Pipeline;

namespace Geopilot.Api.Test.Pipeline;

/// <summary>
/// Represents a test implementation of the IPilelineTransferFile interface.
/// </summary>
public class PilelineTransferFile : IPilelineTransferFile
{
    /// <summary>
    /// Initializes a new instance of the PilelineTestTransferFile class with the specified file path.
    /// </summary>
    /// <param name="filePath">The full path to the file to be transferred. Cannot be null or empty.</param>
    public PilelineTransferFile(string filePath)
    {
        this.FilePath = filePath;
    }

    /// <inheritdoc/>
    public string FilePath { get; set; }

    /// <inheritdoc/>
    public Stream OpenFileStream()
    {
        return File.Open(FilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
    }
}
