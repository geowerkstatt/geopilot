using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Matcher.XtfMatcher;

internal class XtfMatcherResult
{
    public required IPipelineFile[] XtfFiles { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}
