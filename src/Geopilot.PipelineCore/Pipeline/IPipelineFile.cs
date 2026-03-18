namespace Geopilot.PipelineCore.Pipeline;

/// <summary>
/// Represents a pipeline file as part of a pipeline process.
/// </summary>
public interface IPipelineFile
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
    /// Gets the file extension of the file associated with the current object.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Opens a filestream to read the contents of the file associated with the current object.
    /// </summary>
    /// <returns>A filestream for reading the file contents.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the file does not exist.</exception>
    FileStream OpenReadFileStream();

    /// <summary>
    /// Opens a filestream  to write the file.
    /// </summary>
    /// <returns>A filestream for writing the file contents.</returns>
    /// <exception cref="Exception">Thrown if the file already exists. Overwriting not allowed.</exception>
    FileStream OpenWriteFileStream();
}
