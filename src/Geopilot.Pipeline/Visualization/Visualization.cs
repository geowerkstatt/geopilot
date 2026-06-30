using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// Envelope for a visualization produced by a pipeline step: a self-describing <see cref="Type"/>
/// discriminator plus the typed <see cref="Data"/> payload. Serialized to JSON and consumed by the
/// frontend, which picks the matching component from <see cref="Type"/> and renders <see cref="Data"/>.
/// </summary>
/// <typeparam name="TData">The visualization payload type (e.g. a map or tree config).</typeparam>
/// <param name="Type">The visualization type discriminator (e.g. <c>map</c>, <c>tree</c>).</param>
/// <param name="Data">The payload rendered by the frontend component selected by <paramref name="Type"/>.</param>
internal sealed record Visualization<TData>(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] TData Data) : IVisualization;

/// <summary>
/// Marker for a visualization envelope, independent of its payload type. An output tagged with the
/// <c>Visualization</c> output action must carry an <see cref="IVisualization"/> so the runtime can
/// serve a well-formed <c>{ type, data }</c> document; the pipeline step enforces this.
/// </summary>
internal interface IVisualization
{
    /// <summary>
    /// The visualization type discriminator (e.g. <c>map</c>, <c>tree</c>).
    /// </summary>
    string Type { get; }
}

/// <summary>
/// Creates <see cref="Visualization{TData}"/> envelopes, binding each payload type to its discriminator
/// in one place so the type and the data cannot drift apart.
/// </summary>
internal static class VisualizationFactory
{
    /// <summary>
    /// Wraps a composite XTF error visualization config (map and/or tree) in its envelope.
    /// </summary>
    /// <param name="data">The composite config payload.</param>
    /// <returns>The XTF error visualization envelope.</returns>
    public static Visualization<XtfErrorVisualizationConfig> XtfError(XtfErrorVisualizationConfig data) => new("xtfError", data);
}
