using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Unzip;

internal class UnzipResult
{
    public required IPipelineFile[] ExtractedFiles { get; set; }

    public required LocalizedText StatusMessage { get; set; }
}
