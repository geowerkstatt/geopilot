using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// Describes what is displayed in the map visualization on the client. Serializes to the custom
/// map-visualization JSON: a list of <see cref="Layers"/>, each either a WMTS map service layer
/// (<see cref="MapLayer.Wmts"/>) or a feature layer carrying features inline (<see cref="MapLayer.Features"/>).
/// </summary>
internal class MapVisualizationConfig
{
    /// <summary>
    /// The layers displayed in the map, drawn in order. A layer is either a WMTS layer or a feature layer.
    /// </summary>
    [JsonPropertyName("layers")]
    public required IList<MapLayer> Layers { get; set; }
}

/// <summary>
/// A single map layer. Exactly one of <see cref="Wmts"/> or <see cref="Features"/> is set; the other is
/// omitted from the serialized JSON.
/// </summary>
internal class MapLayer
{
    /// <summary>
    /// Localized display title of the layer, keyed by language ("de", "en", ...). Shown in the client's
    /// layer switcher. Optional; the client falls back to a generic title.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? Title { get; set; }

    /// <summary>
    /// The capabilities URL of a WMTS map service. Set for WMTS layers; otherwise <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("wmts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Wmts { get; set; }

    /// <summary>
    /// Identifiers of the layers to display from the WMTS service referenced by <see cref="Wmts"/>. When
    /// <see langword="null"/> or empty, the client displays all layers the service advertises (wrapped in a
    /// group layer if there is more than one). Only meaningful for WMTS layers.
    /// </summary>
    [JsonPropertyName("layerIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<string>? LayerIds { get; set; }

    /// <summary>
    /// Color of the layer's features as a hex color (e.g. <c>#e53835</c>). The client uses it as the
    /// stroke color and a transparent variant of it as the fill color for polygons. Only meaningful for
    /// feature layers. Optional; the client falls back to its theme color.
    /// </summary>
    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }

    /// <summary>
    /// Attribution / data-owner credit for the layer (for example <c>swisstopo</c>), shown as a copyright
    /// credit on the map; the client adds a localized "©" prefix. Typically set on the base map layer.
    /// Optional.
    /// </summary>
    [JsonPropertyName("attribution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Attribution { get; set; }

    /// <summary>
    /// Optional URL the attribution links to (for example the map owner's terms of use). When set, the
    /// client renders <see cref="Attribution"/> as a link.
    /// </summary>
    [JsonPropertyName("attributionUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributionUrl { get; set; }

    /// <summary>
    /// Features rendered directly from the JSON. Set for feature layers; otherwise <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("features")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<MapFeature>? Features { get; set; }
}

/// <summary>
/// A single feature inside a feature layer.
/// </summary>
internal class MapFeature
{
    /// <summary>
    /// Stable id of the validation error this feature represents, shared with the error's tree node so the
    /// frontend can cross-select map and tree.
    /// </summary>
    [JsonPropertyName("errorId")]
    public required string ErrorId { get; set; }

    /// <summary>
    /// The feature geometry as Well-Known Text (WKT), for example <c>POINT(2600000 1200000)</c>.
    /// </summary>
    [JsonPropertyName("geom")]
    public required string Geom { get; set; }

    /// <summary>
    /// The informational text shown for the feature.
    /// </summary>
    [JsonPropertyName("info")]
    public required string Info { get; set; }
}
