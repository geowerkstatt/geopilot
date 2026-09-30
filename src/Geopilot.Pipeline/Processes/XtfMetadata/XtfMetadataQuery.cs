using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Processes.XtfMetadata;

/// <summary>
/// Where one metadata value comes from, the configuration behind a key such as <c>scope</c>. It arrives as a YAML
/// mapping and binds through the JSON round trip of the configuration binding, hence the explicit property names. An
/// unknown key, such as a misspelled <c>fetch</c>, fails that binding instead of being ignored.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record XtfMetadataQuery
{
    /// <summary>
    /// XPath applied to every object of the transfer file. Its first step names the class as <c>Model:Class</c>.
    /// A missing key already fails the startup validation, an empty value fails the step.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// How the matches of <see cref="Path"/> reduce to one value.
    /// </summary>
    [JsonPropertyName("fetch")]
    public XtfMetadataFetch Fetch { get; init; }
}

/// <summary>
/// How the matches of a metadata path reduce to one value.
/// </summary>
internal enum XtfMetadataFetch
{
    /// <summary>
    /// Exactly one match in the whole file, otherwise no value.
    /// </summary>
    Single,

    /// <summary>
    /// The first match in file order. Reading stops there.
    /// </summary>
    First,

    /// <summary>
    /// Exactly one match among the currently valid objects, after the DMAV rule <c>VIEW Gemeinde_Gueltig</c>: the
    /// <c>Entstehung</c> of the object references an object with <c>GenehmigtAm</c>, and its <c>Untergang</c> does not.
    /// </summary>
    CurrentlyValid,
}
