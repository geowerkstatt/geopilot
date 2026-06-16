using Geopilot.PipelineCore.Pipeline;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline;

/// <summary>
/// Reads and writes <see cref="LocalizedText"/> as a YAML mapping of language code to
/// text, matching the historical <c>Dictionary&lt;string, string&gt;</c> shape used for
/// <c>display_name</c> and condition <c>message</c> entries.
/// </summary>
internal sealed class LocalizedTextYamlConverter : IYamlTypeConverter
{
    /// <inheritdoc/>
    public bool Accepts(Type type) => type == typeof(LocalizedText);

    /// <inheritdoc/>
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        // Return null deliberately: an absent or empty display_name yields a null scalar here.
        // Leaving the property null lets the [Required] attribute flag it during validation.
        if (parser.TryConsume<Scalar>(out _))
            return null!;

        var values = new Dictionary<string, string>();
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            var value = parser.Consume<Scalar>().Value;
            values[key] = value;
        }

        return new LocalizedText(values);
    }

    /// <inheritdoc/>
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var localized = (LocalizedText)value!;
        emitter.Emit(new MappingStart());
        foreach (var language in localized.Languages)
        {
            emitter.Emit(new Scalar(language));
            emitter.Emit(new Scalar(localized[language] ?? string.Empty));
        }

        emitter.Emit(new MappingEnd());
    }
}
