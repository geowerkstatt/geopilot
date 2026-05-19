using Microsoft.Extensions.Options;

namespace Geopilot.Api.FileAccess;

/// <inheritdoc />
public class DirectoryProvider : IDirectoryProvider
{
    /// <inheritdoc/>
    public string UploadDirectory { get; }

    /// <inheritdoc/>
    public string DownloadDirectory { get; }

    /// <inheritdoc/>
    public string AssetDirectory { get; }

    /// <inheritdoc/>
    public string PipelineDirectory { get; }

    /// <inheritdoc/>
    public string SharedDirectory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryProvider"/> class.
    /// </summary>
    public DirectoryProvider(IOptions<FileAccessOptions> fileAccessOptions)
    {
        if (fileAccessOptions == null)
            throw new InvalidOperationException("Missing file Access Options");

        var fileAccess = fileAccessOptions.Value;

        UploadDirectory = fileAccess.UploadDirectory;
        DownloadDirectory = fileAccess.DownloadDirectory;
        AssetDirectory = fileAccess.AssetsDirectory;
        PipelineDirectory = fileAccess.PipelineDirectory;
        SharedDirectory = fileAccess.SharedDirectory;
    }

    /// <inheritdoc/>
    public string GetUploadDirectoryPath(Guid jobId)
        => Path.Combine(UploadDirectory, jobId.ToString());

    /// <inheritdoc/>
    public string GetDownloadDirectoryPath(Guid jobId)
        => Path.Combine(DownloadDirectory, jobId.ToString());

    /// <inheritdoc/>
    public string GetAssetDirectoryPath(Guid jobId)
        => Path.Combine(AssetDirectory, jobId.ToString());

    /// <inheritdoc/>
    public string GetPipelineDirectoryPath(Guid jobId)
        => Path.Combine(PipelineDirectory, jobId.ToString());
}
