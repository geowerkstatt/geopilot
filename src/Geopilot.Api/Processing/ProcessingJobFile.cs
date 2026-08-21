namespace Geopilot.Api.Processing;

/// <summary>
/// Represents a file associated with a processing job.
/// </summary>
/// <param name="OriginalFileName">The original name of the file to be processed.</param>
/// <param name="TempFileName">The sanitized file name assigned during the upload, unique within the job.</param>
/// <param name="StorageKey">The storage key the file is fetched from while the job and its delivery are alive.</param>
public record ProcessingJobFile(string OriginalFileName, string TempFileName, string StorageKey);
