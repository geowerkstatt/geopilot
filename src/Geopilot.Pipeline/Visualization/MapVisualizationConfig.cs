namespace Geopilot.Pipeline.Visualization;

/// <summary>
/// Describes what is displayed in the map of a visualization on the client. Serializes to the custom
/// map-visualization JSON: a list of <see cref="Layers"/>, each either a WMTS map service layer
/// (<see cref="MapLayer.Wmts"/>) or a feature layer carrying features inline (<see cref="MapLayer.Features"/>).
/// </summary>
internal class MapVisualizationConfig
{
    /// <summary>
    /// The layers displayed in the map, drawn in order. A layer is either a WMTS layer or a feature layer.
    /// </summary>
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
    public IDictionary<string, string>? Title { get; set; }

    /// <summary>
    /// The capabilities URL of a WMTS map service. Set for WMTS layers; otherwise <see langword="null"/>.
    /// </summary>
    public string? Wmts { get; set; }

    /// <summary>
    /// Identifiers of the layers to display from the WMTS service referenced by <see cref="Wmts"/>. When
    /// <see langword="null"/> or empty, the client displays all layers the service advertises (wrapped in a
    /// group layer if there is more than one). Only meaningful for WMTS layers.
    /// </summary>
    public IList<string>? LayerIds { get; set; }

    /// <summary>
    /// Attribution / data-owner credit for the layer (for example <c>swisstopo</c>), shown as a copyright
    /// credit on the map; the client adds a localized "©" prefix. Typically set on the base map layer.
    /// Optional.
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Optional URL the attribution links to (for example the map owner's terms of use). When set, the
    /// client renders <see cref="Attribution"/> as a link.
    /// </summary>
    public string? AttributionUrl { get; set; }

    /// <summary>
    /// Features rendered directly from the JSON. Set for feature layers; otherwise <see langword="null"/>.
    /// </summary>
    public IList<MapFeature>? Features { get; set; }

    /// <summary>
    /// The state of a diff the features of a feature layer show. The client draws such a layer by the change
    /// type of each feature and compares the two states. <see langword="null"/> for a layer outside a diff,
    /// drawn in the default map color.
    /// </summary>
    public DiffState? State { get; set; }
}

/// <summary>
/// The state before or after a diff.
/// </summary>
internal enum DiffState
{
    /// <summary>
    /// The old state of something that changed.
    /// </summary>
    Old,

    /// <summary>
    /// The new state of something that changed.
    /// </summary>
    New,
}

/// <summary>
/// A single feature inside a feature layer.
/// </summary>
internal class MapFeature
{
    /// <summary>
    /// Stable id of what this feature represents, for example a validation error, shared with its entry in
    /// the list or tree next to the map so the frontend can cross-select the two.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// The feature geometry as Well-Known Text (WKT), for example <c>POINT(2600000 1200000)</c>.
    /// </summary>
    public required string Geom { get; set; }

    /// <summary>
    /// The informational text shown for the feature.
    /// </summary>
    public required string Info { get; set; }
}
