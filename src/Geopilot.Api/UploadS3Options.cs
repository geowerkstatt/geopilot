using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api;

/// <summary>
/// Settings of the S3 provider of the cloud upload backend. Only read and validated when
/// <see cref="UploadCloudOptions.Provider"/> is <see cref="Enums.CloudStorageProvider.S3"/>.
/// The bucket and the browser settings (CORS, CSP endpoint) stay in <see cref="UploadCloudOptions"/>.
/// </summary>
public class UploadS3Options
{
    /// <summary>
    /// Gets the name of the configuration section that contains the S3 settings.
    /// </summary>
    public static string SectionName => "Upload:Cloud:S3";

    /// <summary>
    /// The endpoint of the S3 service (e.g., "https://s3.example.com").
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public required string ServiceUrl { get; set; }

    /// <summary>
    /// The region the requests are signed for. Use the value the provider documents.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string Region { get; set; }

    /// <summary>
    /// The access key ID.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string AccessKeyId { get; set; }

    /// <summary>
    /// The secret access key.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string SecretAccessKey { get; set; }

    /// <summary>
    /// Whether to address the bucket in the path ("https://host/bucket/key") instead of the host name
    /// ("https://bucket.host/key"). Most S3-compatible providers and emulators need it.
    /// </summary>
    public bool ForcePathStyle { get; set; }
}
