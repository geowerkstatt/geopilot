using System.Text.RegularExpressions;

namespace Geopilot.PipelineCore.Pipeline;

/// <summary>
/// Filtering helpers for a sequence of <see cref="IPipelineFile"/>. They work on any file collection
/// (an array, an <see cref="IPipelineFileList"/> or any <see cref="IEnumerable{T}"/> of
/// <see cref="IPipelineFile"/>), so a process can filter its files regardless of how they were wired
/// into its run method parameter.
/// </summary>
public static class PipelineFileFilters
{
    /// <summary>
    /// Filters the files to those whose extension matches one of <paramref name="extensions"/>.
    /// Each extension is given without the leading dot (for example "xtf"); the comparison is
    /// case-insensitive.
    /// </summary>
    public static IEnumerable<IPipelineFile> WithExtensions(this IEnumerable<IPipelineFile> files, HashSet<string> extensions)
    {
        var normalized = extensions.Select(extension => extension.ToLowerInvariant()).ToHashSet();
        return files.Where(file => normalized.Contains(file.FileExtension.ToLowerInvariant()));
    }

    /// <summary>
    /// Filters the files to those whose original file name matches the regular expression
    /// <paramref name="namePattern"/>.
    /// </summary>
    public static IEnumerable<IPipelineFile> WithMatchingName(this IEnumerable<IPipelineFile> files, string namePattern)
    {
        return files.Where(file => Regex.IsMatch(file.OriginalFileName, namePattern));
    }

    /// <summary>
    /// Filters the files to those satisfying <paramref name="predicate"/>.
    /// </summary>
    public static IEnumerable<IPipelineFile> Matches(this IEnumerable<IPipelineFile> files, Func<IPipelineFile, bool> predicate)
    {
        return files.Where(predicate);
    }
}
