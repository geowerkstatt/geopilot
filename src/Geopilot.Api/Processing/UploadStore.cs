using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Stores, retrieves and removes <see cref="UploadInfo"/> instances as <see cref="UploadSession"/> rows.
/// </summary>
/// <remarks>
/// The sessions live in the database, so every instance of the API sees the same uploads and they survive a restart.
/// </remarks>
public class UploadStore : IUploadStore
{
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadStore"/> class.
    /// </summary>
    public UploadStore(Context context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public async Task<UploadInfo> CreateUploadAsync(Guid id, ImmutableList<UploadedFileInfo> files, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var session = new UploadSession
        {
            Id = id,
            CreatedAt = DateTime.UtcNow,
            Files = files.Select(file => new UploadSessionFile
            {
                FileName = file.FileName,
                StorageKey = file.StorageKey,
                DeclaredSize = file.ExpectedSize,
                ContentType = file.ContentType,
            }).ToList(),
        };

        context.UploadSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        return new UploadInfo(id, files, session.CreatedAt);
    }

    /// <inheritdoc/>
    public async Task<UploadInfo?> GetUploadAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        var session = await context.UploadSessions
            .AsNoTracking()
            .Include(s => s.Files.OrderBy(f => f.Id))
            .SingleOrDefaultAsync(s => s.Id == uploadId, cancellationToken);

        return session is null ? null : ToUploadInfo(session);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveUploadAsync(Guid uploadId, CancellationToken cancellationToken = default)
        => await context.UploadSessions.Where(s => s.Id == uploadId).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Guid>> GetUploadIdsCreatedBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => await context.UploadSessions.Where(s => s.CreatedAt < cutoff).Select(s => s.Id).ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<int> GetActiveUploadCountAsync(CancellationToken cancellationToken = default)
        => context.UploadSessions.CountAsync(cancellationToken);

    private static UploadInfo ToUploadInfo(UploadSession session)
        => new(
            session.Id,
            session.Files.Select(f => new UploadedFileInfo(f.FileName, f.StorageKey, f.DeclaredSize, f.ContentType)).ToImmutableList(),
            session.CreatedAt);
}
