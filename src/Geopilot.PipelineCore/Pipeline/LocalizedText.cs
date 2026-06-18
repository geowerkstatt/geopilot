using System.Text.Json.Serialization;

namespace Geopilot.PipelineCore.Pipeline;

/// <summary>
/// An immutable localized text: language-code keys (ISO 639, for example "de", "en",
/// "fr", "it") mapped to the text in that language. Serializes to and from a flat JSON
/// object so the wire format matches a plain dictionary. Language lookup is case
/// sensitive, matching the previous <see cref="Dictionary{TKey, TValue}"/> behavior.
/// </summary>
[JsonConverter(typeof(LocalizedTextJsonConverter))]
public sealed class LocalizedText : IEquatable<LocalizedText>
{
    private readonly Dictionary<string, string> values;

    /// <summary>Initializes a new instance from the given language-to-text entries.</summary>
    public LocalizedText(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = new Dictionary<string, string>(values);
    }

    /// <summary>An empty localized text (no languages).</summary>
    public static LocalizedText Empty { get; } = new LocalizedText(new Dictionary<string, string>());

    /// <summary>Creates a new instance from the given plain dictionary.</summary>
    public static LocalizedText FromDictionary(Dictionary<string, string> values) => new LocalizedText(values);

    /// <summary>Convenience conversion so callers can pass a plain dictionary.</summary>
    public static implicit operator LocalizedText(Dictionary<string, string> values) => new LocalizedText(values);

    /// <summary>The language codes present in this text.</summary>
    public IReadOnlyCollection<string> Languages => [.. values.Keys];

    /// <summary>The number of languages present.</summary>
    public int Count => values.Count;

    /// <summary>Whether no language is present.</summary>
    public bool IsEmpty => values.Count == 0;

    /// <summary>The text for the given language, or <see langword="null"/> if absent.</summary>
    public string? this[string language] => values.TryGetValue(language, out var text) ? text : null;

    /// <summary>Tries to get the text for the given language.</summary>
    public bool TryGet(string language, out string text)
    {
        if (values.TryGetValue(language, out var found))
        {
            text = found;
            return true;
        }

        text = string.Empty;
        return false;
    }

    /// <summary>Returns the entries as a new dictionary.</summary>
    public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>(values);

    /// <summary>Returns a new localized text with <paramref name="transform"/> applied to each value.</summary>
    public LocalizedText Map(Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var mapped = new Dictionary<string, string>(values.Count);
        foreach (var pair in values)
            mapped[pair.Key] = transform(pair.Value);
        return new LocalizedText(mapped);
    }

    /// <summary>
    /// Merges several localized texts into one. For each language, the present values are
    /// joined with <paramref name="separator"/>.
    /// </summary>
    public static LocalizedText Merge(IEnumerable<LocalizedText> parts, string separator)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(separator);
        var partsList = parts.ToList();
        var merged = new Dictionary<string, string>();
        foreach (var language in partsList.SelectMany(p => p.values.Keys).Distinct())
        {
            var texts = partsList.Where(p => p.values.ContainsKey(language)).Select(p => p.values[language]);
            merged[language] = string.Join(separator, texts);
        }

        return new LocalizedText(merged);
    }

    /// <inheritdoc/>
    public bool Equals(LocalizedText? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (values.Count != other.values.Count)
            return false;

        foreach (var pair in values)
        {
            if (!other.values.TryGetValue(pair.Key, out var otherValue) ||
                !string.Equals(pair.Value, otherValue, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as LocalizedText);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
