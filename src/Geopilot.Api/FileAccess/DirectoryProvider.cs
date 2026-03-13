using Microsoft.Extensions.Options;
using System.IO;

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

        if (fileAccess == null)
            throw new InvalidOperationException("Missing file Access Options \"Storage\"");

        UploadDirectory = fileAccess.UploadDirectory ?? throw new InvalidOperationException("Missing root directory for file uploads, the value can be configured as \"Storage:UploadDirectory\"");
        AssetDirectory = fileAccess.AssetsDirectory ?? throw new InvalidOperationException("Missing root directory for persisted assets, the value can be configured as \"Storage:AssetsDirectory\"");
        PipelineDirectory = fileAccess.PipelineDirectory ?? throw new InvalidOperationException("Missing root pipeline directory, the value can be configured as \"Storage:PipelineDirectory\"");
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
