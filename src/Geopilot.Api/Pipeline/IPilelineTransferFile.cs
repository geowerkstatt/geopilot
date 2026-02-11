namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a file to be transferred as part of a pipeline process.
/// </summary>
public interface IPilelineTransferFile
{
    /// <summary>
    /// The original file name without its extension.
    /// </summary>
    string OrginalFileNameWithoutExtension { get; }

    /// <summary>
    /// Gets the original file name, including its extension.
    /// </summary>
    string OrginalFileName => OrginalFileNameWithoutExtension + Extension;

    /// <summary>
    /// Gets the name of the file including file extension associated with the current object.
    /// </summary>
    string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the name of the file without its extension.
    /// </summary>
    string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>
    /// The full path of the file associated with the current object.
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
