using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline;

/// <summary>
/// Extension methods for resolving a <see cref="LocalizedText"/> to a single display string.
/// </summary>
internal static class LocalizedTextExtensions
{
    private static readonly string[] PreferredLanguages = ["en", "de", "fr", "it"];

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
