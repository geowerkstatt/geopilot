namespace Geopilot.Api.Services;

/// <summary>
/// Provides cloud storage operations for file uploads via presigned URLs.
/// </summary>
public interface ICloudStorageService
{
    /// <summary>
    /// Generates a presigned URL for uploading a file to cloud storage.
    /// </summary>
    /// <param name="key">The storage key for the file.</param>
    /// <param name="contentType">The content type of the file.</param>
    /// <param name="expiresIn">The duration for which the URL is valid.</param>
    /// <returns>The presigned upload URL.</returns>
    Task<string> GeneratePresignedUploadUrlAsync(string key, string? contentType, TimeSpan expiresIn);

    /// <summary>
    /// Downloads a file from cloud storage to a local stream.
    /// </summary>
    /// <param name="key">The storage key of the file to download.</param>
    /// <param name="destination">The stream to write the file contents to.</param>
    Task DownloadAsync(string key, Stream destination);

    /// <summary>
    /// Lists files in cloud storage matching the specified prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to filter by.</param>
    /// <returns>A list of keys, sizes, and last modified timestamps.</returns>
    Task<IReadOnlyList<(string Key, long Size, DateTime LastModified)>> ListFilesAsync(string prefix);

    /// <summary>
    /// Deletes a single file from cloud storage.
    /// </summary>
    /// <param name="key">The storage key of the file to delete.</param>
    Task DeleteAsync(string key);

    /// <summary>
    /// Deletes all files matching the specified prefix.
    /// </summary>
    /// <param name="prefix">The key prefix of files to delete.</param>
    Task DeletePrefixAsync(string prefix);

    /// <summary>
    /// Gets the total size of all files matching the specified prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to filter by.</param>
    /// <returns>The total size in bytes.</returns>
    Task<long> GetTotalSizeAsync(string prefix);
}
