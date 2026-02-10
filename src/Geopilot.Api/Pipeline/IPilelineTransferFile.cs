namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a file to be transferred as part of a pipeline process.
/// </summary>
public interface IPilelineTransferFile
{
    /// <summary>
    /// Gets the name of the file associated with the current object.
    /// </summary>
    string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the full path of the file associated with the current object.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the file extension of the file associated with the current object.
    /// </summary>
    string Extension => Path.GetExtension(FilePath);

    /// <summary>
    /// Opens a stream to read the contents of the file associated with the current object.
    /// </summary>
    /// <returns>A stream for reading the file contents.</returns>
    Stream OpenFileStream();
}
