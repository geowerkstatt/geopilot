using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Stores, retrieves and removes <see cref="UploadInfo"/> instances in memory in a thread-safe manner.
/// </summary>
public class UploadStore : IUploadStore
{
    private readonly ConcurrentDictionary<Guid, UploadInfo> uploads = new();

    /// <inheritdoc/>
    public UploadInfo CreateUpload(Guid id, ImmutableList<CloudFileInfo> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var upload = new UploadInfo(
            Id: id,
            Files: files,
            CreatedAt: DateTime.Now);

        if (!uploads.TryAdd(id, upload))
            throw new InvalidOperationException($"An upload with id <{id}> already exists.");

        return upload;
    }

    /// <inheritdoc/>
    public UploadInfo? GetUpload(Guid uploadId) => uploads.TryGetValue(uploadId, out var upload) ? upload : null;

    /// <inheritdoc/>
    public bool RemoveUpload(Guid uploadId) => uploads.TryRemove(uploadId, out _);

    /// <inheritdoc/>
    public int GetActiveUploadCount() => uploads.Count;
}
