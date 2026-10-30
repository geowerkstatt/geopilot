using System.Globalization;

namespace Geopilot.Api;

/// <summary>
/// GeoPilot API string extensions.
/// </summary>
public static class StringExtensions
{
    // The same characters on every operating system, so Windows and Linux sanitize a name alike. Control and format
    // characters are removed by their Unicode category, see IsControlOrFormat.
    private static readonly char[] invalidFileNameChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

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

        var sanitized = Path.GetFileName(new string(fileName
            .Trim()
            .ReplaceLineEndings(string.Empty)
            .Replace("..", string.Empty)
            .Replace("./", string.Empty)
            .Where(x => !invalidFileNameChars.Contains(x) && !IsControlOrFormat(x)).ToArray()));

        // Removing a character can join two dots into "..", so sanitize again until nothing changes. The stores sanitize
        // the names they are given once more, and a name that changed then would address another file.
        return string.IsNullOrWhiteSpace(sanitized) || string.Equals(sanitized, fileName, StringComparison.Ordinal)
            ? sanitized
            : sanitized.SanitizeFileName();
    }

    /// <summary>
    /// Whether <paramref name="fileName"/> can be stored as it is: <see cref="SanitizeFileName"/> leaves it unchanged.
    /// </summary>
    /// <param name="fileName">The file name to check.</param>
    /// <returns><c>true</c> if the name is not empty and sanitizing does not change it.</returns>
    public static bool IsSanitizedFileName(this string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) && string.Equals(fileName.SanitizeFileName(), fileName, StringComparison.Ordinal);

    // shortcut: checks UTF-16 code units, so format characters outside the BMP (such as tag characters) pass.
    private static bool IsControlOrFormat(char c) => char.GetUnicodeCategory(c)
        is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
