using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Managed store for <see cref="UploadInfo"/> instances: uploads that have been initiated (files placed in
/// cloud storage) but not yet turned into a processing job. Implementations must be thread safe.
/// </summary>
public interface IUploadStore
{
    /// <summary>
    /// Creates and stores a new <see cref="UploadInfo"/> with the given <paramref name="id"/> and <paramref name="files"/>.
    /// </summary>
    UploadInfo CreateUpload(Guid id, ImmutableList<CloudFileInfo> files);

    /// <summary>
    /// Retrieves an <see cref="UploadInfo"/> by its id.
    /// </summary>
    /// <param name="uploadId">The id of the upload.</param>
    /// <returns>The upload, or <see langword="null"/> when no upload with the specified id exists.</returns>
    UploadInfo? GetUpload(Guid uploadId);

    /// <summary>
    /// Removes the upload from the store.
    /// </summary>
    /// <param name="uploadId">The id of the upload to remove.</param>
    /// <returns><see langword="true"/> when the upload existed and was removed.</returns>
    bool RemoveUpload(Guid uploadId);

    /// <summary>
    /// Number of uploads currently tracked (initiated but not yet consumed by a job).
    /// </summary>
    int GetActiveUploadCount();
}
