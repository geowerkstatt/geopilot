namespace Geopilot.Api.FileAccess;

/// <summary>
/// Provides access to the upload and asset directories.
/// </summary>
public interface IDirectoryProvider
{
    /// <summary>
    /// Gets the root directory for file uploads.
    /// </summary>
    string UploadDirectory { get; }

    /// <summary>
    /// Gets the root directory for persisted assets.
    /// </summary>
    string AssetDirectory { get; }

    /// <summary>
    /// Gets the upload directory for the specified <paramref name="jobId"/>.
    /// </summary>
    /// <returns>The path of the upload directory.</returns>
    string GetUploadDirectoryPath(Guid jobId);

    /// <summary>
    /// Gets the asset directory for the specified <paramref name="jobId"/>.
    /// </summary>
    /// <returns>The path of the asset directory.</returns>
    string GetAssetDirectoryPath(Guid jobId);
}
