using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Matcher.FileMatcher
{
    internal class FileMatcherResult
    {
        public required IPipelineFile[] MatchedFiles { get; set; }

        public required LocalizedText StatusMessage { get; set; }
    }
}
