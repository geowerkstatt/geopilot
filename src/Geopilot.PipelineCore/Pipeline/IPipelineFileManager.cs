namespace Geopilot.Api.Pipeline;

/// <summary>
/// Manages temporary files for pipeline processing.
/// This interface provides a method to generate unique temporary file names based on the processor type, file type, and file extension.
/// </summary>
public interface IPipelineFileManager
{
    /// <summary>
    /// Generates a temporary file name for a given file type, and file extension. The file name is unique and can be used to store intermediate results during pipeline processing.
    /// </summary>
    /// <param name="fileType">The type of the file. This is a string that describes the purpose or content of the file, such as 'input', 'output', 'log', etc. The file name will be prefixed with this file type.</param>
    /// <param name="fileExtension">The File extension without leading dot.</param>
    /// <returns>Path of the format 'fileType_GUID.fileExtension'.</returns>
    string GenerateTempFileName(string fileType, string fileExtension);
}
