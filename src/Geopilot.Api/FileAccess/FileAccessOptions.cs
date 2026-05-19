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
    /// Gets or sets the root directory for files uploaded by the user (the originals
    /// fed into the pipeline).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string UploadDirectory { get; set; }

    /// <summary>
    /// Gets or sets the root directory for downloadable files (e.g. validation logs)
    /// produced by pipeline steps and offered to the user via the download endpoint.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string DownloadDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for asset files. Pipeline outputs marked as part of
    /// the delivery payload are written here directly, and the originals from the
    /// upload directory are copied here on submission. Survives the cleanup sweep.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string AssetsDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for storing pipeline files.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string PipelineDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for application resource files.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string ResourcesDirectory { get; set; }

    /// <summary>
    /// Gets or sets the directory for shared files.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string SharedDirectory { get; set; }
}
