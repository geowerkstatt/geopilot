namespace Geopilot.Api.FileAccess;

/// <summary>
/// Represents configuration options for directories.
/// </summary>
public class FileAccessOptions
{
    /// <summary>
    /// Gets or sets the root directory for file uploads. This is the base path where uploaded files will be stored.
    /// </summary>
    public string? UploadDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for storing asset files.
    /// </summary>
    public string? AssetsDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for storing pipeline files.
    /// </summary>
    public string? PipelineDirectory { get; set; }
}
