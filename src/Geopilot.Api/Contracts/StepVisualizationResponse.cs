using Geopilot.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// A visualization config produced by a processing step. The frontend fetches the referenced URL
/// and renders the matching built-in visualization component for <see cref="Kind"/>.
/// </summary>
/// <param name="Kind">The kind of visualization this config drives.</param>
/// <param name="OriginalFileName">The human-readable name of the config file.</param>
/// <param name="Url">Absolute URL to fetch the JSON config.</param>
public record StepVisualizationResponse(VisualizationKind Kind, string OriginalFileName, Uri Url);
