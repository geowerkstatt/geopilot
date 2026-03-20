using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Represents configuration options for directories.
/// </summary>
public class FileAccessOptions
{
    /// <summary>
    /// Gets the name of the configuration section that contains storage settings.
    /// </summary>
    public static string SectionName => "Storage";

    /// <summary>
    /// Gets or sets the root directory for file uploads. This is the base path where uploaded files will be stored.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string UploadDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for storing asset files.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string AssetsDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for storing pipeline files.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string PipelineDirectory { get; set; }
}
