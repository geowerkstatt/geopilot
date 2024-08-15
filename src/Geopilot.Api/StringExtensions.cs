namespace Geopilot.Api;

/// <summary>
/// GeoPilot API string extensions.
/// </summary>
public static class StringExtensions
{
    // Get invalid characters for file names and add some platform-specific ones.
    private static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars()
        .Concat(new[] { '?', '$', '*', '|', '<', '>', '"', ':', '\\' }).ToArray();

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    /// <param name="fileName">The file name to sanitize.</param>
    /// <returns>The sanitized file name.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="fileName"/> is <c>null</c>,
    /// empty or white space.</exception>"
    public static string SanitizeFileName(this string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        return Path.GetFileName(new string(fileName
            .Trim()
            .ReplaceLineEndings(string.Empty)
            .Replace("..", string.Empty)
            .Replace("./", string.Empty)
            .Where(x => !invalidFileNameChars.Contains(x)).ToArray()));
    }
}
