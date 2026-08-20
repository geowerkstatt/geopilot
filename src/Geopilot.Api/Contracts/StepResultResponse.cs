using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The DTO for a single processing step's result.
/// </summary>
/// <param name="Id">The unique id of the step within the pipeline.</param>
/// <param name="Name">The localized display name of the step.</param>
/// <param name="State">The current state of the step.</param>
/// <param name="StatusMessage">Localized status message produced by the process itself (e.g. validator summaries). <see langword="null"/> when no status message was emitted.</param>
/// <param name="ConditionMessage">Localized message from the step's matching condition (pre-fail, pre-skip, post-fail or post-warn). <see langword="null"/> when no condition message applies.</param>
/// <param name="Downloads">Files produced by the step that are available for download. Each entry carries the original file name and an absolute download URL.</param>
/// <param name="Deliveries">Files produced by the step that are available for delivery. Each entry carries the original file name.</param>
/// <param name="Visualizations">Visualization configs produced by the step. Each entry carries the original file name and an absolute URL to fetch the self-describing JSON config from the visualization endpoint.</param>
public record StepResultResponse(
    string Id,
    LocalizedText Name,
    StepState State,
    LocalizedText? StatusMessage,
    LocalizedText? ConditionMessage,
    IList<StepDownload> Downloads,
    IList<string> Deliveries,
    IList<StepVisualizationResponse> Visualizations);
