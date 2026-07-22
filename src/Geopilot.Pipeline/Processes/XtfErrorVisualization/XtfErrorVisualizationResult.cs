using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

internal class XtfErrorVisualizationResult
{
    public required Visualization<XtfErrorVisualizationConfig> Visualization { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}
