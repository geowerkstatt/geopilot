namespace Geopilot.Api.FileAccess;

/// <summary>
/// Builds the on-disk name used for a user-supplied file. The user's original name is preserved so the
/// job's files stay human-readable; if the job already uses that name, a numeric suffix disambiguates
/// without overwriting.
/// </summary>
public static class UploadFileNaming
{
    /// <summary>
    /// Returns a sanitized file name that is unique among <paramref name="usedNames"/> and adds it there.
    /// Names are assigned up front for the whole upload, before any file exists on disk, because an
    /// uploaded file is only fetched once a step reads it.
    /// </summary>
    /// <param name="originalFileName">The name the user uploaded.</param>
    /// <param name="usedNames">The names already handed out for this job. The returned name is added to it.</param>
    public static string MakeUnique(string originalFileName, ISet<string> usedNames)
    {
        ArgumentNullException.ThrowIfNull(usedNames);

        var baseName = originalFileName.SanitizeFileName();
        if (usedNames.Add(baseName))
            return baseName;

        var stem = Path.GetFileNameWithoutExtension(baseName);
        var extension = Path.GetExtension(baseName);
        for (var counter = 2; counter < int.MaxValue; counter++)
        {
            var candidate = $"{stem}_{counter}{extension}";
            if (usedNames.Add(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Could not generate a unique upload name for <{originalFileName}>.");
    }
}
