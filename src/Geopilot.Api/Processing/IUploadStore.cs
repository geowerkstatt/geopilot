using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Managed store for <see cref="UploadInfo"/> instances: uploads that have been initiated (files placed in
/// upload storage) but not yet released.
/// </summary>
public interface IUploadStore
{
    /// <summary>
    /// Creates and stores a new <see cref="UploadInfo"/> with the given <paramref name="id"/> and <paramref name="files"/>.
    /// </summary>
    Task<UploadInfo> CreateUploadAsync(Guid id, ImmutableList<UploadedFileInfo> files, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an <see cref="UploadInfo"/> by its id, with its files in the order they were registered.
    /// </summary>
    /// <param name="uploadId">The id of the upload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The upload, or <see langword="null"/> when no upload with the specified id exists.</returns>
    Task<UploadInfo?> GetUploadAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the upload from the store.
    /// </summary>
    /// <param name="uploadId">The id of the upload to remove.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the upload existed and was removed.</returns>
    Task<bool> RemoveUploadAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the ids of the uploads created before <paramref name="cutoff"/>.
    /// </summary>
    /// <param name="cutoff">The point in time, in UTC, the uploads must be older than.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ids of the matching uploads.</returns>
    Task<IReadOnlyCollection<Guid>> GetUploadIdsCreatedBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of uploads currently stored (initiated but not yet released), across all instances.
    /// </summary>
    Task<int> GetActiveUploadCountAsync(CancellationToken cancellationToken = default);
}
