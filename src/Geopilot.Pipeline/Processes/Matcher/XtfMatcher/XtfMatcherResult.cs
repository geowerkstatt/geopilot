using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Matcher.XtfMatcher;

internal class XtfMatcherResult
{
    public required IPipelineFile[] XtfFiles { get; init; }

    public required IPipelineFile[] UnmatchedFiles { get; init; }

    /// <summary>
    /// The INTERLIS versions of the <see cref="XtfFiles"/>, each once and named like <see cref="InterlisVersion"/>.
    /// A file whose version is not recognized adds none.
    /// </summary>
    /// <remarks>Text rather than <see cref="InterlisVersion"/>, because a condition's <c>in</c> never matches an enum value.</remarks>
    public required string[] InterlisVersions { get; init; }

    public required LocalizedText StatusMessage { get; init; }
}
