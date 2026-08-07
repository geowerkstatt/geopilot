using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api;

/// <summary>
/// Extension methods for resolving a <see cref="LocalizedText"/> to a single display string.
/// </summary>
internal static class LocalizedTextExtensions
{
    private static readonly string[] FallbackLanguages = ["en", "de", "fr", "it"];

    /// <summary>
    /// Resolves the localized text to a single display string for contexts that have no active UI
    /// language, such as the STAC output. Mirrors the frontend fallback order: English, then German,
    /// French and Italian, then any remaining language, and finally an empty string when no text is present.
    /// </summary>
    /// <param name="text">The localized text to resolve.</param>
    /// <returns>The resolved text, or an empty string when no language is present.</returns>
    public static string GetDisplayText(this LocalizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var language in FallbackLanguages)
        {
            if (text.TryGet(language, out var preferred))
                return preferred;
        }

        foreach (var language in text.Languages)
        {
            if (text.TryGet(language, out var remaining))
                return remaining;
        }

        return string.Empty;
    }
}
