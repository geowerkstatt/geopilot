namespace Geopilot.Api.FileAccess;

/// <summary>
/// Finds directories that share files: one is the other or lies below it. A directory geopilot cleans up must not
/// overlap one that belongs to something else, because the cleanups delete what they cannot account for.
/// </summary>
internal static class DirectoryOverlap
{
    /// <summary>
    /// The configured data directories with their configuration keys, from <see cref="DataDirectories.Local"/>, the
    /// list the startup probe uses too; a test pins that it covers every <see cref="FileAccessOptions"/> property,
    /// because what it costs to miss a directory is deleted data. An empty directory is left out, the data
    /// annotations report it.
    /// </summary>
    internal static IEnumerable<(string Key, string Directory)> StorageDirectories(FileAccessOptions storage)
        => DataDirectories.Local(storage)
            .Where(directory => !string.IsNullOrWhiteSpace(directory.Path))
            .Select(directory => (directory.Key, directory.Path));

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
