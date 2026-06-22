namespace Geopilot.PipelineCore.Pipeline;

/// <summary>
/// Manages temporary files for pipeline processing.
/// This interface provides a method to generate unique temporary file names based on the processor type, file type, and file extension.
/// </summary>
public interface IPipelineFileManager
{
    /// <summary>
    /// Generates a transfer file at the root of the step's logical hierarchy. Equivalent to calling
    /// <see cref="GeneratePipelineFile(string, string, string)"/> with an empty <c>originalRelativePath</c>.
    /// </summary>
    /// <param name="originalFileName">The original name of the file without file extension.</param>
    /// <param name="fileExtension">The file extension to use for the generated file, without the leading dot (for example, "txt").</param>
    /// <returns>An instance of IPipelineFile that represents the generated transfer file.</returns>
    IPipelineFile GeneratePipelineFile(string originalFileName, string fileExtension);

    /// <summary>
    /// Generates a transfer file and tags it with the specified original relative directory within the step's
    /// logical hierarchy. The relative directory is metadata only — files are not stored under this path on disk.
    /// Downstream consumers (e.g. ZIP packaging) read it from <see cref="IPipelineFile.OriginalRelativePath"/>.
    /// Rejects path-traversal attempts (<c>..</c> segments, absolute or rooted paths) by throwing
    /// an <see cref="ArgumentException"/> so the canonical-form contract of <see cref="IPipelineFile.OriginalRelativePath"/> holds.
    /// </summary>
    /// <param name="originalRelativePath">The original relative directory the file belongs to. Use an empty string for files at the root. Forward slash is the separator; backslashes and a leading or trailing separator are tolerated and normalized. Must not be null.</param>
    /// <param name="originalFileName">The original name of the file without file extension.</param>
    /// <param name="fileExtension">The file extension to use for the generated file, without the leading dot (for example, "txt").</param>
    /// <returns>An instance of IPipelineFile that represents the generated transfer file. The relative directory is exposed via <see cref="IPipelineFile.OriginalRelativePath"/>.</returns>
    IPipelineFile GeneratePipelineFile(string originalRelativePath, string originalFileName, string fileExtension);

    /// <summary>
    /// Creates an owned, content-identical copy of <paramref name="source"/> in this step's working directory,
    /// for example to keep an inspectable intermediate stage. The returned file belongs to the current step and
    /// may be freely modified, including in place by path via <see cref="IPipelineFile.GetLocalPath"/>.
    /// </summary>
    /// <param name="source">The file to copy. Cannot be null.</param>
    /// <param name="name">The original name of the copy without file extension; extension and relative directory are taken from <paramref name="source"/>.</param>
    /// <returns>A new <see cref="IPipelineFile"/> owned by the current step with the same content as <paramref name="source"/>.</returns>
    IPipelineFile CreateWritableCopy(IPipelineFile source, string name);
}
