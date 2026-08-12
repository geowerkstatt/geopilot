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
    /// Gets the original relative directory of the file within its logical hierarchy, or an empty string for files at the root.
    /// This is metadata only — files are not stored under this path on disk; downstream consumers (e.g. ZIP packaging) use it to reconstruct the hierarchy.
    /// Always uses forward slash as the separator, never has a leading or trailing slash, and never contains <c>..</c> segments.
    /// Example: a ZIP entry <c>a/b/c/deep.txt</c> exposes <c>a/b/c</c> here while <see cref="OriginalFileName"/> stays <c>deep.txt</c>.
    /// </summary>
    string OriginalRelativePath { get; }

    /// <summary>
    /// Opens a stream to read the contents of the file associated with the current object. The file may be
    /// backed by remote storage, in which case it is fetched on first access and reused for further reads.
    /// The returned stream supports seeking, so it can be handed to readers that need it (for example
    /// <see cref="System.IO.Compression.ZipArchive"/>).
    /// </summary>
    /// <param name="cancellationToken">Cancels a pending fetch of a remotely backed file.</param>
    /// <returns>A stream for reading the file contents. The caller owns and disposes it.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the file does not exist.</exception>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a filestream  to write the file.
    /// </summary>
    /// <returns>A filestream for writing the file contents.</returns>
    /// <exception cref="Exception">Thrown if the file already exists. Overwriting not allowed.</exception>
    FileStream OpenWriteFileStream();

    /// <summary>
    /// Returns the local filesystem path of a private copy of this file, so it can be handed to external
    /// tools (for example SQLite or command line utilities) that operate on files by path rather than streams.
    /// For a file received from another step the runtime creates a content-identical copy in the consuming
    /// step's working directory on first access and returns that copy's path; the originating step's file is
    /// never exposed or modified. For a file created by the current step the path is returned directly.
    /// Reading via <see cref="OpenReadAsync"/> stays cheap and never copies.
    /// </summary>
    /// <param name="cancellationToken">Cancels a pending copy or fetch.</param>
    /// <returns>The local filesystem path of a copy that is safe to read and modify in place.</returns>
    Task<string> GetLocalPathAsync(CancellationToken cancellationToken = default);
}
