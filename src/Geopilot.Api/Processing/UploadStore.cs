using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Stores, retrieves and removes <see cref="UploadInfo"/> instances in memory in a thread-safe manner.
/// </summary>
/// <remarks>
/// Uploads are process-local, so the API has to run as exactly one instance. A second instance does not see
/// the uploads of the first one: starting a job answers 404 for an upload that was initiated elsewhere, even
/// though its files sit in cloud storage and would be readable from any instance. Only this bookkeeping is in
/// memory, so scaling the API out means persisting it together with the processing job state.
/// </remarks>
public class UploadStore : IUploadStore
{
    private readonly ConcurrentDictionary<Guid, UploadInfo> uploads = new();

    /// <inheritdoc/>
    public UploadInfo CreateUpload(Guid id, ImmutableList<UploadedFileInfo> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var upload = new UploadInfo(
            Id: id,
            Files: files,
            CreatedAt: DateTime.UtcNow);

        if (!uploads.TryAdd(id, upload))
            throw new InvalidOperationException($"An upload with id <{id}> already exists.");

        return upload;
    }

    /// <inheritdoc/>
    public void SetFiles(Guid uploadId, ImmutableList<UploadedFileInfo> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        // Compare and swap: a removal that lands in between must not be undone by a stale write.
        while (true)
        {
            if (!uploads.TryGetValue(uploadId, out var current))
                throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

            if (uploads.TryUpdate(uploadId, current with { Files = files }, current))
                return;
        }
    }

    /// <inheritdoc/>
    public UploadInfo? GetUpload(Guid uploadId) => uploads.TryGetValue(uploadId, out var upload) ? upload : null;

    /// <inheritdoc/>
    public bool RemoveUpload(Guid uploadId) => uploads.TryRemove(uploadId, out _);

    /// <inheritdoc/>
    public IReadOnlyCollection<UploadInfo> GetUploadsCreatedBefore(DateTime cutoff)
        => uploads.Values.Where(upload => upload.CreatedAt < cutoff).ToList();

    /// <inheritdoc/>
    public int GetActiveUploadCount() => uploads.Count;
}
