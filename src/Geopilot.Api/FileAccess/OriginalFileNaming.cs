namespace Geopilot.Api.FileAccess;

/// <summary>
/// Builds the on-disk name of a file that keeps its original name: an uploaded file in the pipeline's
/// working directory and a delivered file in the asset store. The original name is preserved so the
/// files stay recognisable; if the name is already taken, a numeric suffix disambiguates without overwriting.
/// </summary>
public static class OriginalFileNaming
{
    /// <summary>
    /// Returns a sanitized file name that is unique among <paramref name="usedNames"/> and adds it there.
    /// The first file keeps its name, every further one gets a counter before the extension
    /// (<c>myFile_2.pdf</c>, <c>myFile_3.pdf</c>). Uniqueness comes from the set, not from the disk,
    /// because an uploaded file is only fetched once a step reads it.
    /// </summary>
    /// <param name="originalFileName">The name the file arrived or was produced with.</param>
    /// <param name="usedNames">The names already handed out in the same directory. The returned name is added to it.</param>
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
            $"Could not generate a unique file name for <{originalFileName}>.");
    }
}
