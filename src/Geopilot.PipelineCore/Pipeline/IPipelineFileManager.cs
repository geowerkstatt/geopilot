using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Manages temporary files for pipeline processing.
/// This interface provides a method to generate unique temporary file names based on the processor type, file type, and file extension.
/// </summary>
public interface IPipelineFileManager
{
    /// <summary>
    /// Gets the absolute path to the working directory owned by this file manager.
    /// Processes may create files and subdirectories here for staging intermediate results
    /// (for example, input/output bind-mount directories for a container step).
    /// </summary>
    string WorkingDirectory { get; }

    /// <summary>
    /// Generates a transfer file using the specified file type, file extension, and original file name.
    /// </summary>
    /// <param name="originalFileName">The original name of the file without file extension.</param>
    /// <param name="fileExtension">The file extension to use for the generated file, without the leading dot (for example, "txt").</param>
    /// <returns>An instance of IPipelineTransferFile that represents the generated transfer file.</returns>
    IPipelineFile GeneratePipelineFile(string originalFileName, string fileExtension);
}
