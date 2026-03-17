using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a test implementation of the IPilelineTransferFile interface.
/// </summary>
public class PipelineTransferFile : IPipelineTransferFile
{
    /// <summary>
    /// Initializes a new instance of the PilelineTestTransferFile class with the specified file path.
    /// </summary>
    /// <param name="filePath">The full qualified file name.</param>
    /// <param name="originalFileName">The original file name with extension. Cannot be null or empty.</param>
    public PipelineTransferFile(string filePath, string originalFileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
        ArgumentException.ThrowIfNullOrEmpty(originalFileName, nameof(originalFileName));
        this.OriginalFileName = originalFileName;
        this.filePath = filePath;
    }

    private readonly string filePath;

    /// <inheritdoc/>
    public string OriginalFileName { get; private set; }

    /// <inheritdoc/>
    public string OriginalFileNameWithoutExtension
    {
        get
        {
            return Path.GetFileNameWithoutExtension(OriginalFileName);
        }
    }

    /// <inheritdoc/>
    public string FileExtension
    {
        get
        {
            var extension = Path.GetExtension(OriginalFileName);
            if (!string.IsNullOrEmpty(extension) && extension.StartsWith('.'))
                return extension.Substring(1);
            else
                return extension;
        }
    }

    /// <inheritdoc/>
    public FileStream OpenReadFileStream()
    {
        return File.OpenRead(this.filePath);
    }

    /// <inheritdoc/>
    public FileStream OpenWriteFileStream()
    {
        return File.Open(this.filePath, FileMode.CreateNew);
    }
}
