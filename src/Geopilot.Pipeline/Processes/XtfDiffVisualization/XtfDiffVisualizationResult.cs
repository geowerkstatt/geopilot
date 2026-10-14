using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

internal class XtfDiffVisualizationResult
{
    public required Visualization<XtfDiffVisualizationConfig> Visualization { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}
