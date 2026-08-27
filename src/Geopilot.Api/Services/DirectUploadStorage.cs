using Microsoft.Extensions.Options;
using System.Web;

namespace Geopilot.Api.Services;

/// <summary>
/// Filesystem implementation of <see cref="IUploadStorage"/> for deployments without an object storage.
/// Files live under <see cref="UploadDirectOptions.Directory"/> with the same key structure the cloud
/// backend uses (uploads/{uploadId}/...), so listing, cleanup and preflight work unchanged. Instead of a
/// presigned URL, clients upload to the API's own upload endpoint, which writes through <see cref="WriteAsync"/>.
/// </summary>
public class DirectUploadStorage : IUploadStorage
{
    private readonly string rootDirectory;
    private readonly ILogger<DirectUploadStorage> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectUploadStorage"/> class.
    /// </summary>
    public DirectUploadStorage(IOptions<UploadDirectOptions> options, ILogger<DirectUploadStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.logger = logger;
        rootDirectory = Path.GetFullPath(options.Value.Directory);
        Directory.CreateDirectory(rootDirectory);
        StorageLocation = new Uri(rootDirectory).AbsoluteUri;
    }

    /// <inheritdoc/>
    public string StorageLocation { get; }

    /// <inheritdoc/>
    public Task<string> GenerateUploadUrlAsync(string key, string? contentType, TimeSpan expiresIn)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        // The URL points at the API's own upload endpoint instead of a presigned storage URL. Expiry is
        // not encoded into the URL; the endpoint checks the upload session's age against the same window.
        var (uploadId, fileName) = SplitKey(key);
        return Task.FromResult($"/api/v2/upload/{uploadId}/{HttpUtility.UrlEncode(fileName)}");
    }

    /// <summary>
    /// Streams <paramref name="content"/> into the file addressed by <paramref name="key"/>.
    /// Not part of <see cref="IUploadStorage"/>: with the cloud backend clients write directly to the
    /// object storage, only the direct upload endpoint writes through the API.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    public async Task<long> WriteAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var target = File.Create(path);
        await content.CopyToAsync(target, cancellationToken);

        logger.LogInformation("Stored uploaded file {Key} ({Size} bytes).", key, target.Length);
        return target.Length;
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default)
    {
        using var source = File.OpenRead(ResolvePath(key));
        await source.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(File.OpenRead(ResolvePath(key)));

    /// <inheritdoc/>
    public Task<IReadOnlyList<(string Key, long Size, DateTime LastModified)>> ListFilesAsync(string prefix)
    {
        var results = new List<(string Key, long Size, DateTime LastModified)>();

        foreach (var file in EnumerateFilesByPrefix(prefix))
        {
            results.Add((ToKey(file.FullName), file.Length, file.LastWriteTimeUtc));
        }

        return Task.FromResult<IReadOnlyList<(string Key, long Size, DateTime LastModified)>>(results);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key)
    {
        // Delete-if-exists like the cloud backend: File.Delete alone would still throw for a missing directory.
        var path = ResolvePath(key);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeletePrefixAsync(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        logger.LogInformation("Deleting uploaded files with prefix {Prefix}.", prefix);

        // Every caller passes a directory-shaped prefix (uploads/{uploadId}/), so deleting the
        // subtree is equivalent to deleting every matching key and removes the directory with it.
        var prefixPath = ResolvePath(prefix.TrimEnd('/'));
        if (Directory.Exists(prefixPath))
            Directory.Delete(prefixPath, recursive: true);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<long> GetTotalSizeAsync(string prefix)
        => (await ListFilesAsync(prefix)).Sum(f => f.Size);

    private static (Guid UploadId, string FileName) SplitKey(string key)
    {
        var parts = key.Split('/');
        if (parts.Length != 3 || parts[0] != "uploads" || !Guid.TryParse(parts[1], out var uploadId) || string.IsNullOrEmpty(parts[2]))
            throw new InvalidOperationException($"Key <{key}> does not match the expected structure uploads/{{uploadId}}/{{fileName}}.");

        return (uploadId, parts[2]);
    }

    private IEnumerable<FileInfo> EnumerateFilesByPrefix(string prefix)
    {
        // Enumerate from the root and filter on the key, so any prefix the cloud backend supports
        // (e.g. "uploads/" or "uploads/{uploadId}/") behaves identically here.
        if (!Directory.Exists(rootDirectory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            if (ToKey(path).StartsWith(prefix, StringComparison.Ordinal))
                yield return new FileInfo(path);
        }
    }

    private string ToKey(string fullPath)
        => Path.GetRelativePath(rootDirectory, fullPath).Replace('\\', '/');

    /// <summary>
    /// Resolves a storage key to a full path and confines it to the upload directory. Keys are
    /// server-generated, so an escaping key indicates a bug or tampering and fails loudly.
    /// </summary>
    private string ResolvePath(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var fullPath = Path.GetFullPath(Path.Combine(rootDirectory, key));
        if (!fullPath.StartsWith(rootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Key <{key}> resolves outside the upload directory.");

        return fullPath;
    }
}
