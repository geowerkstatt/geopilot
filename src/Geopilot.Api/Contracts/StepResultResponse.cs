using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The DTO for a single processing step's result.
/// </summary>
/// <param name="Id">The unique id of the step within the pipeline.</param>
/// <param name="Name">The localized display name of the step.</param>
/// <param name="State">The current state of the step.</param>
/// <param name="StatusMessage">Localized status message produced by the step (e.g. condition-driven skip/fail messages, validator summaries). <see langword="null"/> when no status message was emitted.</param>
/// <param name="Downloads">Files produced by the step that are available for download. Each entry carries the original file name and an absolute download URL.</param>
/// <param name="Visualizations">Visualization configs produced by the step. Each entry tells the frontend which built-in visualization component to render and from where to fetch its JSON config.</param>
public record StepResultResponse(
    string Id,
    LocalizedText Name,
    StepState State,
    LocalizedText? StatusMessage,
    IList<StepDownload> Downloads,
    IList<StepVisualizationResponse> Visualizations);
