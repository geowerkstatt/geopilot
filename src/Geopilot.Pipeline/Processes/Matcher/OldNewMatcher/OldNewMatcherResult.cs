using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Matcher.OldNewMatcher;

internal class OldNewMatcherResult
{
    public required IPipelineFile[] OldFiles { get; init; }

    public required IPipelineFile[] NewFiles { get; init; }

    public required IPipelineFile[] UnmatchedFiles { get; init; }

    public required RoleDecision DecidedBy { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}

/// <summary>
/// What matched the files to the old and the new state.
/// </summary>
internal enum RoleDecision
{
    /// <summary>
    /// The configured role words in the file names, an unambiguous feature. Also when they leave a state without a file or
    /// with several, which the pipeline checks with a condition on the counts.
    /// </summary>
    RoleWords,

    /// <summary>
    /// The ascending order of the names of exactly two files, because neither carries a role word. A guess the pipeline
    /// should make visible.
    /// </summary>
    NameOrder,
}
