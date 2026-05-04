namespace Geopilot.Api.Processing;

/// <summary>
/// Represents a file associated with a processing job, including both the original and temporary file names used during processing.
/// </summary>
/// <param name="OriginalFileName">The original name of the file to be processed.</param>
/// <param name="TempFileName">The temporary file name assigned during the upload.</param>
public record ProcessingJobFile(string OriginalFileName, string TempFileName);
