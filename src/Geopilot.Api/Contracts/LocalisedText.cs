namespace Geowerkstatt.Ilicop.Web.Contracts
{
    /// <summary>
    /// Text that may be localized with a ISO639-1 language code.
    /// </summary>
    public record LocalisedText
    {
        /// <summary>
        /// Language the text is in as ISO639-1 language code (e.g., "en", "de").
        /// </summary>
        public string Language { get; init; } = string.Empty;

        /// <summary>
        /// The text in the specified language.
        /// </summary>
        public string Text { get; init; } = string.Empty;
    }
}
