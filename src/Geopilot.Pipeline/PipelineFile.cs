using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a test implementation of the IPilelineTransferFile interface.
/// </summary>
public class PipelineFile : IPipelineFile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineFile"/> class for a file at the root of its logical hierarchy.
    /// </summary>
    /// <param name="filePath">The full qualified file name.</param>
    /// <param name="originalFileName">The original file name with extension. Cannot be null or empty.</param>
    public PipelineFile(string filePath, string originalFileName)
        : this(filePath, originalFileName, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineFile"/> class with an explicit original relative path.
    /// </summary>
    /// <param name="filePath">The full qualified file name.</param>
    /// <param name="originalFileName">The original file name with extension. Cannot be null or empty.</param>
    /// <param name="originalRelativePath">The original relative directory of the file within its logical hierarchy. Use an empty string for files at the root. Must not be null.</param>
    public PipelineFile(string filePath, string originalFileName, string originalRelativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
        ArgumentException.ThrowIfNullOrEmpty(originalFileName, nameof(originalFileName));
        ArgumentNullException.ThrowIfNull(originalRelativePath);
        this.OriginalFileName = originalFileName;
        this.OriginalRelativePath = originalRelativePath;
        this.filePath = filePath;
    }

    private readonly string filePath;

    /// <inheritdoc/>
    public string OriginalFileName { get; private set; }

    /// <inheritdoc/>
    public string OriginalRelativePath { get; private set; }

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
