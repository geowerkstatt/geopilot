namespace Geopilot.Api.Contracts;

/// <summary>
/// The processing settings response schema.
/// </summary>
public class ProcessingSettingsResponse
{
    /// <summary>
    /// File extensions that are allowed for upload.
    /// All entries start with a "." like ".txt", ".xml" and the collection can include ".*" (all files allowed).
    /// </summary>
    public ICollection<string> AllowedFileExtensions { get; set; } = new List<string>();
}
