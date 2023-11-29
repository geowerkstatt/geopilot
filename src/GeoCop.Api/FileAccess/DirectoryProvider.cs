namespace GeoCop.Api.FileAccess;

/// <summary>
/// Provides access to the upload and asset directories.
/// </summary>
public class DirectoryProvider : IDirectoryProvider
{
    private readonly string uploadDirecory;
    private readonly string assetDicrectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryProvider"/> class.
    /// </summary>
    public DirectoryProvider(IConfiguration configuration)
    {
        uploadDirecory = configuration.GetValue<string>("Storage:UploadDirectory")
            ?? throw new InvalidOperationException("Missing root directory for file uploads, the value can be configured as \"Storage:UploadDirectory\"");
        assetDicrectory = configuration.GetValue<string>("Storage:AssetsDirectory")
            ?? throw new InvalidOperationException("Missing root directory for persisted assets, the value can be configured as \"Storage:AssetsDirectory\"");
    }

    /// <inheritdoc/>
    public string GetAssetDirectoryPath(Guid jobId)
    {
        return Path.Combine(assetDicrectory, jobId.ToString());
    }

    /// <inheritdoc/>
    public string GetUploadDirectoryPath(Guid jobId)
    {
        return Path.Combine(uploadDirecory, jobId.ToString());
    }
}
