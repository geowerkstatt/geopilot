using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Matcher.XtfMatcher;

internal class XtfMatcherResult
{
    public required IPipelineFile[] XtfFiles { get; set; }

    public required LocalizedText StatusMessage { get; set; }
}
