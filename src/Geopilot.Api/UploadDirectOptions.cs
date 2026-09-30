using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api;

/// <summary>
/// Settings of the direct upload backend: files are uploaded through the API into a local directory.
/// Only read and validated when <see cref="UploadOptions.Backend"/> is <see cref="Enums.UploadBackend.Direct"/>.
/// </summary>
public class UploadDirectOptions
{
    /// <summary>
    /// Gets the name of the configuration section that contains the direct backend settings.
    /// </summary>
    public static string SectionName => "Upload:Direct";

    /// <summary>
    /// The root directory uploaded files are stored in, one folder per upload id. It must belong to the
    /// direct upload backend alone: the upload cleanup treats every file below it as its own and deletes
    /// what it cannot account for, so a directory shared with anything else loses data.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string Directory { get; set; }
}
