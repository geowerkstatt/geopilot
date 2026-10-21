using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class Ili2GpkgExportResult
{
    public bool Success { get; init; }

    public required LocalizedText StatusMessage { get; init; }

    /// <summary>
    /// The resulting transfer file as a collection that contains one file or is empty, allowing a download action
    /// without failing the run when missing.
    /// </summary>
    public required IPipelineFile[] TransferFile { get; init; }

    public required IPipelineFile ErrorLog { get; init; }

    public required IPipelineFile XtfLog { get; init; }
}
