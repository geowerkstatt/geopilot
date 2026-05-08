namespace Geopilot.Api.FileAccess;

/// <summary>
/// Builds the on-disk name used by <see cref="IUploadFileStore"/> for a user-supplied file.
/// The user's original name is preserved so the upload directory is human-readable; if a job
/// already has a file under that name, a numeric suffix disambiguates without overwriting.
/// </summary>
public static class UploadFileNaming
{
    /// <summary>
    /// Returns a sanitized file name for the upload that is unique within the job's
    /// upload directory.
    /// </summary>
    public static string MakeUnique(Guid jobId, string originalFileName, IUploadFileStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var baseName = originalFileName.SanitizeFileName();
        if (!store.Exists(jobId, baseName))
            return baseName;

        var stem = Path.GetFileNameWithoutExtension(baseName);
        var extension = Path.GetExtension(baseName);
        for (var counter = 2; counter < int.MaxValue; counter++)
        {
            var candidate = $"{stem}_{counter}{extension}";
            if (!store.Exists(jobId, candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Could not generate a unique upload name for <{originalFileName}> in job <{jobId}>.");
    }
}
