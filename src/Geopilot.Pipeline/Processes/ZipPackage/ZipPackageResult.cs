using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.ZipPackage;

internal class ZipPackageResult
{
    public IPipelineFile? ZipPackage { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}
