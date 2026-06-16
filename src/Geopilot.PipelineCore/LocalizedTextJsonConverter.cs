using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.PipelineCore.Pipeline;

/// <summary>
/// Serializes <see cref="LocalizedText"/> as a flat JSON object keyed by language code
/// (for example <c>{ "de": "...", "en": "..." }</c>), identical to a plain dictionary.
/// </summary>
internal sealed class LocalizedTextJsonConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options)
            ?? new Dictionary<string, string>();
        return new LocalizedText(values);
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToDictionary(), options);
    }
}
