using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Unzip;

internal class UnzipResult
{
    public required IPipelineFile[] ExtractedFiles { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}
