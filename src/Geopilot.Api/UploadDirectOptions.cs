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
    /// The root directory uploaded files are stored in. Owned exclusively by the direct upload backend;
    /// the files keep the same key structure as the cloud container (uploads/{uploadId}/...).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string Directory { get; set; }
}
