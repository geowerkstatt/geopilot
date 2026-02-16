using Geopilot.Api.Pipeline;

namespace Geopilot.Api.Test.Pipeline;

/// <summary>
/// Represents a test implementation of the IPilelineTransferFile interface.
/// </summary>
public class PipelineTransferFile : IPipelineTransferFile
{
    /// <summary>
    /// Initializes a new instance of the PilelineTestTransferFile class with the specified file path.
    /// </summary>
    /// <param name="filePath">The full path to the file to be transferred. Cannot be null or empty.</param>
    /// <param name="originalFileNameWithoutExtension">The original file name without its extension. Cannot be null or empty.</param>
    public PipelineTransferFile(string originalFileNameWithoutExtension, string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalFileNameWithoutExtension, nameof(originalFileNameWithoutExtension));
        ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
        this.FilePath = filePath;
        this.OriginalFileNameWithoutExtension = originalFileNameWithoutExtension;
    }

    /// <inheritdoc/>
    public string OriginalFileName
    {
        get
        {
            return OriginalFileNameWithoutExtension + Extension;
        }
    }

    /// <inheritdoc/>
    public string FileName
    {
        get
        {
            return Path.GetFileName(FilePath);
        }
    }

    /// <inheritdoc/>
    public string FileNameWithoutExtension
    {
        get
        {
            return Path.GetFileNameWithoutExtension(FilePath);
        }
    }

    /// <inheritdoc/>
    public string Extension
    {
        get
        {
            return Path.GetExtension(FilePath);
        }
    }

    /// <inheritdoc/>
    public string FilePath { get; set; }

    /// <inheritdoc/>
    public string OriginalFileNameWithoutExtension { get; set; }

    /// <inheritdoc/>
    public Stream OpenFileStream()
    {
        return File.Open(FilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
    }
}
