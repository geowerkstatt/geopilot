using System.Globalization;

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
        fileName = fileName.Trim().ReplaceLineEndings(string.Empty);

        // Get invalid characters for file names and add some platform-specific ones.
        var invalidFileNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '?', '$', '*', '|', '<', '>', '"', ':' }).ToArray();

        foreach (var invalidFileNameChar in invalidFileNameChars)
        {
            fileName = fileName.Replace(invalidFileNameChar.ToString(CultureInfo.InvariantCulture), string.Empty);
        }

        return fileName;
    }
}
