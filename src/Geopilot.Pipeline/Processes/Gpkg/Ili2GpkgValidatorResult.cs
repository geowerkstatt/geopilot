using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class Ili2GpkgValidatorResult
{
    public bool ValidationSuccessful { get; init; }

    public required LocalizedText StatusMessage { get; init; }

    public required IPipelineFile ErrorLog { get; init; }

    public required IPipelineFile XtfLog { get; init; }
}
