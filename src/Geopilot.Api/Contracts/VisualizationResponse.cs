using System.Text.Json.Serialization;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The body returned by the visualization endpoint: a self-describing visualization config with a
/// <see cref="Type"/> discriminator and the matching <see cref="Data"/> payload. The frontend selects
/// the component from <see cref="Type"/> and renders <see cref="Data"/>. Documents the wire shape for
/// the API; the bytes are streamed straight from the visualization store.
/// </summary>
/// <param name="Type">The visualization type discriminator (e.g. <c>map</c>, <c>tree</c>).</param>
/// <param name="Data">The visualization payload, whose shape depends on <see cref="Type"/>.</param>
public record VisualizationResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] object Data);
