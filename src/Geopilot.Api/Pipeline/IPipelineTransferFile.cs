namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a file to be transferred as part of a pipeline process.
/// </summary>
public interface IPipelineTransferFile
{
    /// <summary>
    /// The original file name without its extension.
    /// </summary>
    string OriginalFileNameWithoutExtension { get; }

    /// <summary>
    /// Gets the original file name, including its extension.
    /// </summary>
    string OriginalFileName { get; }

    /// <summary>
    /// Gets the name of the file including file extension associated with the current object.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Gets the name of the file without its extension.
    /// </summary>
    string FileNameWithoutExtension { get; }

    /// <summary>
    /// The full path of the file associated with the current object.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the file extension of the file associated with the current object.
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Opens a stream to read the contents of the file associated with the current object.
    /// </summary>
    /// <returns>A stream for reading the file contents.</returns>
    Stream OpenFileStream();
}
