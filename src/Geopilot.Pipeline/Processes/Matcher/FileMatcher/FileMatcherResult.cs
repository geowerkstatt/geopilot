using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Matcher.FileMatcher;

internal class FileMatcherResult
{
    public required IPipelineFile[] MatchedFiles { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}
