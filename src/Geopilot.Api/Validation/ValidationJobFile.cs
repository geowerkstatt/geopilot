namespace Geopilot.Api.Validation;

/// <summary>
/// Represents a file associated with a validation job, including both the original and temporary file names used during processing.
/// </summary>
/// <param name="OriginalFileName">The original name of the file to be validated.</param>
/// <param name="TempFileName">The temporary file name assigned during the validation process.</param>
public record ValidationJobFile(string OriginalFileName, string TempFileName)
{
}
