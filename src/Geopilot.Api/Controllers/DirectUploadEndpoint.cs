using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Controllers;

/// <summary>
/// The write endpoint of the direct upload backend, taking the place of the cloud backend's presigned
/// URLs: <see cref="DirectUploadStorage.GenerateUploadUrlAsync"/> hands clients a URL of this endpoint
/// for every registered file. Only mapped when the direct backend is active, so the route does not
/// exist in cloud mode.
/// </summary>
public static class DirectUploadEndpoint
{
    /// <summary>
    /// Maps <c>PUT api/v2/upload/{uploadId}/{fileName}</c>.
    /// </summary>
    /// <remarks>
    /// Anonymous like the presigned URLs it replaces (public mandates allow anonymous deliveries).
    /// The guarantees a presigned URL gives are enforced here instead: the target is the storage key
    /// recorded at initiation, never a client-supplied path; only files registered for the upload
    /// session are accepted; the session expires after <see cref="UploadOptions.UploadUrlExpiryMinutes"/>;
    /// and the content must match the declared size.
    /// </remarks>
    public static void MapDirectUpload(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut("/api/v2/upload/{uploadId:guid}/{fileName}", HandleUploadAsync).AllowAnonymous();
    }

    private static async Task<IResult> HandleUploadAsync(
        Guid uploadId,
        string fileName,
        HttpRequest request,
        IUploadStore uploadStore,
        DirectUploadStorage uploadStorage,
        IOptions<UploadOptions> uploadOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(DirectUploadEndpoint));

        var upload = uploadStore.GetUpload(uploadId);
        if (upload is null)
        {
            logger.LogTrace("Rejected upload for unknown upload id <{UploadId}>.", uploadId);
            return Results.NotFound();
        }

        var file = upload.Files.FirstOrDefault(f => string.Equals(f.FileName, fileName, StringComparison.Ordinal));
        if (file is null)
        {
            logger.LogTrace("Rejected upload of file not registered for upload <{UploadId}>.", uploadId);
            return Results.NotFound();
        }

        // The cloud backend encodes the expiry into the presigned URL; here the session age carries it.
        var expiresIn = TimeSpan.FromMinutes(uploadOptions.Value.UploadUrlExpiryMinutes);
        if (DateTime.UtcNow - upload.CreatedAt > expiresIn)
        {
            logger.LogTrace("Rejected upload for expired upload session <{UploadId}>.", uploadId);
            return Results.Problem("The upload session has expired.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (request.ContentLength is { } declaredLength && declaredLength != file.ExpectedSize)
            return SizeMismatch(declaredLength, file.ExpectedSize);

        var written = await uploadStorage.WriteAsync(file.StorageKey, request.Body, cancellationToken);
        if (written != file.ExpectedSize)
        {
            // A partial file would pass the size check of a later retry against ListFilesAsync, so remove it.
            await uploadStorage.DeleteAsync(file.StorageKey);
            logger.LogTrace("Rejected upload for <{UploadId}>: {Written} bytes received, {Expected} declared.", uploadId, written, file.ExpectedSize);
            return SizeMismatch(written, file.ExpectedSize);
        }

        return Results.Created();
    }

    private static IResult SizeMismatch(long actual, long expected)
        => actual > expected
            ? Results.Problem($"The content is larger than the declared size of {expected} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge)
            : Results.Problem($"The content is smaller than the declared size of {expected} bytes.", statusCode: StatusCodes.Status400BadRequest);
}
