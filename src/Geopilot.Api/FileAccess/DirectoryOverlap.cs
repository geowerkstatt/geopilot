using System.Reflection;

namespace Geopilot.Api.FileAccess;

/// <summary>
/// Finds directories that share files: one is the other or lies below it. A directory geopilot cleans up must not
/// overlap one that belongs to something else, because the cleanups delete what they cannot account for.
/// </summary>
internal static class DirectoryOverlap
{
    /// <summary>
    /// The configured <see cref="FileAccessOptions"/> directories with their configuration keys. Read off the type
    /// instead of listed here: a listing is complete only until someone adds another directory, and what it costs to
    /// miss one is deleted data. The static <see cref="FileAccessOptions.SectionName"/> is no instance property and
    /// stays out by itself.
    /// </summary>
    internal static IEnumerable<(string Key, string Directory)> StorageDirectories(FileAccessOptions storage)
        => typeof(FileAccessOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead)
            .Select(property => (Key: $"{FileAccessOptions.SectionName}:{property.Name}", Directory: property.GetValue(storage) as string ?? string.Empty))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Directory));

    /// <summary>
    /// True when one path is the other or lies below it. Compared case-insensitively, so a case-sensitive
    /// filesystem may see an overlap reported that it would not have; refusing to start is the harmless side.
    /// </summary>
    internal static bool Overlaps(string left, string right)
    {
        var first = WithTrailingSeparator(left);
        var second = WithTrailingSeparator(right);

        return first.StartsWith(second, StringComparison.OrdinalIgnoreCase)
            || second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
    }

    private static string WithTrailingSeparator(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
}
