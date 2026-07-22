using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.XtfValidation;

internal class XtfValidatorResult
{
    public bool ValidationSuccessful { get; init; }

    public required LocalizedText StatusMessage { get; init; }

    public IPipelineFile? ErrorLog { get; init; }

    public IPipelineFile? XtfLog { get; init; }
}
