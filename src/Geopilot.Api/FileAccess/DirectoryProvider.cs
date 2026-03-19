using Microsoft.Extensions.Options;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides access to the upload and asset directories.
/// </summary>
public class DirectoryProvider : IDirectoryProvider
{
    /// <inheritdoc/>
    public string UploadDirectory { get; }

    /// <inheritdoc/>
    public string AssetDirectory { get; }

    /// <inheritdoc/>
    public string PipelineDirectory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryProvider"/> class.
    /// </summary>
    public DirectoryProvider(IOptions<FileAccessOptions> fileAccessOptions)
    {
        if (fileAccessOptions == null)
            throw new InvalidOperationException("Missing file Access Options");

        var fileAccess = fileAccessOptions.Value;

        UploadDirectory = fileAccess.UploadDirectory;
        AssetDirectory = fileAccess.AssetsDirectory;
        PipelineDirectory = fileAccess.PipelineDirectory;
    }

    /// <inheritdoc/>
    public string GetAssetDirectoryPath(Guid jobId)
    {
        return Path.Combine(AssetDirectory, jobId.ToString());
    }

    /// <inheritdoc/>
    public string GetUploadDirectoryPath(Guid jobId)
    {
        return Path.Combine(UploadDirectory, jobId.ToString());
    }

    /// <inheritdoc/>
    public string GetPipelineDirectoryPath(Guid jobId)
    {
        return Path.Combine(PipelineDirectory, jobId.ToString());
    }
}
