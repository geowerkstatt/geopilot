namespace Geopilot.Api;

/// <summary>
/// GeoPilot API extensions.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    /// <param name="fileName">The file name to sanitize.</param>
    /// <returns>The sanitized file name.</returns>
    public static string SanitizeFileName(this string fileName)
    {
        // Get invalid characters for file names and add some platform-specific ones.
        var invalidFileNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '?', '$', '*', '|', '<', '>', '"', ':' }).ToArray();

        return new string(fileName
            .Trim()
            .ReplaceLineEndings(string.Empty)
            .Replace("..", string.Empty)
            .Replace("./", string.Empty)
            .Where(x => !invalidFileNameChars.Contains(x)).ToArray());
    }
}
