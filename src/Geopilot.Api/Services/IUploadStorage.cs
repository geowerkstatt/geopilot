namespace Geopilot.Api.Services;

/// <summary>
/// The storage backend uploaded files are written to and read from, addressed by storage key.
/// </summary>
public interface IUploadStorage
{
    /// <summary>
    /// Where this storage keeps its files, as a credential-free URI (the blob container URI without any
    /// query, or the local root directory as file URI). Recorded in the execution protocol so a storage
    /// key stays a resolvable reference after a backend or bucket change. Must never contain secrets:
    /// the value ends up in a table that outlives every token.
    /// </summary>
    string StorageLocation { get; }

    /// <summary>
    /// Generates the URL the client uploads the file content to.
    /// </summary>
    /// <param name="key">The storage key for the file.</param>
    /// <param name="contentType">The content type of the file.</param>
    /// <param name="expiresIn">The duration for which the URL is valid.</param>
    /// <returns>The upload URL.</returns>
    Task<string> GenerateUploadUrlAsync(string key, string? contentType, TimeSpan expiresIn);

    /// <summary>
    /// Downloads a file from the upload storage to a local stream.
    /// </summary>
    /// <param name="key">The storage key of the file to download.</param>
    /// <param name="destination">The stream to write the file contents to.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    Task DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable stream to a file in the upload storage without buffering the entire file in memory.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    /// <param name="key">The storage key of the file to read.</param>
    /// <param name="cancellationToken">Cancels opening the stream.</param>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files in the upload storage matching the specified prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to filter by.</param>
    /// <returns>A list of keys, sizes, and last modified timestamps.</returns>
    Task<IReadOnlyList<(string Key, long Size, DateTime LastModified)>> ListFilesAsync(string prefix);

    /// <summary>
    /// Deletes a single file from the upload storage.
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
