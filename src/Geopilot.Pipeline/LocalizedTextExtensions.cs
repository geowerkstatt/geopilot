using Geopilot.PipelineCore.Pipeline;
using System.Globalization;

namespace Geopilot.Pipeline;

/// <summary>
/// Extension methods for filling in a <see cref="LocalizedText"/> and resolving it to a single display string.
/// </summary>
internal static class LocalizedTextExtensions
{
    private static readonly string[] PreferredLanguages = ["en", "de", "fr", "it"];

    /// <summary>
    /// Fills the placeholders <c>{0}</c>, <c>{1}</c>, ... of every language with the same values, formatted with the
    /// invariant culture.
    /// </summary>
    /// <param name="format">The localized text with the placeholders.</param>
    /// <param name="arguments">The values, in the order of their placeholders.</param>
    /// <returns>The localized text with the values filled in.</returns>
    public static LocalizedText Format(this LocalizedText format, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(format);
        return format.Map(text => string.Format(CultureInfo.InvariantCulture, text, arguments));
    }

    /// <summary>
    /// Resolves the localized text to a single display string by taking the first language that is available in the order:
    /// the preferred language, if any, then English, German, French, Italian, then any remaining language.
    /// Entries that are missing or blank are skipped.
    /// </summary>
    /// <param name="text">The localized text to resolve.</param>
    /// <param name="preferredLanguage">The language to try first, or <see langword="null"/> to follow the order only.</param>
    /// <returns>The resolved text, or an empty string when no language carries text.</returns>
    public static string GetDisplayText(this LocalizedText text, string? preferredLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var languages = preferredLanguage is null ? PreferredLanguages : PreferredLanguages.Prepend(preferredLanguage);
        foreach (var language in languages.Concat(text.Languages))
        {
            if (text.TryGet(language, out var candidate) && !string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return string.Empty;
    }
}
