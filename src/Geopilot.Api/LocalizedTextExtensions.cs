using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api;

/// <summary>
/// Extension methods for resolving a <see cref="LocalizedText"/> to a single display string.
/// </summary>
internal static class LocalizedTextExtensions
{
    private static readonly string[] PreferredLanguages = ["en", "de", "fr", "it"];

    /// <summary>
    /// Resolves the localized text to a single display string by taking the first language that is available in the order:
    /// English, German, French, Italian, then any remaining language.
    /// Entries that are missing or blank are skipped.
    /// </summary>
    /// <param name="text">The localized text to resolve.</param>
    /// <returns>The resolved text, or an empty string when no language carries text.</returns>
    public static string GetDisplayText(this LocalizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var language in PreferredLanguages.Concat(text.Languages))
        {
            if (text.TryGet(language, out var candidate) && !string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return string.Empty;
    }
}
