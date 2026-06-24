namespace Geopilot.Api.Contracts;

/// <summary>
/// A visualization config produced by a processing step. The frontend fetches the referenced URL
/// (a self-describing JSON config) and renders the built-in component its <c>type</c> selects.
/// </summary>
/// <param name="OriginalFileName">The human-readable name of the config file.</param>
/// <param name="Url">Absolute URL to fetch the JSON config from the visualization endpoint.</param>
public record StepVisualizationResponse(string OriginalFileName, Uri Url);
